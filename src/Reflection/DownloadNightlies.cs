using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml;
using DSharpPlus.CommandsNext;
using DSharpPlus.Interactivity;
using DSharpPlus.Lavalink;
using DSharpPlus.SlashCommands;
using DSharpPlus.VoiceNext;

namespace DSharpPlus.DocBot
{
    public partial class CachedReflection
    {
        private static readonly TargetFrameworkAttribute currentFramework = Assembly.GetExecutingAssembly().GetCustomAttribute<TargetFrameworkAttribute>()!;
        private static readonly HttpClient HttpClient = new()
        {
            DefaultRequestHeaders = { { "User-Agent", "DSharpPlus Docs Bot/2.0" } }
        };

        public static async Task DownloadNightliesAsync()
        {
            HttpRequestMessage getLatestActionRunMessage = new(HttpMethod.Get, "https://api.github.com/repos/DSharpPlus/DSharpPlus/actions/runs?branch=master&status=success&event=push&page=1&per_page=1");
            getLatestActionRunMessage.Headers.Add("Authorization", "token " + Program.Configuration["github_token"]);
            HttpResponseMessage getLatestActionRunResponse = await HttpClient.SendAsync(getLatestActionRunMessage);
            if (!getLatestActionRunResponse.IsSuccessStatusCode)
            {
                Console.WriteLine($"Github returned a non-success status code: HTTP {getLatestActionRunResponse.StatusCode} {getLatestActionRunResponse.ReasonPhrase}");
                Console.WriteLine("Falling back to built-in dependencies.");
                await SetTypesThroughReflection();
                return;
            }

            JsonDocument latestActionRun = JsonDocument.Parse(getLatestActionRunResponse.Content.ReadAsStream());
            if (!latestActionRun.RootElement.TryGetProperty("total_count", out JsonElement totalWorkflowCount) || totalWorkflowCount.GetInt32() == 0 // If no actions are available
            || !latestActionRun.RootElement.TryGetProperty("workflow_runs", out JsonElement workflowRuns) || !workflowRuns[0].TryGetProperty("artifacts_url", out JsonElement artifactsUrl)) // If no artifacts are available
            {
                // TODO: Use a proper logger dumbass
                Console.WriteLine("No workflow runs found from the Github API, unable to download the latest nightly/release. Falling back to the built-in dependencies.");
                await SetTypesThroughReflection();
                return;
            }

            HttpRequestMessage getArtifactDownloadUrlMessage = new(HttpMethod.Get, artifactsUrl.GetString());
            getArtifactDownloadUrlMessage.Headers.Add("Authorization", "token " + Program.Configuration["github_token"]);
            HttpResponseMessage getArtifactDownloadUrlResponse = await HttpClient.SendAsync(getArtifactDownloadUrlMessage);
            if (!getArtifactDownloadUrlResponse.IsSuccessStatusCode)
            {
                Console.WriteLine($"Github returned a non-success status code: HTTP {getArtifactDownloadUrlResponse.StatusCode} {getArtifactDownloadUrlResponse.ReasonPhrase}");
                Console.WriteLine("Falling back to built-in dependencies.");
                await SetTypesThroughReflection();
                return;
            }

            JsonDocument artifactDownloadUrl = JsonDocument.Parse(getArtifactDownloadUrlResponse.Content.ReadAsStream());
            if (!artifactDownloadUrl.RootElement.TryGetProperty("total_count", out JsonElement totalArtifactCount) || totalArtifactCount.GetInt32() == 0
            || !artifactDownloadUrl.RootElement.TryGetProperty("artifacts", out JsonElement artifacts) || !artifacts[0].TryGetProperty("archive_download_url", out JsonElement downloadUrl))
            {
                // TODO: Use a proper logger dumbass
                Console.WriteLine("Unable to find the latest artifact. Falling back to the built-in dependencies.");
                await SetTypesThroughReflection();
                return;
            }

            FileStream zipFile = File.OpenWrite("DSharpPlus Nightlies.zip");
            HttpRequestMessage downloadArtifactsMessage = new(HttpMethod.Get, downloadUrl.GetString());
            downloadArtifactsMessage.Headers.Add("Authorization", "token " + Program.Configuration["github_token"]);
            HttpResponseMessage downloadArtifactsResponse = await HttpClient.SendAsync(downloadArtifactsMessage);
            if (!downloadArtifactsResponse.IsSuccessStatusCode)
            {
                Console.WriteLine($"Github returned a non-success status code: HTTP {downloadArtifactsResponse.StatusCode} {downloadArtifactsResponse.ReasonPhrase}");
                Console.WriteLine("Falling back to built-in dependencies.");
                await SetTypesThroughReflection();
                return;
            }

            downloadArtifactsResponse.Content.ReadAsStream().CopyTo(zipFile);
            zipFile.Close();

            // Begone cache, begone!
            if (Directory.Exists("DSharpPlus Nightlies"))
            {
                Directory.Delete("DSharpPlus Nightlies", true);
            }

            // TODO: This should really use the OS' temp file dir
            ZipFile.ExtractToDirectory("DSharpPlus Nightlies.zip", "DSharpPlus Nightlies");
            File.Delete("DSharpPlus Nightlies.zip");

            Console.WriteLine("Downloaded the latest nightly/release. Extracting and downloading dependencies...");
            await LoadNightliesAsync();
            Console.WriteLine("Done!");
            return;
        }

        private static async Task LoadNightliesAsync()
        {
            List<Type> types = new();
            string previousDir = Environment.CurrentDirectory;
            Environment.CurrentDirectory = Path.GetFullPath("DSharpPlus Nightlies");
            foreach (string fileName in Directory.GetFiles(Environment.CurrentDirectory, "*.nupkg").OrderBy(x => x))
            {
                string extractedDllName = await UnpackNupkgAsync(fileName) + ".dll";
                Console.WriteLine("Attempting to load: " + Path.GetFullPath(extractedDllName));
                Assembly assembly = Assembly.LoadFrom(Path.GetFullPath(extractedDllName));
                types.AddRange(assembly.GetExportedTypes());
                Console.WriteLine("Loaded " + assembly.FullName);
            }
            Types = types.ToArray();
            Environment.CurrentDirectory = previousDir;
            SetProperties();
        }

        private static async Task<string> UnpackNupkgAsync(string nupkgFile)
        {
            string dll = Path.GetFullPath(Path.ChangeExtension(nupkgFile, ".dll"));
            string symbols = Path.GetFullPath(Path.ChangeExtension(nupkgFile, ".snupkg"));
            string dependencies = Path.GetFullPath(Path.ChangeExtension(nupkgFile, ".nuspec"));
            string? assemblyName = null;
            List<string> irresponsiblyNamedFileList = new();

            using ZipArchive archive = ZipFile.Open(nupkgFile, ZipArchiveMode.Read);
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                // TODO: Extract the DLL first, then the symbols, then the dependencies, all under the base ProductName
                if (entry.FullName.EndsWith(".dll"))
                {
                    entry.ExtractToFile(dll);
                    FileVersionInfo fileVersionInfo = FileVersionInfo.GetVersionInfo(dll);
                    File.Move(dll, fileVersionInfo.ProductName + ".dll");
                    assemblyName = fileVersionInfo.ProductName;
                }
                else if (entry.FullName.EndsWith(".snupkg"))
                {
                    if (assemblyName == null)
                    {
                        irresponsiblyNamedFileList.Add(symbols);
                        entry.ExtractToFile(symbols);
                    }
                    else
                    {
                        entry.ExtractToFile(assemblyName + ".snupkg");
                    }
                }
                else if (entry.FullName.EndsWith(".nuspec"))
                {
                    if (assemblyName == null)
                    {
                        irresponsiblyNamedFileList.Add(dependencies);
                        entry.ExtractToFile(dependencies);
                    }
                    else
                    {
                        entry.ExtractToFile(assemblyName + ".nuspec");
                    }

                }
            }

            foreach (string poorlyNamedFile in irresponsiblyNamedFileList)
            {
                File.Move(poorlyNamedFile, assemblyName + Path.GetExtension(poorlyNamedFile));
            }

            File.Delete(nupkgFile);
            if (File.Exists(assemblyName + ".nuspec"))
            {
                await ResolveDependenciesAsync(assemblyName + ".nuspec");
            }
            return assemblyName!;
        }

        private static async Task ResolveDependenciesAsync(string nuspecFile)
        {
            // Parse dependencies
            using XmlReader reader = XmlReader.Create(nuspecFile);
            if (reader.ReadToFollowing("dependencies")) // If the nupkg has dependencies
            {
                XmlReader groupNodes = reader.ReadSubtree();
                groupNodes.Read();
                while (groupNodes.Read())
                {
                    if (groupNodes.Name == "")
                    {
                        continue;
                    }

                    //string? targetFramework = reader.GetAttribute("targetFramework");
                    XmlReader dependencyNodes = reader.ReadSubtree();
                    dependencyNodes.Read();
                    while (dependencyNodes.Read())
                    {
                        if (dependencyNodes.Name == "")
                        {
                            continue;
                        }

                        string dependencyId = dependencyNodes.GetAttribute("id")!.ToLowerInvariant();
                        if (File.Exists(dependencyId + ".nupkg"))
                        {
                            continue;
                        }

                        string dependencyVersion = dependencyNodes.GetAttribute("version")!;
                        string dependencyUrl = $"https://api.nuget.org/v3-flatcontainer/{dependencyId}/{dependencyVersion}/{dependencyId}.{dependencyVersion}.nupkg";
                        Console.WriteLine($"Fetching {dependencyId} v{dependencyVersion}");
                        FileStream zipFile = File.OpenWrite(dependencyId + ".nupkg");
                        (await HttpClient.GetStreamAsync(dependencyUrl)).CopyTo(zipFile);
                        zipFile.Close();

                        await UnpackNupkgAsync(dependencyId + ".nupkg");
                        File.Delete(dependencyId + ".nupkg");
                    }
                }
            }
        }

        // Gather all D#+ types
        private static Task SetTypesThroughReflection()
        {
            if (Directory.Exists("DSharpPlus Nightlies"))
            {
                // Cache!
                return LoadNightliesAsync();
            }

            Types = typeof(DiscordClient).Assembly.ExportedTypes
            .Concat(typeof(CommandsNextExtension).Assembly.ExportedTypes)
            .Concat(typeof(InteractivityExtension).Assembly.ExportedTypes)
            .Concat(typeof(LavalinkExtension).Assembly.ExportedTypes)
            .Concat(typeof(SlashCommandsExtension).Assembly.ExportedTypes)
            .Concat(typeof(VoiceNextExtension).Assembly.ExportedTypes)
            .ToArray();

            SetProperties();
            return Task.CompletedTask;
        }

        private static void SetProperties()
        {
            // Gather all D#+ methods, grouping them by method name and grouping them by method overloads
            MethodGroups = Types
                .SelectMany(type => type
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static) // TODO: I'd like to use binding flags here as I feel it'd be more efficient however I haven't been able to understand how they work yet.
                    .Where(method =>
                        !method.IsSpecialName && // Drop these stupid getters and setters.
                        (method.GetBaseDefinition().DeclaringType?.Namespace?.StartsWith("DSharpPlus") ?? false))) // Drop methods not implemented by us (aka object and Enum methods)
                .GroupBy(method => method.Name) // Method name
                .ToDictionary(method => method.Key, method => method.ToArray()); // Group method overloads by method name

            // I believe this grabs all public properties
            Properties = Types.SelectMany(t => t.GetProperties()).ToArray();

            // Grab all events from all extensions
            // Not even sure if these binding flags are needed...
            Events = Types.SelectMany(t => t.GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)).ToArray();
        }
    }
}
