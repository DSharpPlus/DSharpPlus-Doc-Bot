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
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Exceptions;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlusDocs.Controllers;
using DSharpPlusDocs.EmbedExtension;
using DSharpPlusDocs.Modules;
using DSharpPlusDocs.Paginator;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace DSharpPlusDocs.Handlers
{
    public class CommandHandler
    {
        private CommandsNextExtension _commands;
        private DiscordClient _client;
        private MainHandler _mainHandler;
        private IServiceProvider _services;
        private readonly MemoryCache _cache = new(new MemoryCacheOptions { ExpirationScanFrequency = TimeSpan.FromMinutes(3) });

        public Task InitializeAsync(MainHandler mainHandler)
        {
            _mainHandler = mainHandler;
            _client = mainHandler.Client;
            ServiceCollection services = new();
            services.AddSingleton(mainHandler);
            services.AddSingleton(new PaginationService(_client));
            _services = services.BuildServiceProvider();

            _commands = _client.UseCommandsNext(new CommandsNextConfiguration
            {
                EnableDefaultHelp = true,
                Services = _services,
                EnableMentionPrefix = false,
                PrefixResolver = (x) => HandleCommand(x)
            });

            _commands.SetHelpFormatter<HelpFormatter>();
            _commands.RegisterCommands<GeneralCommands>();

            _client.MessageUpdated += HandleUpdate;
            _commands.CommandErrored += CommandErroredAsync;

            return Task.CompletedTask;
        }

        private async Task CommandErroredAsync(CommandsNextExtension commandsNext, CommandErrorEventArgs e)
        {
            if (e.Exception is CommandNotFoundException)
            {
                (string, DiscordEmbedBuilder, PaginatedMessage) reply = await BuildReplyAsync(e.Context.Message, e.Context.Message.Content[e.Context.Message.GetMentionPrefixLength(_client.CurrentUser)..]);
                if (reply.Item1 == null && reply.Item2 == null && reply.Item3 == null)
                {
                    return;
                }

                DiscordMessage message = reply.Item3 != null
                    ? await e.Context.Services.GetService<PaginationService>().SendPaginatedMessageAsync(e.Context.Channel, reply.Item3)
                    : await e.Context.RespondAsync(reply.Item1, embed: reply.Item2);
                AddCache(e.Context.Message.Id, message.Id);
            }
            else
            {
                Console.WriteLine(e.Exception);
            }
        }

        public Task<int> HandleCommand(DiscordMessage msg)
        {
            if (!msg.Channel.IsPrivate && msg.Channel.Guild.Id == 81384788765712384)
            {
                if (msg.Channel.Name is not "dotnet_dsharpplus" and not "testing" and not "playground")
                {
                    return Task.FromResult(-1);
                }
            }

            return Task.FromResult(msg.GetMentionPrefixLength(_client.CurrentUser));
        }

        private Task HandleUpdate(DiscordClient client, MessageUpdateEventArgs e)
        {
            _ = Task.Run(async () =>
            {
                ulong? id;
                if ((id = GetOurMessageIdFromCache(e.Message.Id)) != null)
                {
                    DiscordMessage botMessage = await e.Channel.GetMessageAsync(id.Value);
                    if (botMessage == null)
                    {
                        return;
                    }

                    int argPos = 0;
                    if ((argPos = await HandleCommand(e.Message)) == -1)
                    {
                        return;
                    }

                    (string, DiscordEmbedBuilder, PaginatedMessage) reply = await BuildReplyAsync(e.Message, e.Message.Content[argPos..]);

                    if (reply.Item1 == null && reply.Item2 == null && reply.Item3 == null)
                    {
                        return;
                    }

                    PaginationService pagination = _services.GetService<PaginationService>();
                    bool isPaginatedMessage = pagination.IsPaginatedMessage(id.Value);
                    if (reply.Item3 != null)
                    {
                        if (isPaginatedMessage)
                        {
                            await pagination.UpdatePaginatedMessageAsync(botMessage, reply.Item3);
                        }
                        else
                        {
                            await pagination.EditMessageToPaginatedMessageAsync(botMessage, reply.Item3);
                        }
                    }
                    else
                    {
                        if (isPaginatedMessage)
                        {
                            pagination.StopTrackingPaginatedMessage(id.Value);
                            _ = botMessage.DeleteAllReactionsAsync(); //TODO: Should await, but D#+ is broken
                        }
                        await botMessage.ModifyAsync(reply.Item1, reply.Item2.Build());
                    }
                }
            });
            return Task.CompletedTask;
        }

        private async Task<(string, DiscordEmbedBuilder, PaginatedMessage)> BuildReplyAsync(DiscordMessage msg, string message)
        {
            if (!_mainHandler.QueryHandler.IsReady())
            {
                return ("Loading cache...", null, null); //TODO: Change message
            }
            else
            {
                try
                {
                    (string, object) tuple = await _mainHandler.QueryHandler.RunAsync(message);
                    if (tuple.Item2 is PaginatorBuilder pag)
                    {
                        PaginatedMessage paginated = new(pag.Pages, "Results", user: msg.Author, options: new AppearanceOptions { Timeout = TimeSpan.FromMinutes(10) });
                        return (null, null, paginated);
                    }
                    else
                    {
                        return (tuple.Item1, tuple.Item2 as DiscordEmbedBuilder, null);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                    return ("Uh-oh... I think some pipes have broken...", null, null);
                }
            }
        }

        public void AddCache(ulong userMessageId, ulong ourMessageId) => _cache.Set(userMessageId, ourMessageId, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) });

        public ulong? GetOurMessageIdFromCache(ulong messageId) => _cache.TryGetValue(messageId, out ulong id) ? id : null;

        /*public async Task<DiscordEmbed> HelpEmbedBuilderAsync(CommandContext context, string command = null)
        {
            DiscordEmbed eb = new DiscordEmbed();
            eb.Author = new DiscordEmbedAuthor().WithName("Help:").WithIconUrl("http://i.imgur.com/VzDRjUn.png");
            StringBuilder sb = new StringBuilder();
            if (command == null)
            {
                foreach (ModuleInfo mi in _commands.Modules.OrderBy(x => x.Name))
                    if (!mi.IsSubmodule)
                        if (mi.Name != "Help")
                        {
                            bool ok = true;
                            foreach (PreconditionAttribute precondition in mi.Preconditions)
                                if (!(await precondition.CheckPermissions(context, null, _services)).IsSuccess)
                                {
                                    ok = false;
                                    break;
                                }
                            if (ok)
                            {
                                var cmds = mi.Commands.ToList<object>();
                                cmds.AddRange(mi.Submodules);
                                for (int i = cmds.Count - 1; i >= 0; i--)
                                {
                                    object o = cmds[i];
                                    foreach (PreconditionAttribute precondition in ((o as CommandInfo)?.Preconditions ?? (o as ModuleInfo)?.Preconditions))
                                        if (!(await precondition.CheckPermissions(context, o as CommandInfo, _services)).IsSuccess)
                                            cmds.Remove(o);
                                }
                                if (cmds.Count != 0)
                                {
                                    var list = cmds.Select(x => $"{((x as CommandInfo)?.Name ?? (x as ModuleInfo)?.Name)}").OrderBy(x => x);
                                    sb.AppendLine($"**{mi.Name}:** {String.Join(", ", list)}");
                                }
                            }
                        }

                eb.AddField((x) =>
                {
                    x.IsInline = false;
                    x.Name = "Query help";
                    x.Value = $"Usage: {context.Client.CurrentUser.Mention} [query]";
                });
                eb.AddField((x) =>
                {
                    x.IsInline = true;
                    x.Name = "Keywords";
                    x.Value = "method, type, property,\nevent, in, list";
                });
                eb.AddField((x) =>
                {
                    x.IsInline = true;
                    x.Name = "Examples";
                    x.Value = "EmbedBuilder\n" +
                              "IGuildUser.Nickname\n" +
                              "ModifyAsync in IRole\n" +
                              "send message\n" +
                              "type Emote";
                });
                eb.Footer = new EmbedFooterBuilder().WithText("Note: (i) = Inherited");
                eb.Description = sb.ToString();
            }
            else
            {
                SearchResult sr = _commands.Search(context, command);
                if (sr.IsSuccess)
                {
                    Nullable<CommandMatch> cmd = null;
                    if (sr.Commands.Count == 1)
                        cmd = sr.Commands.First();
                    else
                    {
                        int lastIndex;
                        var find = sr.Commands.Where(x => x.Command.Aliases.First().Equals(command, StringComparison.OrdinalIgnoreCase));
                        if (find.Any())
                            cmd = find.First();
                        while (cmd == null && (lastIndex = command.LastIndexOf(' ')) != -1) //TODO: Maybe remove and say command not found?
                        {
                            find = sr.Commands.Where(x => x.Command.Aliases.First().Equals(command.Substring(0, lastIndex), StringComparison.OrdinalIgnoreCase));
                            if (find.Any())
                                cmd = find.First();
                            command = command.Substring(0, lastIndex);
                        }
                    }
                    if (cmd != null && (await cmd.Value.CheckPreconditionsAsync(context, _services)).IsSuccess)
                    {
                        eb.Author.Name = $"Help: {cmd.Value.Command.Aliases.First()}";
                        sb.Append($"Usage: {_mainHandler.Prefix}{cmd.Value.Command.Aliases.First()}");
                        if (cmd.Value.Command.Parameters.Count != 0)
                            sb.Append($" [{String.Join("] [", cmd.Value.Command.Parameters.Select(x => x.Name))}]");
                        if (!String.IsNullOrEmpty(cmd.Value.Command.Summary))
                            sb.Append($"\nSummary: {cmd.Value.Command.Summary}");
                        if (!String.IsNullOrEmpty(cmd.Value.Command.Remarks))
                            sb.Append($"\nRemarks: {cmd.Value.Command.Remarks}");
                        if (cmd.Value.Command.Aliases.Count != 1)
                            sb.Append($"\nAliases: {String.Join(", ", cmd.Value.Command.Aliases.Where(x => x != cmd.Value.Command.Aliases.First()))}");
                        eb.Description = sb.ToString();
                    }
                    else
                        eb.Description = $"Command '{command}' not found.";
                }
                else
                    eb.Description = $"Command '{command}' not found.";
            }
            return eb;
        }*/
    }
}
