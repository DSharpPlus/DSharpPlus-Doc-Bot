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
        private async Task<DiscordEmbedBuilder> ShowPropertiesAsync(DiscordEmbedBuilder eb, IEnumerable<PropertyInfoWrapper> list)
        {
            PropertyInfoWrapper first = list.First();
            DocsHttpResult result;
            string pageUrl = SanitizeDocsUrl($"{first.Parent.TypeInfo.Namespace}.{first.Parent.TypeInfo.Name}");
            try
            {
                result = await GetWebDocsAsync($"{QueryHandler.DocsBaseUrl}api/{pageUrl}.html", first);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                result = new DocsHttpResult($"{QueryHandler.DocsBaseUrl}api/{pageUrl}.html{PropertyToDocs(first)}");
            }
            eb.WithAuthor($"Property: {first.Parent.TypeInfo.Namespace}.{first.Parent.DisplayName}.{first.Property.Name} {(IsInherited(first) ? "(i)" : "")}", result.Url, "http://i.imgur.com/yYiUhdi.png");
            eb.AddField("Docs:", FormatDocsUrl(result.Url), true);
            var githubUrl = await GithubRest.GetPropertyUrlAsync(first);
            if (githubUrl != null)
                eb.AddField("Source:", FormatGithubUrl(githubUrl), true);
            if (result.Summary != null)
                eb.AddField("Summary:", result.Summary, false);
            if (result.Example != null)
                eb.AddField("Example:", result.Example, false);
            eb.AddField("Return type:", Utils.BuildType(first.Property.PropertyType), false);
            /*eb.Fields.Add(new DiscordEmbedField
            {
                Inline = true,
                Name = "Get & Set:",
                Value = $"Can write: {first.Property.CanWrite}\nCan read: {first.Property.CanRead}"
            });*/
            return eb;
        }

        private string PropertyToDocs(PropertyInfoWrapper pi)
        {
            if (IsInherited(pi))
                return "";
            return $"#{pi.Parent.TypeInfo.Namespace.Replace('.', '_')}_{pi.Parent.TypeInfo.Name}_{pi.Property.Name}";
        }
    }
}
