using DSharpPlus.Entities;
using DSharpPlusDocs.Github;
using DSharpPlusDocs.Handlers;
using DSharpPlusDocs.Query.Results;
using DSharpPlusDocs.Query.Wrappers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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
            var githubUrl = await GithubRest.GetMethodUrlAsync(first);
            if (githubUrl != null)
                eb.AddField("Source:", FormatGithubUrl(githubUrl), true);
            if (result.Summary != null)
                eb.AddField("Summary:", result.Summary, false);
            if (result.Example != null)
                eb.AddField("Example:", result.Example, false);
            int i = 1;
            eb.AddField("Overloads:", String.Join("\n", list.OrderBy(y => IsInherited(y)).Select(y => $"``{i++}-``{(IsInherited(y) ? " (i)" : "")} {BuildMethod(y)}")), false);
            return eb;
        }

        private string MethodToDocs(MethodInfoWrapper mi, bool removeDiscord = false) //Always second option because the docs urls are too strange, removing the namespace one time and not another...
        {
            if (IsInherited(mi))
                return "";
            Regex rgx = new Regex("[^a-zA-Z0-9_][^a-zA-Z]*");
            string parameters = "";
            string parameters_orig = "";
            foreach (ParameterInfo pi in mi.Method.GetParameters())
            {
                string format = rgx.Replace(pi.ParameterType.ToString(), "_").Replace("System_Action", "Action").Replace("System_Collections_Generic_IEnumerable", "IEnumerable");
                if (removeDiscord)
                    format = format.Replace("Discord_", "");
                parameters += $"{format}_";
                parameters_orig += $"{pi.ParameterType.ToString()}_";
            }
            string final = $"#{mi.Parent.TypeInfo.Namespace.Replace('.', '_')}_{mi.Parent.TypeInfo.Name}_{mi.Method.Name}_{parameters}";
            if (final.Length > 68 && !removeDiscord) //This isnt how they select if they should remove the namespace...
                return MethodToDocs(mi, true);
            return final;
        }

        private string BuildMethod(MethodInfoWrapper methodWrapper)
        {
            var mi = methodWrapper.Method;
            IEnumerable<string> parameters = null;
            var parametersInfo = mi.GetParameters();
            if (mi.IsDefined(typeof(ExtensionAttribute)) && parametersInfo.First().ParameterType.IsAssignableFrom(methodWrapper.Parent.TypeInfo.AsType()))
                parameters = parametersInfo.Skip(1).Select(x => $"{BuildPreParameter(x)}{Utils.BuildType(x.ParameterType)} {x.Name}{GetParameterDefaultValue(x)}");
            else
                parameters = parametersInfo.Select(x => $"{BuildPreParameter(x)}{Utils.BuildType(x.ParameterType)} {x.Name}{GetParameterDefaultValue(x)}");
            return $"{Utils.BuildType(mi.ReturnType)} {mi.Name}({String.Join(", ", parameters)})";
        }

        private string BuildPreParameter(ParameterInfo pi)
        {
            if (pi.IsOut)
                return "out ";
            if (pi.ParameterType.IsByRef)
                return "ref ";
            return "";
        }

        private string GetParameterDefaultValue(ParameterInfo pi)
        {
            if (pi.HasDefaultValue)
                return $" = {GetDefaultValueAsString(pi.DefaultValue)}";
            return "";
        }

        private string GetDefaultValueAsString(object obj)
        {
            if (obj == null)
                return "null";
            switch (obj)
            {
                case false: return "false";
                case true: return "true";
                default: return obj.ToString();
            }
        }
    }
}
