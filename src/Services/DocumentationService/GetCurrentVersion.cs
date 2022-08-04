using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus.DocBot.Interfaces;
using DSharpPlus.DocBot.Types;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DSharpPlus.DocBot.Services
{
    public sealed partial class DocumentationService : IDocumentationService
    {
        /// <summary>
        /// The IConfiguration used to load the documentation. Loads values from the environment variables, config file and command line arguments.
        /// </summary>
        private IConfiguration Configuration { get; init; }

        /// <summary>
        /// The ILogger used to log messages.
        /// </summary>
        private ILogger<DocumentationService> Logger { get; init; }

        /// <summary>
        /// The last loaded assembly's informational version.
        /// </summary>
        private string? CurrentVersion { get; set; }

        private IReadOnlyDictionary<string, Page> Documentation { get; set; } = new Dictionary<string, Page>();

        private AbstractAssemblyFetcher AssemblyFetcher { get; init; }

        public DocumentationService(IConfiguration configuration, ILogger<DocumentationService> logger, AbstractAssemblyFetcher assemblyFetcher)
        {
            ArgumentNullException.ThrowIfNull(configuration, nameof(configuration));
            ArgumentNullException.ThrowIfNull(logger, nameof(logger));
            ArgumentNullException.ThrowIfNull(assemblyFetcher, nameof(assemblyFetcher));

            Configuration = configuration;
            Logger = logger;
            AssemblyFetcher = assemblyFetcher;
            ReloadAsync().GetAwaiter().GetResult();
        }

        public string? GetCurrentVersion() => CurrentVersion;

        public async Task ReloadAsync()
        {
            if (!await AssemblyFetcher.CheckForUpdateAsync())
            {
                Logger.LogInformation("No update found.");
                return;
            }
            else if (!AssemblyFetcher.TryFetch(out IEnumerable<AssemblyLoadInfo>? assemblies))
            {
                Logger.LogError("Failed to fetch assemblies.");
            }
            else
            {
                CurrentVersion = assemblies.Last().Version;

                // This is the worst thing ever.
                Documentation = FormatDocumentation(assemblies.SelectMany(assembly => XmlMemberInfo.Parse(assembly.Assembly, assemblies.Select(a => a.Assembly), assembly.XmlDocPath)));
            }
        }
    }
}
