﻿using System.IO;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Executors;
using DSharpPlus.DocBot.Events;
using DSharpPlus.DocBot.Pagination;
using Microsoft.Extensions.Configuration;

namespace DSharpPlus.DocBot
{
    public sealed class Program
    {
        public static IConfigurationRoot Configuration { get; private set; } = null!;

        static Program()
        {
            // Load configuration from the json file, environment variables, and command line arguments
            ConfigurationBuilder configurationBuilder = new();
            configurationBuilder.Sources.Clear();
            configurationBuilder.AddJsonFile(Path.Combine(Directory.GetCurrentDirectory(), "res/config.json"), true, true);
            configurationBuilder.AddEnvironmentVariables("DSHARPPLUS_DOCBOT_");
            Configuration = configurationBuilder.Build();
        }

        public static async Task Main()
        {
            await CachedReflection.DownloadNightliesAsync();

            DiscordShardedClient shardedClient = new(new()
            {
                AlwaysCacheMembers = false, // The bot only needs message content, not member data
                Intents = DiscordIntents.DirectMessages | DiscordIntents.GuildMessages // Respond to commands
                    | DiscordIntents.Guilds, // CNext permission checking
                Token = Configuration["discord_token"] // We can only pray the user stored this securely.
            });

            // Register CNext on each shard, in case the bot is added to thousands of servers (unlikely)
            foreach ((int _, CommandsNextExtension commandsNextExtension) in await shardedClient.UseCommandsNextAsync(new CommandsNextConfiguration()
            {
                UseDefaultCommandHandler = false, // We use our own command handler due to the prefix being the command itself
                CommandExecutor = new AsynchronousCommandExecutor() // Task.Run lmao
            }))
            {
                // Yell at the user that we couldn't find any documentation or an unexpected error occurred
                commandsNextExtension.CommandErrored += CommandErrored.CommandErroredAsync;

                // Register our singular command
                commandsNextExtension.RegisterCommands(typeof(Program).Assembly);
            }

            // Change the status to "Listening"
            shardedClient.Ready += Ready.ReadyAsync;

            // Our command handler
            shardedClient.MessageCreated += MessageCreated.MessageCreatedAsync;

            // Pagination
            shardedClient.ComponentInteractionCreated += MenuPaginator.PaginateAsync;

            // Connect to Discord praying there aren't any breaking changes
            await shardedClient.StartAsync();

            // And now we wait for the heat death of the universe
            await Task.Delay(-1);
        }
    }
}
