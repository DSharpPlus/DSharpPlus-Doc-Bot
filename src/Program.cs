using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Executors;
using DSharpPlus.DocBot.Events;
using DSharpPlus.DocBot.Interfaces;
using DSharpPlus.DocBot.Services;
using DSharpPlus.DocBot.Services.AssemblyFetchers;
using DSharpPlus.Interactivity.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

namespace DSharpPlus.DocBot
{
    public sealed class Program
    {
        /// <summary>
        /// Lunar's Custom Colored Console Logger Theme.
        /// </summary>
        private static readonly AnsiConsoleTheme LunarLoggerTheme = new(new Dictionary<ConsoleThemeStyle, string>
        {
            [ConsoleThemeStyle.Text] = "\x1b[0m",
            [ConsoleThemeStyle.SecondaryText] = "\x1b[90m",
            [ConsoleThemeStyle.TertiaryText] = "\x1b[90m",
            [ConsoleThemeStyle.Invalid] = "\x1b[31m",
            [ConsoleThemeStyle.Null] = "\x1b[95m",
            [ConsoleThemeStyle.Name] = "\x1b[93m",
            [ConsoleThemeStyle.String] = "\x1b[96m",
            [ConsoleThemeStyle.Number] = "\x1b[95m",
            [ConsoleThemeStyle.Boolean] = "\x1b[95m",
            [ConsoleThemeStyle.Scalar] = "\x1b[95m",
            [ConsoleThemeStyle.LevelVerbose] = "\x1b[34m",
            [ConsoleThemeStyle.LevelDebug] = "\x1b[90m",
            [ConsoleThemeStyle.LevelInformation] = "\x1b[36m",
            [ConsoleThemeStyle.LevelWarning] = "\x1b[33m",
            [ConsoleThemeStyle.LevelError] = "\x1b[31m",
            [ConsoleThemeStyle.LevelFatal] = "\x1b[97;91m",
        });

        public static async Task Main(string[] args)
        {
            IConfiguration? configuration = LoadConfiguration(args);
            if (configuration == null)
            {
                Console.WriteLine("Failed to load configuration due to unknown errors.");
                Environment.Exit(1); // Respect the Linux users!
                return; // Shutup Roslyn. Configuration isn't null.
            }

            CancellationTokenSource cancellationTokenSource = new();
            ServiceCollection serviceCollection = new();
            serviceCollection.AddSingleton(configuration);
            serviceCollection.AddSingleton(cancellationTokenSource);
            serviceCollection.AddLogging(logger =>
            {
                LoggerConfiguration loggerConfiguration = new();
                loggerConfiguration.MinimumLevel.Is(configuration.GetValue("logging:level", LogEventLevel.Information));
                loggerConfiguration.Enrich.WithThreadId();
                loggerConfiguration.WriteTo.Console(
                    theme: LunarLoggerTheme,
                    outputTemplate: configuration.GetValue("logging:format", "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u4}] [{ThreadId}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
                );
                loggerConfiguration.WriteTo.File(
                    $"logs/{DateTime.Now.ToUniversalTime().ToString("yyyy'-'MM'-'dd' 'HH'_'mm'_'ss", CultureInfo.InvariantCulture)}.log",
                    rollingInterval: configuration.GetValue("logging:rolling_interval", RollingInterval.Day),
                    outputTemplate: configuration.GetValue("logging:format", "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u4}] [{ThreadId}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
                );

                Log.Logger = loggerConfiguration.CreateLogger();
                logger.AddSerilog(Log.Logger);
            });

            serviceCollection.AddSingleton(serviceProvider =>
            {
                IConfiguration configuration = serviceProvider.GetRequiredService<IConfiguration>();

                DiscordConfiguration discordConfig = new()
                {
                    Token = configuration.GetValue<string>("discord:token"),
                    Intents = DiscordIntents.Guilds | DiscordIntents.GuildMessages | DiscordIntents.DirectMessages,
                    LoggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>()
                };

                return new DiscordShardedClient(discordConfig);
            });
            serviceCollection.AddSingleton(typeof(IPaginatorService), typeof(PaginatorService));
            serviceCollection.AddSingleton(typeof(IDocumentationService), typeof(DocumentationService));
            serviceCollection.AddTransient(typeof(AbstractAssemblyFetcher), (serviceProvider) =>
            {
                IConfiguration configuration = serviceProvider.GetRequiredService<IConfiguration>();
                return ActivatorUtilities.GetServiceOrCreateInstance(serviceProvider, configuration.GetValue<AssemblyFetchType>("documentation:load_type") switch
                {
                    AssemblyFetchType.DirectLink => typeof(DirectLinkAssemblyFetcher),
                    AssemblyFetchType.GithubActions => typeof(GithubActionsAssemblyFetcher),
                    AssemblyFetchType.LocalDirectory => typeof(LocalDirectoryAssemblyFetcher),
                    AssemblyFetchType.LocalProject => typeof(LocalProjectAssemblyFetcher),
                    _ => throw new ArgumentException("Invalid assembly fetch type.")
                });
            });

            ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();
            DiscordShardedClient shardedClient = serviceProvider.GetRequiredService<DiscordShardedClient>();

            shardedClient.MessageCreated += CommandExecutor.MessageCreatedAsync;
            shardedClient.ComponentInteractionCreated += PaginatorHandler.PaginateAsync;
            shardedClient.GuildDownloadCompleted += new SetStatus(serviceProvider.GetRequiredService<IConfiguration>(), serviceProvider.GetRequiredService<ILogger<SetStatus>>()).SetStatusAsync;
            foreach ((int _, CommandsNextExtension commandsNextExtension) in await shardedClient.UseCommandsNextAsync(new CommandsNextConfiguration()
            {
                Services = serviceProvider,
                EnableDefaultHelp = false, // We'll handle this ourselves
                UseDefaultCommandHandler = false, // We use our own command handler due to the prefix being the command itself
                CommandExecutor = new AsynchronousCommandExecutor() // Task.Run lmao
            }))
            {
                commandsNextExtension.RegisterCommands(typeof(Program).Assembly);
            }

            Console.CancelKeyPress += async (sender, eventArgs) =>
            {
                eventArgs.Cancel = true;
                cancellationTokenSource.Cancel(false);
                if (shardedClient.ShardClients.Count != 0)
                {
                    await shardedClient.StopAsync();
                }
            };

            await shardedClient.UseInteractivityAsync();

            System.Timers.Timer timer = new(configuration.GetValue("documentation:update_interval", TimeSpan.FromHours(1)).TotalMilliseconds);
            timer.Elapsed += async (sender, eventArgs) => await serviceProvider.GetRequiredService<IDocumentationService>().ReloadAsync();
            timer.Start();

            await shardedClient.StartAsync();
            await Task.Delay(-1);
        }

        internal static IConfiguration? LoadConfiguration(string[] args)
        {
            ConfigurationBuilder configurationBuilder = new();
            configurationBuilder.Sources.Clear();

            string configurationFilePath = Path.Join(Environment.CurrentDirectory, "res", "config.json");
            if (File.Exists(configurationFilePath))
            {
                configurationBuilder.AddJsonFile(Path.Join(Environment.CurrentDirectory, "res", "config.json"), true, true);
            }

            configurationBuilder.AddEnvironmentVariables("DOCBOT:");
            configurationBuilder.AddCommandLine(args);

            return configurationBuilder.Build();
        }
    }
}
