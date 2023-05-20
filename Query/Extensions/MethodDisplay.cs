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
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
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
        private async Task<DiscordEmbedBuilder> ShowMethodsAsync(DiscordEmbedBuilder eb, IEnumerable<MethodInfoWrapper> list)
        {
            MethodInfoWrapper first = list.First();
            DocsHttpResult result;
            string pageUrl = SanitizeDocsUrl($"{first.Parent.TypeInfo.Namespace}.{first.Parent.TypeInfo.Name}");
            try
            {
                result = await GetWebDocsAsync($"{QueryHandler.DocsBaseUrl}api/{pageUrl}.html", first);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                result = new DocsHttpResult($"{QueryHandler.DocsBaseUrl}api/{pageUrl}.html{MethodToDocs(first)}");
            }
            eb.WithAuthor($"Method: {first.Parent.TypeInfo.Namespace}.{first.Parent.DisplayName}.{first.Method.Name}", result.Url, "http://i.imgur.com/yYiUhdi.png");
            eb.AddField("Docs:", FormatDocsUrl(result.Url), true);
            string githubUrl = await GithubRest.GetMethodUrlAsync(first);
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

            int i = 1;
            eb.AddField("Overloads:", string.Join("\n", list.OrderBy(y => IsInherited(y)).Select(y => $"``{i++}-``{(IsInherited(y) ? " (i)" : "")} {BuildMethod(y)}")), false);
            return eb;
        }

        private string MethodToDocs(MethodInfoWrapper mi, bool removeDiscord = false) //Always second option because the docs urls are too strange, removing the namespace one time and not another...
        {
            if (IsInherited(mi))
            {
                return "";
            }

            Regex rgx = new("[^a-zA-Z0-9_][^a-zA-Z]*");
            string parameters = "";
            foreach (ParameterInfo pi in mi.Method.GetParameters())
            {
                string format = rgx.Replace(pi.ParameterType.ToString(), "_").Replace("System_Action", "Action").Replace("System_Collections_Generic_IEnumerable", "IEnumerable");
                if (removeDiscord)
                {
                    format = format.Replace("Discord_", "");
                }

                parameters += $"{format}_";
            }
            string final = $"#{mi.Parent.TypeInfo.Namespace.Replace('.', '_')}_{mi.Parent.TypeInfo.Name}_{mi.Method.Name}_{parameters}";
            return final.Length > 68 && !removeDiscord ? MethodToDocs(mi, true) : final;
        }

        private static string BuildMethod(MethodInfoWrapper methodWrapper)
        {
            MethodInfo mi = methodWrapper.Method;
            ParameterInfo[] parametersInfo = mi.GetParameters();
            IEnumerable<string> parameters = mi.IsDefined(typeof(ExtensionAttribute)) && parametersInfo.First().ParameterType.IsAssignableFrom(methodWrapper.Parent.TypeInfo.AsType())
                ? parametersInfo.Skip(1).Select(x => $"{BuildPreParameter(x)}{Utils.BuildType(x.ParameterType)} {x.Name}{GetParameterDefaultValue(x)}")
                : parametersInfo.Select(x => $"{BuildPreParameter(x)}{Utils.BuildType(x.ParameterType)} {x.Name}{GetParameterDefaultValue(x)}");
            return $"{Utils.BuildType(mi.ReturnType)} {mi.Name}({string.Join(", ", parameters)})";
        }

        private static string BuildPreParameter(ParameterInfo pi) => pi.IsOut ? "out " : pi.ParameterType.IsByRef ? "ref " : "";

        private static string GetParameterDefaultValue(ParameterInfo pi) => pi.HasDefaultValue ? $" = {GetDefaultValueAsString(pi.DefaultValue)}" : "";

        private static string GetDefaultValueAsString(object obj) => obj switch
        {
            null => "null",
            true => "true",
            false => "false",
            _ => obj.ToString()
        };
    }
}
