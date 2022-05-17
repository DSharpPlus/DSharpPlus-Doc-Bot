// This file is part of the DSharpPlus project.
//
// Copyright (c) 2015 Mike Santiago
// Copyright (c) 2016-2022 DSharpPlus Contributors
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus.Entities;
using DSharpPlusDocs.Handlers;
using DSharpPlusDocs.Query.Results;
using DSharpPlusDocs.Query.Wrappers;
using DSharpPlusDocs.Rest;

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
            string githubUrl = await GithubRest.GetEventUrlAsync(first);
            if (githubUrl != null)
            {
                eb.AddField("Source:", FormatGithubUrl(githubUrl), true);
            }

            if (result.Summary != null)
            {
                eb.AddField("Summary:", result.Summary, false);
            }

            if (result.Example != null)
            {
                eb.AddField("Example:", result.Example, false);
            }

            eb.AddField("Arguments:", BuildEvent(first), false);
            return eb;
        }

        private static string EventToDocs(EventInfoWrapper ei) => $"#{ei.Parent.TypeInfo.Namespace.Replace('.', '_')}_{ei.Parent.TypeInfo.Name}_{ei.Event.Name}";

        private static string BuildEvent(EventInfoWrapper ev)
        {
            IEnumerable<Type> par = ev.Event.EventHandlerType.GenericTypeArguments;
            par = par.Take(par.Count() - 1);
            return $"({string.Join(", ", par.Select(x => $"{Utils.BuildType(x)}"))})";
        }
    }
}
