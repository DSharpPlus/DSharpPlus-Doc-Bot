using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus.DocBot.Interfaces;
using DSharpPlus.DocBot.Types;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DSharpPlus.DocBot.Services.AssemblyFetchers
{
    public sealed class LocalProjectAssemblyFetcher : AbstractAssemblyFetcher
    {
        public LocalProjectAssemblyFetcher(IConfiguration configuration, ILogger<LocalProjectAssemblyFetcher> logger) : base(configuration, logger)
        {
            if (!Configuration.GetValue<string>("documentation:local_project:sources").Split(';').Any())
            {
                throw new ArgumentException("No sources found in configuration.", nameof(configuration));
            }
        }

        public override Task<bool> CheckForUpdateAsync()
        {
            string[] sources = Configuration.GetValue<string>("documentation:local_project:sources").Split(';');
            foreach (string source in sources)
            {
                string resolvedFile = ResolveFile(source);
                if (LastModifiedAt.TryGetValue(resolvedFile, out DateTimeOffset sourceModificationTime) && File.GetLastWriteTime(resolvedFile) != sourceModificationTime)
                {
                    Logger.LogInformation("Documentation has been updated.");
                    return Task.FromResult(true);
                }
            }
            return Task.FromResult(false);
        }

        public override bool TryFetch([NotNullWhen(true)] out IEnumerable<AssemblyLoadInfo>? assemblies)
        {
            assemblies = null;
            string[] projectFiles = Configuration.GetSection("documentation:local_project:sources").Get<string[]>();
            if (projectFiles.Length == 1 && File.GetAttributes(projectFiles[0]).HasFlag(FileAttributes.Directory))
            {
                projectFiles = Directory.GetFiles(projectFiles[0], "*.csproj", SearchOption.AllDirectories);
                if (projectFiles.Length == 0)
                {
                    Logger.LogError("No .csproj files found in the specified directory.");
                    Logger.LogWarning("Failed to load local project, falling back to local documentation.");
                    return false;
                }
            }

            List<string> loadedAssemblies = new();
            foreach (string csProjFile in projectFiles)
            {
                if (!File.Exists(csProjFile) || File.GetAttributes(csProjFile).HasFlag(FileAttributes.Directory) || Path.GetExtension(csProjFile) != ".csproj")
                {
                    Logger.LogWarning("Skipping invalid file {File}", csProjFile);
                    continue;
                }

                ProcessStartInfo startInfo = new()
                {
                    CreateNoWindow = true,
                    FileName = "dotnet",
                    WorkingDirectory = Path.GetDirectoryName(csProjFile)
                };
                startInfo.ArgumentList.Add("build");
                startInfo.ArgumentList.Add("-p:GenerateDocumentationFile=true");
                startInfo.ArgumentList.Add("-p:EnableDynamicLoading=true");
                startInfo.RedirectStandardError = true;
                startInfo.RedirectStandardOutput = true;

                Logger.LogDebug("Building {File}", csProjFile);
                Process process = Process.Start(startInfo)!;
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    Logger.LogError("Failed to generate documentation for {ProjectFile}:\n{ErrorMessage}", csProjFile, process.StandardError.ReadToEnd());
                    Logger.LogWarning("Failed to load local project, falling back to local documentation.");
                    return false;
                }
                Logger.LogDebug("Building complete, loading assemblies...");

                string[] output = process.StandardOutput.ReadToEnd().Split('\n');
                string[] matchFiles = Configuration.GetValue("documentation:local_project:assemblies", ".dll").Split(';');
                IEnumerable<string> assemblyFiles = output.Where(line => matchFiles.Any(match => line.Contains(match))).Select(line => line.Split("->")[1].Trim());
                loadedAssemblies.AddRange(assemblyFiles);
            }

            assemblies = LoadLocalAssemblies(loadedAssemblies);
            return true;
        }

        /// <summary>
        /// Attempts to resolve the filepath. If <paramref name="filepath"/> is a file, it is returned if it exists. If it is a directory, it checks if the directory exists. It then attempts to find a solution or csproj file with the directory's name. If none is found, it'll return the first solution or csproj found. If any of the checks fail, null is returned.
        /// </summary>
        private static string ResolveFile(string filepath)
        {
            // Search the directory if it's a directory
            if (File.GetAttributes(filepath).HasFlag(FileAttributes.Directory))
            {
                // Searches for a solution or csproj file with the directory's name.
                string searchFile = Path.Join(filepath, Path.ChangeExtension(Path.GetFileName(filepath), ".sln"));
                if (File.Exists(searchFile))
                {
                    return searchFile;
                }
                else if (File.Exists(Path.ChangeExtension(searchFile, ".csproj")))
                {
                    return Path.ChangeExtension(searchFile, ".csproj");
                }

                // Returns the first found solution.
                foreach (string slnFile in Directory.GetFiles(filepath, "*.sln"))
                {
                    return slnFile;
                }

                // Retuns the first found csproj.
                foreach (string csprojFile in Directory.GetFiles(filepath, "*.csproj"))
                {
                    return csprojFile;
                }

                // No solution or csproj found.
                throw new FileNotFoundException("No solution or csproj file found in the specified directory.", filepath);
            }
            else if (!File.Exists(filepath))
            {
                throw new FileNotFoundException("File not found.", filepath);
            }
            // File exists.
            return filepath;
        }
    }
}
