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

using System.Threading.Tasks;
using DSharpPlusDocs.Query;
using DSharpPlusDocs.Query.Results;

namespace DSharpPlusDocs.Handlers
{
    public class QueryHandler
    {
        public Cache Cache { get; private set; }
        public static string DocsBaseUrl { get; set; } = "https://dsharpplus.github.io/DSharpPlus/";

        public QueryHandler() => Cache = new Cache();

        public void Initialize() => Cache.Initialize();

        public async Task<(string, object)> RunAsync(string text)
        {
            InterpreterResult interpreterResult = new TextInterpreter(text).Run();
            if (!interpreterResult.IsSuccess)
            {
                return ($"{interpreterResult.Error}", null);
            }

            object result = interpreterResult.Search == SearchType.JustNamespace
                ? await SearchAsync(interpreterResult, SearchType.None) ?? await SearchAsync(interpreterResult, SearchType.JustNamespace) ?? await SearchAsync(interpreterResult, SearchType.JustText) ?? await SearchAsync(interpreterResult, SearchType.All)
                : await SearchAsync(interpreterResult, SearchType.None) ?? await SearchAsync(interpreterResult, SearchType.JustText) ?? await SearchAsync(interpreterResult, SearchType.JustNamespace) ?? await SearchAsync(interpreterResult, SearchType.All);
            return result == null ? ($"No results found for `{text}`.", null) : ("", result);
        }

        private async Task<object> SearchAsync(InterpreterResult interpreterResult, SearchType type)
        {
            interpreterResult.Search = type;
            SearchResult<object> searchResult = new Search(interpreterResult, Cache).Run();
            return searchResult.Count != 0 ? await new ResultDisplay(searchResult, Cache, interpreterResult.IsList).RunAsync() : null;
        }

        public bool IsReady() => Cache.IsReady();
    }
}
