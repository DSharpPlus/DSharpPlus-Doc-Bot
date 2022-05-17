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
            string githubUrl = await GithubRest.GetTypeUrlAsync(first);
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

            CacheBag cb = _cache.GetCacheBag(first);
            if (!cb.Methods.IsEmpty)
            {
                int i = 1;
                IEnumerable<System.Reflection.MethodInfo> methods = cb.Methods.RandomShuffle().Take(3);
                eb.AddField(
                    $"Some methods ({methods.Count()}/{cb.Methods.Count}):",
                    string.Join("\n", methods.Select(y => $"``{i++}-``{(IsInherited(new MethodInfoWrapper(first, y)) ? " (i)" : "")} {y.Name}(...)")),
                    true);
            }
            if (!cb.Properties.IsEmpty)
            {
                int i = 1;
                IEnumerable<System.Reflection.PropertyInfo> properties = cb.Properties.RandomShuffle().Take(3);
                eb.AddField(
                    $"Some properties ({properties.Count()}/{cb.Properties.Count}):",
                    string.Join("\n", properties.Select(y => $"``{i++}-``{(IsInherited(new PropertyInfoWrapper(first, y)) ? " (i)" : "")} {y.Name}")),
                    true);
            }
            if (first.TypeInfo.IsEnum)
            {
                string[] enumValues = first.TypeInfo.GetEnumNames();
                int i = 1;
                IEnumerable<string> fields = enumValues.RandomShuffle().Take(3);
                eb.AddField(
                    $"Some fields ({fields.Count()}/{enumValues.Length}):",
                    string.Join("\n", fields.Select(y => $"``{i++}-`` {y}")),
                    true);
            }
            return eb;
        }
    }
}
