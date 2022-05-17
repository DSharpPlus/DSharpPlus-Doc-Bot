using DSharpPlus.Entities;
using DSharpPlusDocs.Github;
using DSharpPlusDocs.Handlers;
using DSharpPlusDocs.Query.Results;
using DSharpPlusDocs.Query.Wrappers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DSharpPlusDocs.Query
{
    public partial class ResultDisplay
    {
        private async Task<DiscordEmbedBuilder> ShowTypesAsync(DiscordEmbedBuilder eb, IEnumerable<TypeInfoWrapper> list)
        {
            TypeInfoWrapper first = list.First();
            DocsHttpResult result;
            string pageUrl = SanitizeDocsUrl($"{first.TypeInfo.Namespace}.{first.TypeInfo.Name}");
            try
            {
                result = await GetWebDocsAsync($"{QueryHandler.DocsBaseUrl}api/{pageUrl}.html", first);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                result = new DocsHttpResult($"{QueryHandler.DocsBaseUrl}api/{pageUrl}.html");
            }
            eb.WithAuthor($"{(first.TypeInfo.IsInterface ? "Interface" : (first.TypeInfo.IsEnum ? "Enum" : "Type"))}: {first.TypeInfo.Namespace}.{first.DisplayName}", result.Url, "http://i.imgur.com/yYiUhdi.png");
            eb.AddField("Docs:", FormatDocsUrl(result.Url), true);
            var githubUrl = await GithubRest.GetTypeUrlAsync(first);
            if (githubUrl != null)
                eb.AddField("Source:", FormatGithubUrl(githubUrl), true);
            if (result.Summary != null)
                eb.AddField("Summary:", result.Summary, false);
            if (result.Example != null)
                eb.AddField("Example:", result.Example, false);
            CacheBag cb = _cache.GetCacheBag(first);
            if (cb.Methods.Count != 0)
            {
                int i = 1;
                var methods = cb.Methods.RandomShuffle().Take(3);
                eb.AddField(
                    $"Some methods ({methods.Count()}/{cb.Methods.Count}):",
                    String.Join("\n", methods.Select(y => $"``{i++}-``{(IsInherited(new MethodInfoWrapper(first, y)) ? " (i)" : "")} {y.Name}(...)")),
                    true);
            }
            if (cb.Properties.Count != 0)
            {
                int i = 1;
                var properties = cb.Properties.RandomShuffle().Take(3);
                eb.AddField(
                    $"Some properties ({properties.Count()}/{cb.Properties.Count}):",
                    String.Join("\n", properties.Select(y => $"``{i++}-``{(IsInherited(new PropertyInfoWrapper(first, y)) ? " (i)" : "")} {y.Name}")),
                    true);
            }
            if (first.TypeInfo.IsEnum)
            {
                var enumValues = first.TypeInfo.GetEnumNames();
                int i = 1;
                var fields = enumValues.RandomShuffle().Take(3);
                eb.AddField(
                    $"Some fields ({fields.Count()}/{enumValues.Length}):",
                    String.Join("\n", fields.Select(y => $"``{i++}-`` {y}")),
                    true);
            }
            return eb;
        }
    }
}
