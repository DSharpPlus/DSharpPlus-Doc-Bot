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
        private async Task<DiscordEmbedBuilder> ShowEventsAsync(DiscordEmbedBuilder eb, IEnumerable<EventInfoWrapper> list)
        {
            EventInfoWrapper first = list.First();
            DocsHttpResult result;
            string pageUrl = SanitizeDocsUrl($"{first.Parent.TypeInfo.Namespace}.{first.Parent.TypeInfo.Name}");
            try
            {
                result = await GetWebDocsAsync($"{QueryHandler.DocsBaseUrl}api/{pageUrl}.html", first);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                result = new DocsHttpResult($"{QueryHandler.DocsBaseUrl}api/{pageUrl}.html{EventToDocs(first)}");
            }
            eb.WithAuthor($"Event: {first.Parent.TypeInfo.Namespace}.{first.Parent.DisplayName}.{first.Event.Name}", result.Url, "http://i.imgur.com/yYiUhdi.png");
            eb.AddField("Docs:", FormatDocsUrl(result.Url), true);
            var githubUrl = await GithubRest.GetEventUrlAsync(first);
            if (githubUrl != null)
                eb.AddField("Source:", FormatGithubUrl(githubUrl), true);
            if (result.Summary != null)
                eb.AddField("Summary:", result.Summary, false);
            if (result.Example != null)
                eb.AddField("Example:", result.Example, false);
            eb.AddField("Arguments:", BuildEvent(first), false);
            return eb;
        }

        private string EventToDocs(EventInfoWrapper ei)
        {
            return $"#{ei.Parent.TypeInfo.Namespace.Replace('.', '_')}_{ei.Parent.TypeInfo.Name}_{ei.Event.Name}";
        }

        private string BuildEvent(EventInfoWrapper ev)
        {
            IEnumerable<Type> par = ev.Event.EventHandlerType.GenericTypeArguments;
            par = par.Take(par.Count() - 1);
            return $"({String.Join(", ", par.Select(x => $"{Utils.BuildType(x)}"))})";
        }
    }
}
