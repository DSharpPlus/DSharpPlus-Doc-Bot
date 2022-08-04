using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DSharpPlus.DocBot.Types;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DSharpPlus.DocBot.Interfaces
{
    /// <summary>
    /// How to download the documentation.
    /// </summary>
    public enum AssemblyFetchType
    {
        /// <summary>
        /// Downloads the latest documentation from the latest Github Action. Supports Nupkgs, DLLs or a zip file containing both.
        /// </summary>
        GithubActions,

        /// <summary>
        /// Downloads the file from the specified URL. Supports Nupkgs, DLLs or a zip file containing both.
        /// </summary>
        DirectLink,

        /// <summary>
        /// Attempts to build and load the documentation from a csproj or SLN file. The dotnet executable must be in the path.
        /// </summary>
        LocalProject,

        /// <summary>
        /// A directory containing Nupkgs or DLLs.
        /// </summary>
        LocalDirectory
    }

    /// <summary>
    /// Attempts to load the latest version of the assemblies used for the documentation.
    /// </summary>
    public abstract class AbstractAssemblyFetcher
    {
        /// <summary>
        /// Where the documentation DLL's or Nupkgs are expected to be temporarily and unreliably stored.
        /// </summary>
        public virtual string CacheDirectory { get; init; } = Path.Join(Path.GetTempPath(), "DocBot Assemblies");

        /// <summary>
        /// When a file or url (from header) was last modified at.
        /// </summary>
        internal Dictionary<string, DateTimeOffset> LastModifiedAt { get; init; } = new();
        internal virtual IConfiguration Configuration { get; init; }
        internal virtual ILogger<AbstractAssemblyFetcher> Logger { get; init; }

        public AbstractAssemblyFetcher(IConfiguration configuration, ILogger<AbstractAssemblyFetcher> logger)
        {
            ArgumentNullException.ThrowIfNull(configuration, nameof(configuration));
            ArgumentNullException.ThrowIfNull(logger, nameof(logger));

            Configuration = configuration;
            Logger = logger;
        }

        /// <summary>
        /// Checks to see if the documentation is up to date.
        /// </summary>
        public abstract Task<bool> CheckForUpdateAsync();

        /// <summary>
        /// Attempts to load the latest version of the assemblies.
        /// </summary>
        /// <param name="assemblies">The loaded assemblies.</param>
        /// <returns>Whether the assemblies were successfully loaded.</returns>
        public abstract bool TryFetch([NotNullWhen(true)] out IEnumerable<AssemblyLoadInfo>? assemblies);

        internal IEnumerable<AssemblyLoadInfo> LoadLocalAssemblies(IEnumerable<string>? assembliesToLoad = null)
        {
            assembliesToLoad ??= Directory.GetFiles(CacheDirectory, "*.dll").OrderBy(x => x);
            Regex? ignoreFileRegex = Configuration.GetValue<string?>("documentation:ignore_file_regex", null) == null ? null : new(Configuration.GetValue<string>("documentation:ignore_file_regex"));
            List<AssemblyLoadInfo> assemblies = new();
            string currentDirectory = Environment.CurrentDirectory;

            foreach (string file in assembliesToLoad)
            {
                if (ignoreFileRegex != null && ignoreFileRegex.IsMatch(file))
                {
                    Logger.LogInformation("Ignoring file {File}", file);
                    continue;
                }
                Logger.LogDebug("Attempting to load {DllFile} into the documentation assembly context.", file);

                DocumentationLoadContext assemblyLoadContext = new(file);
                Assembly assembly;
                try
                {
                    Environment.CurrentDirectory = Path.GetDirectoryName(file)!;
                    assembly = assemblyLoadContext.LoadFromAssemblyPath(file);
                    if (assembly.ExportedTypes.Any())
                    {
                        assemblies.Add(new(assembly, File.Exists(Path.ChangeExtension(file, ".xml")) ? Path.ChangeExtension(file, ".xml") : null));
                        Logger.LogInformation("Successfully loaded {AssemblyName}, Version {AssemblyVersion} ({DllName}) into the documentation assembly context.", assemblies[^1].Assembly.GetName().Name, assemblies[^1].Version, file);
                        continue;
                    }
                    Logger.LogDebug("Skipping {AssemblyName} due to no exported types.", assembly.GetName().Name);
                }
                catch (Exception error)
                {
                    Logger.LogError("Failed to load {DllFile} into the documentation assembly context. Error message: {ErrorMessage}", file, error.Message);
                    continue;
                }
            }

            Environment.CurrentDirectory = currentDirectory;
            return assemblies;
        }

        public IEnumerable<AssemblyLoadInfo>? LoadCache()
        {
            if (!Directory.Exists(CacheDirectory))
            {
                Logger.LogDebug("Cache directory {CacheDirectory} does not exist.", CacheDirectory);
                return null;
            }

            return LoadLocalAssemblies();
        }
    }
}
