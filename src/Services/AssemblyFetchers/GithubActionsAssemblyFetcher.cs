using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using DSharpPlus.DocBot.Types;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DSharpPlus.DocBot.Services.AssemblyFetchers
{
    public sealed class GithubActionsAssemblyFetcher : DirectLinkAssemblyFetcher
    {
        private string LastVersionFile { get; init; } = Path.Join(Path.GetTempPath(), "DocBot Last Run Version.txt");
        private ulong CurrentActionsRunNumber { get; set; }
        private string? LatestUpdateUrl { get; set; }
        private ulong LatestUpdateNumber { get; set; }

        public GithubActionsAssemblyFetcher(IConfiguration configuration, ILogger<GithubActionsAssemblyFetcher> logger) : base(configuration, logger)
        {
            if (configuration.GetValue<string>("github:token") == null)
            {
                throw new ArgumentException("Github token not found in configuration.", nameof(configuration));
            }
            else if (Configuration.GetValue<string>("github:latest_action_run_url") == null && Configuration.GetValue<string>("github:repo") == null)
            {
                throw new ArgumentException("Both the latest action run url and the repo link are not found in configuration. Please provide one or the other.", nameof(configuration));
            }

            // Attempt to load the last version from the file.
            ulong result = 0;
            if (File.Exists(LastVersionFile) && !ulong.TryParse(File.ReadLines(LastVersionFile).FirstOrDefault(), NumberStyles.Number, CultureInfo.InvariantCulture, out result))
            {
                File.Delete(LastVersionFile);
            }
            CurrentActionsRunNumber = result;
        }

        public override async Task<bool> CheckForUpdateAsync()
        {
            string requestUrl = Configuration.GetValue<string>("github:repo");
            requestUrl = requestUrl == null
                ? Configuration.GetValue<string>("github:latest_action_run_url")
                : $"https://api.github.com/repos/{requestUrl}/actions/runs".Replace("//", "/");

            HttpResponseMessage latestActionRunResponse = MakeAuthenticatedRequest(requestUrl);
            if (!latestActionRunResponse.IsSuccessStatusCode)
            {
                Logger.LogError("Failed to get latest action run, HTTP response message returned non-success code: HTTP {HttpCode}: {HttpMessage}", latestActionRunResponse.StatusCode, latestActionRunResponse.ReasonPhrase);
                return false;
            }

            JsonDocument latestActionRunJson = await JsonDocument.ParseAsync(await latestActionRunResponse.Content.ReadAsStreamAsync());
            if (!latestActionRunJson.RootElement.TryGetProperty("total_count", out JsonElement totalWorkflowCount) || totalWorkflowCount.GetInt32() == 0 // If no actions are available
            || !latestActionRunJson.RootElement.TryGetProperty("workflow_runs", out JsonElement workflowRuns) || !workflowRuns[0].TryGetProperty("artifacts_url", out JsonElement artifactsUrl)) // If no artifacts are available
            {
                Logger.LogError("No workflow runs found from the Github API.");
                return false;
            }

            if (!workflowRuns[0].TryGetProperty("run_number", out JsonElement runNumberJson))
            {
                Logger.LogWarning("Unable to get run number from Github API. Assuming update available.");
            }
            else if (runNumberJson.GetUInt64() == CurrentActionsRunNumber)
            {
                Logger.LogInformation("Latest run number is the same as the current run number. No update available.");
                return false;
            }

            Logger.LogInformation("Latest run number ({NewRunNumber:N0}) differs from the current run number ({OldRunNumber:N0}). Update available.", runNumberJson.GetUInt64().ToString("N0"), CurrentActionsRunNumber.ToString("N0") ?? "None");
            LatestUpdateUrl = artifactsUrl.GetString();
            LatestUpdateNumber = runNumberJson.GetUInt64();
            return true;
        }

        public override bool TryFetch([NotNullWhen(true)] out IEnumerable<AssemblyLoadInfo>? assemblies)
        {
            assemblies = null;
            if (LatestUpdateUrl == null && (!CheckForUpdateAsync().GetAwaiter().GetResult() || LatestUpdateUrl == null))
            {
                throw new InvalidOperationException("Unable to find the latest artifact url.");
            }

            HttpResponseMessage artifactInfoResponse = MakeAuthenticatedRequest(LatestUpdateUrl);
            if (!artifactInfoResponse.IsSuccessStatusCode)
            {
                Logger.LogError("Failed to find the latest artifact url from the latest workflow run, HTTP response message returned non-success code: HTTP {HttpCode}: {HttpMessage}", artifactInfoResponse.StatusCode, artifactInfoResponse.ReasonPhrase);
                return false;
            }

            JsonDocument artifactInfoJson = JsonDocument.Parse(artifactInfoResponse.Content.ReadAsStream());
            if (!artifactInfoJson.RootElement.TryGetProperty("total_count", out JsonElement totalArtifactCount) || totalArtifactCount.GetInt32() == 0
            || !artifactInfoJson.RootElement.TryGetProperty("artifacts", out JsonElement artifacts) || !artifacts[0].TryGetProperty("archive_download_url", out JsonElement downloadUrl))
            {
                Logger.LogError("No artifacts download url found from the latest workflow run.");
                return false;
            }

            if (TryFetch(downloadUrl.GetString()!, out assemblies))
            {
                CurrentActionsRunNumber = LatestUpdateNumber;
                File.WriteAllLines(LastVersionFile, new[] { CurrentActionsRunNumber.ToString(CultureInfo.InvariantCulture) });
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
