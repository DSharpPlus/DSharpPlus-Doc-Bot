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
using DSharpPlusDocs.EmbedExtension;
using DSharpPlusDocs.Query.Results;
using DSharpPlusDocs.Query.Wrappers;

namespace DSharpPlusDocs.Query
{
    public partial class ResultDisplay
    {
        private readonly SearchResult<object> _result;
        private readonly Cache _cache;
        private readonly bool _isList;
        public ResultDisplay(SearchResult<object> result, Cache cache, bool isList)
        {
            _result = result;
            _cache = cache;
            _isList = isList;
        }

        public async Task<object> RunAsync()
        {
            IEnumerable<IGrouping<string, object>> list = _result.List.GroupBy(x => GetPath(x, false));
            return _isList ? ShowList(list) : list.Count() == 1 ? await ShowAsync(list.First()) : (object)await ShowMultipleAsync(list);
        }

        private async Task<DiscordEmbedBuilder> ShowAsync(IEnumerable<object> o)
        {
            object first = o.First();
            DiscordEmbedBuilder eb = first switch
            {
                TypeInfoWrapper => await ShowTypesAsync(new(), o.Select(x => (TypeInfoWrapper)x)),
                MethodInfoWrapper => await ShowMethodsAsync(new(), o.Select(x => (MethodInfoWrapper)x)),
                PropertyInfoWrapper => await ShowPropertiesAsync(new(), o.Select(x => (PropertyInfoWrapper)x)),
                EventInfoWrapper => await ShowEventsAsync(new(), o.Select(x => (EventInfoWrapper)x)),
                _ => throw new NotImplementedException()
            };

            return eb;
        }

        private async Task<DiscordEmbedBuilder> ShowMultipleAsync(IEnumerable<IEnumerable<object>> obj)
        {
            DiscordEmbedBuilder eb;
            IEnumerable<object> singleList = obj.Select(x => x.First());
            IEnumerable<IGrouping<string, object>> same = singleList.GroupBy(x => GetSimplePath(x));
            if (same.Count() == 1)
            {
                eb = await ShowAsync(obj.First());
                eb.Author.Name = $"(Most likely) {eb.Author.Name}";
                IEnumerable<object> list = singleList.Skip(1).RandomShuffle().Take(6);
                int max = (int)Math.Ceiling(list.Count() / 3.0);
                for (int i = 0; i < max; i++)
                {
                    eb.AddField(
                        i == 0 ? $"Also found in ({list.Count()}/{singleList.Count() - 1}):" : "​",
                        string.Join("\n", list.Skip(3 * i).Take(3).Select(y => GetParent(y))),
                        true);
                }
            }
            else
            {
                /*if (singleList.Count() > 10)
                {
                    eb.Title = $"Too many results, try filtering your search. Some results (10/{obj.Count()}):";
                    eb.Description = string.Join("\n", GetPaths(singleList.Take(10)));
                }
                else
                {
                    eb.Title = "Did you mean:";
                    eb.Description = string.Join("\n", GetPaths(singleList));
                }*/
                eb = await ShowAsync(obj.First());
                eb.Author.Name = $"(First) {eb.Author.Name}";
                IEnumerable<object> list = singleList.Skip(1).RandomShuffle().Take(3);
                eb.AddField(
                    $"Other results ({list.Count()}/{singleList.Count() - 1}):",
                    string.Join("\n", GetPaths(list)),
                     false);
            }
            eb.WithFooter("Type help to see keywords to filter your query.");
            return eb;
        }

        private PaginatorBuilder ShowList(IEnumerable<IEnumerable<object>> obj)
        {
            PaginatorBuilder eb = new();
            IEnumerable<object> singleList = obj.Select(x => x.First());
            int size = 10;
            int pages = (int)Math.Ceiling(singleList.Count() / (float)size);
            eb.Pages = ToPages(singleList, pages, size);
            return eb;
        }

        private IEnumerable<string> ToPages(IEnumerable<object> list, int pages, int size)
        {
            for (int i = 0; i < pages; i++)
            {
                yield return string.Join("\n", GetPaths(list.Skip(i * size).Take(size)));
            }
        }
    }
}
