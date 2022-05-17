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
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DSharpPlusDocs.Handlers;
using DSharpPlusDocs.Query.Results;
using DSharpPlusDocs.Query.Wrappers;

namespace DSharpPlusDocs.Query
{
    public partial class ResultDisplay
    {
        private static async Task<DocsHttpResult> GetWebDocsAsync(string url, object o)
        {
            string summary = null, example = null;
            string search = GetDocsUrlPath(o);
            (bool, string) result = await GetWebDocsHtmlAsync(url, o);
            string html = result.Item2;
            if (result.Item1 && !string.IsNullOrEmpty(html))
            {
                string block = (o is TypeInfoWrapper) ? html[html.IndexOf($"<h1 id=\"{search}")..] : html[html.IndexOf($"<h4 id=\"{search}")..];
                string anchor = block[(block.IndexOf('"') + 1)..];
                anchor = anchor[..anchor.IndexOf('"')];
                summary = block[(block.IndexOf("summary\">") + 9)..];
                summary = summary[..summary.IndexOf("</div>")];
                summary = WebUtility.HtmlDecode(StripTags(summary));
                /*string example = block.Substring(block.IndexOf("example\">")); //TODO: Find this
                summary = summary.Substring(0, summary.IndexOf("</div>"));*/
                if (o is not TypeInfoWrapper && !IsInherited(o))
                {
                    url += $"#{anchor}";
                }
            }
            return new DocsHttpResult(url, summary, example);
        }

        private static async Task<(bool, string)> GetWebDocsHtmlAsync(string url, object o)
        {
            string html;
            if (IsInherited(o))
            {
                if (o is MethodInfoWrapper mi)
                {
                    if (!mi.Method.DeclaringType.Namespace.StartsWith("DSharpPlus"))
                    {
                        return (true, "");
                    }
                    else
                    {
                        url = $"{QueryHandler.DocsBaseUrl}api/{SanitizeDocsUrl($"{mi.Method.DeclaringType.Namespace}.{mi.Method.DeclaringType.Name}")}.html";
                    }
                }
                else
                {
                    PropertyInfoWrapper pi = (PropertyInfoWrapper)o;
                    if (!pi.Property.DeclaringType.Namespace.StartsWith("DSharpPlus"))
                    {
                        return (true, "");
                    }
                    else
                    {
                        url = $"{QueryHandler.DocsBaseUrl}api/{SanitizeDocsUrl($"{pi.Property.DeclaringType.Namespace}.{pi.Property.DeclaringType.Name}")}.html";
                    }
                }
            }
            using (HttpClient httpClient = new()
            { Timeout = TimeSpan.FromSeconds(6) })
            {
                HttpResponseMessage res = await httpClient.GetAsync(url);
                if (!res.IsSuccessStatusCode)
                {
                    if (res.StatusCode != HttpStatusCode.NotFound)
                    {
                        //throw new Exception("Not possible to connect to the docs page");
                        return (false, "Not possible to connect to the docs page");
                    }
                    else
                    {
                        return (false, "Docs page not found");
                    }
                }
                html = await res.Content.ReadAsStringAsync();
            }
            return (true, html);
        }

        private static string GetDocsUrlPath(object o)
        {
            bool useParent = !IsInherited(o);
            Regex rgx = new("\\W+");
            return o is TypeInfoWrapper type
                ? rgx.Replace($"{type.TypeInfo.Namespace}_{type.TypeInfo.Name}", "_")
                : o is MethodInfoWrapper method
                ? rgx.Replace($"{(useParent ? method.Parent.TypeInfo.Namespace : method.Method.DeclaringType.Namespace)}_{(useParent ? method.Parent.TypeInfo.Name : method.Method.DeclaringType.Name)}_{method.Method.Name}", "_")
                : o is PropertyInfoWrapper property
                ? rgx.Replace($"{(useParent ? property.Parent.TypeInfo.Namespace : property.Property.DeclaringType.Namespace)}_{(useParent ? property.Parent.TypeInfo.Name : property.Property.DeclaringType.Name)}_{property.Property.Name}", "_")
                : o is EventInfoWrapper eve
                ? rgx.Replace($"{eve.Parent.TypeInfo.Namespace}_{eve.Parent.TypeInfo.Name}_{eve.Event.Name}".Replace('.', '_'), "_")
                : rgx.Replace($"{o.GetType().Namespace}_{o.GetType().Name}".Replace('.', '_'), "_");
        }

        //Generic types will return like Type`1 and the docs change to Type-1
        private static string SanitizeDocsUrl(string text) => text.Replace('`', '-');

        public static bool IsInherited(object o) => o is PropertyInfoWrapper property
                ? $"{property.Parent.TypeInfo.Namespace}.{property.Parent.TypeInfo.Name}" != $"{property.Property.DeclaringType.Namespace}.{property.Property.DeclaringType.Name}"
                : o is MethodInfoWrapper method
                && $"{method.Parent.TypeInfo.Namespace}.{method.Parent.TypeInfo.Name}" != $"{method.Method.DeclaringType.Namespace}.{method.Method.DeclaringType.Name}";

        private static List<string> GetPaths(IEnumerable<object> list) => list.Select(x => GetPath(x)).ToList();

        public static string GetPath(object o, bool withInheritanceMarkup = true)
        {
            if (o is TypeInfoWrapper typeWrapper)
            {
                string type = "Type";
                if (typeWrapper.TypeInfo.IsInterface)
                {
                    type = "Interface";
                }
                else if (typeWrapper.TypeInfo.IsEnum)
                {
                    type = "Enum";
                }

                return $"{type}: {typeWrapper.DisplayName} in {typeWrapper.TypeInfo.Namespace}";
            }
            return o is MethodInfoWrapper method
                ? $"Method: {method.Method.Name} in {method.Parent.TypeInfo.Namespace}.{method.Parent.DisplayName}{(global::DSharpPlusDocs.Query.ResultDisplay.IsInherited(method) && withInheritanceMarkup ? " (i)" : "")}"
                : o is PropertyInfoWrapper property
                ? $"Property: {property.Property.Name} in {property.Parent.TypeInfo.Namespace}.{property.Parent.DisplayName}{(global::DSharpPlusDocs.Query.ResultDisplay.IsInherited(property) && withInheritanceMarkup ? " (i)" : "")}"
                : o is EventInfoWrapper eve
                ? $"Event: {eve.Event.Name} in {eve.Parent.TypeInfo.Namespace}.{eve.Parent.DisplayName}"
                : o.GetType().ToString();
        }

        public static string GetSimplePath(object o)
        {
            if (o is TypeInfoWrapper typeWrapper)
            {
                string type = "Type";
                if (typeWrapper.TypeInfo.IsInterface)
                {
                    type = "Interface";
                }
                else if (typeWrapper.TypeInfo.IsEnum)
                {
                    type = "Enum";
                }

                return $"{type}:{typeWrapper.DisplayName}";
            }
            return o is MethodInfoWrapper method
                ? $"Method:{method.Method.Name}"
                : o is PropertyInfoWrapper property
                ? $"Property:{property.Property.Name}"
                : o is EventInfoWrapper eve ? $"Event:{eve.Event.Name}" : o.GetType().ToString();
        }

        public static string GetNamespace(object o) => o is TypeInfoWrapper typeWrapper
                ? typeWrapper.TypeInfo.Namespace
                : o is MethodInfoWrapper method
                ? $"{method.Parent.TypeInfo.Namespace}.{method.Parent.DisplayName}"
                : o is PropertyInfoWrapper property
                ? $"{property.Parent.TypeInfo.Namespace}.{property.Parent.DisplayName}"
                : o is EventInfoWrapper eve ? $"{eve.Parent.TypeInfo.Namespace}.{eve.Parent.DisplayName}" : o.GetType().Namespace;

        public static string GetParent(object o) => o is TypeInfoWrapper typeWrapper
                ? typeWrapper.DisplayName
                : o is MethodInfoWrapper method
                ? method.Parent.DisplayName
                : o is PropertyInfoWrapper property
                ? property.Parent.DisplayName
                : o is EventInfoWrapper eve ? eve.Parent.DisplayName : o.GetType().Name;

        private static string StripTags(string source)
        {
            char[] array = new char[source.Length];
            int arrayIndex = 0;
            bool inside = false;

            for (int i = 0; i < source.Length; i++)
            {
                char let = source[i];
                if (let == '<')
                {
                    inside = true;
                    continue;
                }
                if (let == '>')
                {
                    inside = false;
                    continue;
                }
                if (!inside)
                {
                    array[arrayIndex] = let;
                    arrayIndex++;
                }
            }
            return new string(array, 0, arrayIndex);
        }

        private static string FormatGithubUrl(string url) => $"[{url[(url.LastIndexOf('/') + 1)..]}]({url})";

        private static string FormatDocsUrl(string url)
        {
            int idx = url.IndexOf('#');
            string[] arr = idx == -1 ? url[..].Split('.') : url[..idx].Split('.');
            return $"[{arr.ElementAt(arr.Length - 2)}.html]({url})";
        }
    }
}
