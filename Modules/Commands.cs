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
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlusDocs.Controllers;
using DSharpPlusDocs.Handlers;
using DSharpPlusDocs.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Extensions.DependencyInjection;

#pragma warning disable CA1822 // Mark members as static

namespace DSharpPlusDocs.Modules
{
    public class GeneralCommands : BaseCommandModule
    {
        [Command("clean")]
        [Description("Delete all the messages from this bot within the last X messages")]
        public async Task CleanAsync(CommandContext ctx, int messages = 30)
        {
            if (messages > 50)
            {
                messages = 50;
            }
            else if (messages < 2)
            {
                messages = 2;
            }

            IEnumerable<DiscordMessage> msgs = await ctx.Channel.GetMessagesAsync(messages);
            msgs = msgs.Where(x => x.Author.Id == ctx.Client.CurrentUser.Id);
            foreach (DiscordMessage msg in msgs)
            {
                await msg.DeleteAsync();
            }
        }

        [Command("docs")]
        [Description("Show the docs url")]
        public async Task DocsAsync(CommandContext ctx) => await ctx.RespondAsync($"Docs: {QueryHandler.DocsBaseUrl}");

        [Command("invite")]
        [Description("Show the invite url")]
        public async Task InviteAsync(CommandContext ctx) => await ctx.RespondAsync("Invite: https://discordapp.com/oauth2/authorize?client_id=341606460720939008&scope=bot");

        [Command("guides")]
        [Aliases("guide")]
        [Description("Show the url of a guide")]
        public async Task GuidesAsync(CommandContext ctx, [RemainingText] string guide = null)
        {
            try
            {
                string html;
                using (HttpClient httpClient = new())
                {
                    HttpResponseMessage res = await httpClient.GetAsync("https://raw.githubusercontent.com/DSharpPlus/DSharpPlus/master/docs/articles/toc.yml");
                    if (!res.IsSuccessStatusCode)
                    {
                        throw new Exception($"An error occurred: {res.ReasonPhrase}");
                    }

                    html = await res.Content.ReadAsStringAsync();
                }
                Dictionary<string, string> guides = new();
                Dictionary<string, Dictionary<string, string>> subguides = new();
                string[] separate = html.Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
                string lastname = "";
                for (int i = 0; i < separate.Length; i++)
                {
                    string line = separate[i].Trim();
                    if (line.StartsWith("- name:"))
                    {
                        lastname = line.Split(new[] { "- name:" }, StringSplitOptions.None)[1].Trim();
                    }
                    else if (line.StartsWith("items:"))
                    {
                        guides[lastname] = null;
                        subguides[lastname] = new Dictionary<string, string>();
                    }
                    else if (line.StartsWith("href:"))
                    {
                        string link = line.Split(new[] { "href:" }, StringSplitOptions.None)[1].Trim();
                        if (separate[i].StartsWith("   ")) //TODO: Change how to find subgroups
                        {
                            subguides.Last().Value[lastname] = $"{link[..^2]}html";
                        }
                        else
                        {
                            guides[lastname] = $"{link[..^2]}html";
                        }
                    }
                }
                StringBuilder sb = new();
                string authorName = null, authorUrl = null;
                if (string.IsNullOrEmpty(guide))
                {
                    authorName = "Guides";
                    foreach (string category in guides.Keys)
                    {
                        if (guides[category] != null)
                        {
                            sb.AppendLine($"[**{category}**]({guides[category]})");
                        }
                        else
                        {
                            sb.AppendLine($"**{category}**");
                        }

                        if (subguides.ContainsKey(category))
                        {
                            foreach (string subcategory in subguides[category].Keys)
                            {
                                sb.AppendLine($"- [{subcategory}]({QueryHandler.DocsBaseUrl}guides/{subguides[category][subcategory]})");
                            }
                        }
                    }
                }
                else
                {
                    guide = guide.ToLower();
                    foreach (string category in guides.Keys)
                    {
                        authorName = $"Guide: {category}";
                        if (guides.TryGetValue(category, out string guide1))
                        {
                            authorUrl = guide1;
                        }

                        bool add = false;
                        if (category.IndexOf(guide, StringComparison.OrdinalIgnoreCase) != -1)
                        {
                            add = true;
                        }
                        else
                        {
                            foreach (string subcategory in subguides[category].Keys)
                            {
                                if (subcategory.IndexOf(guide, StringComparison.OrdinalIgnoreCase) != -1)
                                {
                                    add = true;
                                }
                            }
                        }

                        if (add)
                        {
                            foreach (string subcategory in subguides[category].Keys)
                            {
                                sb.AppendLine($"- [{subcategory}]({QueryHandler.DocsBaseUrl}guides/{subguides[category][subcategory]})");
                            }

                            break;
                        }
                    }
                }
                string result = sb.ToString();
                if (string.IsNullOrEmpty(result))
                {
                    await ctx.RespondAsync("No guide found.");
                }
                else
                {
                    DiscordEmbedBuilder eb = new();
                    eb.WithAuthor(authorName, authorUrl);
                    eb.WithDescription(result);
                    await ctx.RespondAsync("", embed: eb);
                }
            }
            catch (Exception e) { Console.WriteLine(e); }
        }

        [Command("info")]
        [Description("Show some information about the application")]
        public async Task InfoAsync(CommandContext ctx)
        {
            _ = ctx.Client.CurrentApplication;
            MainHandler mainHandler = ctx.Services.GetService<MainHandler>();
            DiscordEmbedBuilder eb = new();
            string name;
            if (ctx.Guild != null)
            {
                DiscordMember user = ctx.Guild.CurrentMember;
                name = user.Nickname ?? user.Username;
            }
            else
            {
                name = ctx.Client.CurrentUser.Username;
            }

            eb.WithAuthor(name, iconUrl: ctx.Client.CurrentUser.AvatarUrl);
            eb.WithThumbnail(ctx.Client.CurrentUser.AvatarUrl);
            eb.AddField(
                "Info",
                $"- Library: DSharpPlus ({ctx.Client.VersionString})\n" +
                    $"- Runtime: {RuntimeInformation.FrameworkDescription} {RuntimeInformation.ProcessArchitecture}\n" +
                    //$"- Source: https://github.com/SubZero0/DiscordNet-Docs\n" +
                    $"- Uptime: {DateTime.Now - Process.GetCurrentProcess().StartTime:dd\\.hh\\:mm\\:ss}",
                false);
            eb.AddField(
                "Docs",
                $"- Types: {mainHandler.QueryHandler.Cache.GetTypeCount()}\n" +
                    $"- Methods: {mainHandler.QueryHandler.Cache.GetMethodCount()}\n" +
                    $"- Properties: {mainHandler.QueryHandler.Cache.GetPropertyCount()}",
                true);
            eb.AddField(
                "​", // <- zero-width space here
                $"- Events: {mainHandler.QueryHandler.Cache.GetEventCount()}\n" +
                    $"- Extension types: {mainHandler.QueryHandler.Cache.GetExtensionTypesCount()}\n" +
                    $"- Extension methods: {mainHandler.QueryHandler.Cache.GetExtensioMethodsCount()}",
                true);
            await ctx.RespondAsync(eb);
        }

        [Command("eval")] //TODO: Safe eval ? 👀
        [RequireOwner]
        public async Task EvalAsync(CommandContext ctx, [RemainingText] string code)
        {
            /*using (Context.Channel.EnterTypingState())
            {*/
            try
            {
                List<MetadataReference> references = new();
                AssemblyName[] referencedAssemblies = Assembly.GetEntryAssembly().GetReferencedAssemblies();
                foreach (AssemblyName referencedAssembly in referencedAssemblies)
                {
                    references.Add(MetadataReference.CreateFromFile(Assembly.Load(referencedAssembly).Location));
                }

                ScriptOptions scriptoptions = ScriptOptions.Default.WithReferences(references);
                Globals globals = new() { Context = ctx };
                object o = await CSharpScript.EvaluateAsync(@"using System;using System.Linq;using System.Threading.Tasks;using DSharpPlus;" + @code, scriptoptions, globals);
                if (o == null)
                {
                    await ctx.RespondAsync("Done!");
                }
                else
                {
                    await ctx.RespondAsync("", embed: new DiscordEmbedBuilder().WithTitle("Result:").WithDescription(o.ToString()));
                }
            }
            catch (Exception e)
            {
                await ctx.RespondAsync("", embed: new DiscordEmbedBuilder().WithTitle("Error:").WithDescription($"{e.GetType()}: {e.Message}\nFrom: {e.Source}"));
            }
            //}
        }

        [Command("setdocsurl")]
        [RequireOwner]
        public async Task SetDocsUrlAsync(CommandContext ctx, [RemainingText] string url)
        {
            if (!url.EndsWith("/"))
            {
                url += "/";
            }

            QueryHandler.DocsBaseUrl = url;
            await ctx.RespondAsync($"Changed base docs url to: <{url}>");
        }
    }
}
