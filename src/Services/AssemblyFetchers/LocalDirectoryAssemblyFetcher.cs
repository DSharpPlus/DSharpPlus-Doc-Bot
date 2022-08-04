using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;
using DSharpPlus.DocBot.Interfaces;
using DSharpPlus.DocBot.Types;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DSharpPlus.DocBot.Services.AssemblyFetchers
{
    public sealed class LocalDirectoryAssemblyFetcher : AbstractAssemblyFetcher
    {
        public LocalDirectoryAssemblyFetcher(IConfiguration configuration, ILogger<LocalDirectoryAssemblyFetcher> logger) : base(configuration, logger)
        {
            string? directory = configuration.GetValue<string>("documentation:local_directory:path");
            if (directory == null)
            {
                throw new ArgumentException("Local directory not found in configuration.", nameof(configuration));
            }
            else if (!Directory.Exists(directory))
            {
                throw new ArgumentException("Local directory does not exist.", nameof(configuration));
            }
        }

        public override Task<bool> CheckForUpdateAsync()
        {
            foreach (string file in Directory.GetFiles(Configuration.GetValue<string>("documentation:local_directory:path"), "*.dll", SearchOption.TopDirectoryOnly))
            {
                if (LastModifiedAt.TryGetValue(file, out DateTimeOffset fileModificationTime) && File.GetLastWriteTime(file) != fileModificationTime)
                {
                    Logger.LogInformation("Documentation has been updated.");
                    return Task.FromResult(true);
                }
            }
            return Task.FromResult(false);
        }

        public override bool TryFetch([NotNullWhen(true)] out IEnumerable<AssemblyLoadInfo>? assemblies)
        {
            assemblies = LoadLocalAssemblies(Directory.GetFiles(Configuration.GetValue<string>("documentation:local_directory:path"), "*.dll", SearchOption.TopDirectoryOnly));
            return true;
        }
    }
}
