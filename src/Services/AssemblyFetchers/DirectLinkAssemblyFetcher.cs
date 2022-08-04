using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using DSharpPlus.DocBot.Interfaces;
using DSharpPlus.DocBot.Types;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DSharpPlus.DocBot.Services.AssemblyFetchers
{
    public class DirectLinkAssemblyFetcher : AbstractAssemblyFetcher
    {
        private static readonly HttpClient HttpClient = new() { DefaultRequestHeaders = { { $"User-Agent", $"DocBot/3.0 (DSharpPlus/{typeof(DiscordClient).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion})" } } };

        public DirectLinkAssemblyFetcher(IConfiguration configuration, ILogger<DirectLinkAssemblyFetcher> logger) : base(configuration, logger)
        {
            if (this is not GithubActionsAssemblyFetcher && configuration.GetValue<string>("documentation:direct_download:link") == null)
            {
                throw new ArgumentException("Direct download link not found in configuration.", nameof(configuration));
            }
        }

        public override Task<bool> CheckForUpdateAsync()
        {
            string url = Configuration.GetValue<string>("documentation:direct_download:link");
            HttpResponseMessage responseMessage = MakeAuthenticatedRequest(url);
            if (responseMessage.StatusCode == HttpStatusCode.NotModified)
            {
                Logger.LogInformation("Documentation is up to date.");
                return Task.FromResult(false);
            }
            else if (LastModifiedAt.TryGetValue(url, out DateTimeOffset urlModificationTime) && responseMessage.Content.Headers.LastModified != urlModificationTime)
            {
                Logger.LogInformation("Documentation has been updated.");
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        public override bool TryFetch([NotNullWhen(true)] out IEnumerable<AssemblyLoadInfo>? assemblies) => TryFetch(Configuration.GetValue<string>("documentation:direct_download:link"), out assemblies);

        internal bool TryFetch(string url, [NotNullWhen(true)] out IEnumerable<AssemblyLoadInfo>? assemblies)
        {
            assemblies = null;
            HttpResponseMessage responseMessage = MakeAuthenticatedRequest(url, Configuration.GetSection("documentation:direct_download:headers").Get<Dictionary<string, string[]>>());
            if (!responseMessage.IsSuccessStatusCode)
            {
                Logger.LogError("Failed to download documentation, HTTP response message returned non-success code: HTTP {HttpCode}: {HttpMessage}", responseMessage.StatusCode, responseMessage.ReasonPhrase);
                return false;
            }
            else if (responseMessage.Content.Headers.ContentType == null)
            {
                Logger.LogError("Failed to download documentation, no content type header found.");
                return false;
            }

            if (Directory.Exists(CacheDirectory))
            {
                Directory.Delete(CacheDirectory, true);
            }
            Directory.CreateDirectory(CacheDirectory);

            // Zip file
            if (responseMessage.Content.Headers.ContentType.MediaType == "application/zip" || responseMessage.Content.Headers.ContentType.MediaType == "application/x-zip-compressed" || responseMessage.Content.Headers.ContentDisposition!.FileName!.EndsWith(".zip"))
            {
                assemblies = LoadLocalAssemblies(HandleZipFile(responseMessage));
                return true;
            }

            // Dll or Nupkg file
            string assemblyFile = Path.Join(CacheDirectory, responseMessage.Content.Headers.ContentDisposition!.FileName);
            FileStream assemblyFileStream = File.OpenWrite(assemblyFile);
            responseMessage.Content.CopyTo(assemblyFileStream, null, default);
            assemblyFileStream.Dispose();

            if (Path.GetExtension(assemblyFile) == ".nupkg")
            {
                assemblyFile = Path.Join(CacheDirectory, UnpackNupkg(assemblyFile));
            }

            assemblies = LoadLocalAssemblies(new[] { assemblyFile });
            return true;
        }

        internal HttpResponseMessage MakeAuthenticatedRequest(string url, IDictionary<string, string[]>? headers = null)
        {
            headers ??= new Dictionary<string, string[]>() { ["Authorization"] = new[] { $"token {Configuration.GetValue<string>("github:token")}" } };
            HttpRequestMessage httpRequest = new(HttpMethod.Get, url);
            foreach (KeyValuePair<string, string[]> header in headers)
            {
                httpRequest.Headers.Add(header.Key, header.Value);
            }
            Logger.LogTrace("Making HTTP GET request to {Url}", url);
            return HttpClient.Send(httpRequest);
        }

        private static string UnpackNupkg(string nupkgFile)
        {
            string extractDir = Path.GetDirectoryName(nupkgFile)!;
            string dllName = null!;
            using (ZipArchive archive = ZipFile.Open(nupkgFile, ZipArchiveMode.Read))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string extractFile = Path.Join(extractDir, entry.Name);
                    string entryExtension = Path.GetExtension(entry.Name);
                    switch (entryExtension)
                    {
                        case ".dll":
                            dllName = entry.Name;
                            goto case ".nupkg";
                        case ".xml" when entry.Name != "[Content_Types].xml":
                        case ".nupkg":
                            if (!File.Exists(extractFile))
                            {
                                entry.ExtractToFile(extractFile);
                            }
                            break;
                        default:
                            break;
                    }
                }
            }
            File.Delete(nupkgFile);
            return dllName;
        }

        private IEnumerable<string> HandleZipFile(HttpResponseMessage responseMessage)
        {
            if (!Directory.Exists(CacheDirectory))
            {
                Directory.CreateDirectory(CacheDirectory);
            }
            string assemblyZipFile = Path.Join(CacheDirectory, "DocBot Assemblies.zip");

            // Copy the zip file from the HTTP response to the local zip file.
            FileStream zipFile = File.OpenWrite(assemblyZipFile);
            responseMessage.Content.CopyTo(zipFile, null, default);
            zipFile.Dispose();

            // Extract the zip file to the local directory, delete the zip file, unpack the nupkgs and send the filepaths to the load local assemblies method.
            ZipFile.ExtractToDirectory(assemblyZipFile, CacheDirectory);
            File.Delete(assemblyZipFile);
            return Directory.GetFiles(CacheDirectory, "*.nupkg").Select(file => Path.Join(CacheDirectory, UnpackNupkg(file)));
        }
    }
}
