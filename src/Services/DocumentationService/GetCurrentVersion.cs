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
        private IEnumerable<AssemblyLoadInfo>? LoadedAssemblies { get; set; }

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

        public IEnumerable<AssemblyLoadInfo>? GetLoadedAssemblies() => LoadedAssemblies;

        public async Task ReloadAsync()
        {
            IEnumerable<AssemblyLoadInfo>? assemblies;
            if (!await AssemblyFetcher.CheckForUpdateAsync())
            {
                if (Documentation == null || Documentation.Count == 0)
                {
                    Logger.LogWarning("Unable to load documentation on startup. Attempting to load from cache.");
                    assemblies = AssemblyFetcher.LoadCache();
                }
                else
                {
                    Logger.LogInformation("No update found.");
                    return;
                }
            }
            else if (!AssemblyFetcher.TryFetch(out assemblies))
            {
                Logger.LogError("Failed to fetch assemblies. Attempting to load from cache.");
                assemblies = AssemblyFetcher.LoadCache();
            }

            if (assemblies == null)
            {
                Logger.LogCritical("Unable to fetch assemblies and the cache is empty or missing. Bot is expected to be unusable.");
                return;
            }

            LoadedAssemblies = assemblies;
            // This is the worst thing ever. I'm sorry, performance geeks.
            Documentation = FormatDocumentation(assemblies.SelectMany(assembly => XmlMemberInfo.Parse(assembly.Assembly, assemblies.Select(a => a.Assembly), assembly.XmlDocPath)));
        }
    }
}
