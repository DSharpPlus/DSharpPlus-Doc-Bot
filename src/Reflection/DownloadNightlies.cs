using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.Interactivity;
using DSharpPlus.Lavalink;
using DSharpPlus.SlashCommands;
using DSharpPlus.VoiceNext;

namespace DSharpPlus.DocBot
{
    public partial class CachedReflection
    {
        public static async Task DownloadNightliesAsync()
        {
            HttpClient httpClient = new();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "DSharpPlus Docs Bot/2.0");
            httpClient.DefaultRequestHeaders.Add("Authorization", "token " + Program.Configuration["github_token"]);

            JsonDocument latestActionRun = JsonDocument.Parse(await httpClient.GetStreamAsync("https://api.github.com/repos/DSharpPlus/DSharpPlus/actions/runs?branch=master&status=success&event=push&page=1&per_page=1"));
            if (!latestActionRun.RootElement.TryGetProperty("total_count", out JsonElement totalWorkflowCount) || totalWorkflowCount.GetInt32() == 0 // If no actions are available
            || !latestActionRun.RootElement.TryGetProperty("workflow_runs", out JsonElement workflowRuns) || !workflowRuns[0].TryGetProperty("artifacts_url", out JsonElement artifactsUrl)) // If no artifacts are available
            {
                // TODO: Use a proper logger dumbass
                Console.WriteLine("No workflow runs found from the Github API, unable to download the latest nightly/release. Falling back to the built-in dependencies.");
                await SetTypesThroughReflection();
                return;
            }

            Console.WriteLine(latestActionRun.RootElement); // Should print the entire json object

            JsonDocument artifactDownloadUrl = JsonDocument.Parse(await httpClient.GetStreamAsync(artifactsUrl.GetString()));
            if (!artifactDownloadUrl.RootElement.TryGetProperty("total_count", out JsonElement totalArtifactCount) || totalArtifactCount.GetInt32() == 0
            || !artifactDownloadUrl.RootElement.TryGetProperty("artifacts", out JsonElement artifacts) || !artifacts[0].TryGetProperty("archive_download_url", out JsonElement downloadUrl))
            {
                // TODO: Use a proper logger dumbass
                Console.WriteLine("Unable to find the latest artifact. Falling back to the built-in dependencies.");
                await SetTypesThroughReflection();
                return;
            }

            Console.WriteLine(artifactDownloadUrl.RootElement); // Should print the entire json object

            FileStream zipFile = File.OpenWrite("DSharpPlus Nightlies.zip");
            (await httpClient.GetStreamAsync(downloadUrl.GetString())).CopyTo(zipFile);
            zipFile.Close();

            // Begone cache, begone!
            if (Directory.Exists("DSharpPlus Nightlies"))
            {
                Directory.Delete("DSharpPlus Nightlies", true);
            }

            // TODO: This should really use the OS' temp file dir
            ZipFile.ExtractToDirectory("DSharpPlus Nightlies.zip", "DSharpPlus Nightlies");
            File.Delete("DSharpPlus Nightlies.zip");

            await LoadNightliesAsync();
            return;
        }

        private static async Task LoadNightliesAsync()
        {
            List<Type> types = new();
            foreach (string file in Directory.GetFiles("DSharpPlus Nightlies", "*.nupkg"))
            {
                string symbols = Path.Join("DSharpPlus Nightlies/", Path.GetFileNameWithoutExtension(file) + ".snupkg");
                // Open the nupkg, which is a zip file. Find the only `.dll` zip archive entry, extract and read it as a string (only StreamReader behavior).
                // Since StreamReader defaults to UTF8, we get the byte array using the UTF8 encoding class.
                string dll = Path.Join("DSharpPlus Nightlies/", Path.GetFileNameWithoutExtension(file) + ".dll");
                ZipFile.Open(file, ZipArchiveMode.Read).Entries.First(x => x.FullName.EndsWith(".dll")).ExtractToFile(dll);
                Assembly assembly = Assembly.Load(await File.ReadAllBytesAsync(dll), await File.ReadAllBytesAsync(symbols));
                types.AddRange(assembly.GetExportedTypes());
            }
            Types = types.ToArray();
            SetProperties();
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
