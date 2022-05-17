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
using System.Net.Http;
using System.Threading.Tasks;
using DSharpPlusDocs.Query.Wrappers;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Newtonsoft.Json.Linq;

namespace DSharpPlusDocs.Rest
{
    public class GithubRest
    {
        private const string ApiUrl = "https://api.github.com";
        private const string AcceptHeader = "application/vnd.github.v3+json";

        private static async Task<JObject> SendRequestAsync(HttpMethod method, string endpoint, string extra = null)
        {
            using HttpClient http = new();
            HttpRequestMessage request = new(method, $"{ApiUrl}{endpoint}{extra}");
            request.Headers.Add("Accept", AcceptHeader);
            request.Headers.Add("User-Agent", "DSharpPlus Docs Bot/1.0");
            HttpResponseMessage response = await http.SendAsync(request);
            return response.IsSuccessStatusCode
                ? JObject.Parse(await response.Content.ReadAsStringAsync())
                : throw new InvalidOperationException($"{response.ReasonPhrase}: {await response.Content.ReadAsStringAsync()}");
        }

        public static async Task<List<GitSearchResult>> SearchAsync(string search, string filename = null)
        {
            string extra = $"?q=repo:DSharpPlus/DSharpPlus+language:cs+in:file{(filename == null ? "" : $"+filename:{filename}")}+{search.Replace(' ', '+')}&per_page=100";
            JObject result = await SendRequestAsync(HttpMethod.Get, "/search/code", extra);
            JArray items = (JArray)result["items"];
            List<GitSearchResult> list = new();
            foreach (JToken item in items)
            {
                list.Add(new GitSearchResult { Name = (string)item["name"], HtmlUrl = (string)item["html_url"] });
            }

            int totalCount = (int)result["total_count"];
            if (totalCount > 100)
            {
                int pages = (int)Math.Floor(totalCount / 100f);
                for (int i = 2; i <= pages + 1; i++)
                {
                    extra = $"?q=repo:DSharpPlus/DSharpPlus+language:cs+in:file{(filename == null ? "" : $"+filename:{filename}")}+{search.Replace(' ', '+')}&per_page=100&page={i}";
                    result = await SendRequestAsync(HttpMethod.Get, "/search/code", extra);
                    items = (JArray)result["items"];
                    foreach (JToken item in items)
                    {
                        list.Add(new GitSearchResult { Name = (string)item["name"], HtmlUrl = (string)item["html_url"] });
                    }
                }
            }
            return list;
        }

        public static async Task<string> GetTypeUrlAsync(TypeInfoWrapper type)
        {
            List<GitSearchResult> search = await SearchAsync(type.Name, $"{type.Name}.cs");
            return search.FirstOrDefault(x => x.Name == $"{type.Name}.cs")?.HtmlUrl ?? search.FirstOrDefault()?.HtmlUrl ?? null; //null = Not found
        }

        public static async Task<string> GetEventUrlAsync(EventInfoWrapper ev)
        {
            List<GitSearchResult> search = await SearchAsync(ev.Parent.Name, $"{ev.Parent.Name}.cs");
            string result = search.FirstOrDefault(x => x.Name == $"{ev.Parent.Name}.cs")?.HtmlUrl ?? search.FirstOrDefault()?.HtmlUrl;
            if (result != null)
            {
                using HttpClient client = new();
                string url = result.Replace("/blob/", "/raw/");
                string code = await client.GetStringAsync(url);
                Microsoft.CodeAnalysis.SyntaxTree tree = CSharpSyntaxTree.ParseText(code);
                CompilationUnitSyntax root = (CompilationUnitSyntax)tree.GetRoot();
                EventDeclarationSyntax source = root.DescendantNodes().OfType<EventDeclarationSyntax>().FirstOrDefault(x => x.Identifier.ValueText == ev.Event.Name);
                if (source == null)
                {
                    return result;
                }

                int startLine = source.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                int endLine = source.GetLocation().GetLineSpan().EndLinePosition.Line + 1;
                return $"{result}{(startLine == endLine ? $"#L{startLine}" : $"#L{startLine}-L{endLine}")}";
            }
            return result ?? await GetTypeUrlAsync(ev.Parent);
        }

        public static async Task<string> GetMethodUrlAsync(MethodInfoWrapper method)
        {
            List<GitSearchResult> search = await SearchAsync(method.Method.Name, $"{method.Parent.Name}.cs");
            string result = search.FirstOrDefault(x => x.Name == $"{method.Parent.Name}.cs")?.HtmlUrl ?? search.FirstOrDefault()?.HtmlUrl;
            if (result != null)
            {
                using HttpClient client = new();
                string url = result.Replace("/blob/", "/raw/");
                string code = await client.GetStringAsync(url);
                Microsoft.CodeAnalysis.SyntaxTree tree = CSharpSyntaxTree.ParseText(code);
                CompilationUnitSyntax root = (CompilationUnitSyntax)tree.GetRoot();
                MethodDeclarationSyntax source = root.DescendantNodes().OfType<MethodDeclarationSyntax>().FirstOrDefault(x => x.Identifier.ValueText == method.Method.Name);
                if (source == null)
                {
                    return result;
                }

                int startLine = source.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                int endLine = source.GetLocation().GetLineSpan().EndLinePosition.Line + 1;
                return $"{result}{(startLine == endLine ? $"#L{startLine}" : $"#L{startLine}-L{endLine}")}";
            }
            return await GetTypeUrlAsync(method.Parent);
        }

        public static async Task<string> GetPropertyUrlAsync(PropertyInfoWrapper property)
        {
            List<GitSearchResult> search = await SearchAsync(property.Property.Name, $"{property.Parent.Name}.cs");
            string result = search.FirstOrDefault(x => x.Name == $"{property.Parent.Name}.cs")?.HtmlUrl ?? search.FirstOrDefault()?.HtmlUrl;
            if (result != null)
            {
                using HttpClient client = new();
                string url = result.Replace("/blob/", "/raw/");
                string code = await client.GetStringAsync(url);
                Microsoft.CodeAnalysis.SyntaxTree tree = CSharpSyntaxTree.ParseText(code);
                CompilationUnitSyntax root = (CompilationUnitSyntax)tree.GetRoot();
                PropertyDeclarationSyntax source = root.DescendantNodes().OfType<PropertyDeclarationSyntax>().FirstOrDefault(x => x.Identifier.ValueText == property.Property.Name);
                if (source == null)
                {
                    return result;
                }

                int startLine = source.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                int endLine = source.GetLocation().GetLineSpan().EndLinePosition.Line + 1;
                return $"{result}{(startLine == endLine ? $"#L{startLine}" : $"#L{startLine}-L{endLine}")}";
            }
            return await GetTypeUrlAsync(property.Parent);
        }
    }

    public class GitSearchResult
    {
        public string Name { get; protected internal set; }
        public string HtmlUrl { get; protected internal set; }

        public override string ToString() => $"{Name}: {HtmlUrl}";
    }
}
