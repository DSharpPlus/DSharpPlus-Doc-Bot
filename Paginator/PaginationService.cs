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
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;

namespace DSharpPlusDocs.Paginator
{
    public class PaginationService
    {
        private readonly Dictionary<ulong, PaginatedMessage> _messages;
        private readonly DiscordClient _client;

        public PaginationService(DiscordClient client)
        {
            _messages = new Dictionary<ulong, PaginatedMessage>();
            _client = client;
            _client.MessageReactionAdded += OnReactionAddedAsync;
        }

        /// <summary>
        /// Sends a paginated message (with reaction buttons)
        /// </summary>
        /// <param name="channel">The channel this message should be sent to</param>
        /// <param name="paginated">A <see cref="PaginatedMessage">PaginatedMessage</see> containing the pages.</param>
        /// <exception cref="Net.HttpException">Thrown if the bot user cannot send a message or add reactions.</exception>
        /// <returns>The paginated message.</returns>
        public async Task<DiscordMessage> SendPaginatedMessageAsync(DiscordChannel channel, PaginatedMessage paginated)
        {
            DiscordMessage message = await channel.SendMessageAsync("", embed: paginated.GetEmbed());

            if (paginated.Count == 1)
            {
                return message;
            }

            await message.CreateReactionAsync(paginated.Options.EmoteFirst);
            await message.CreateReactionAsync(paginated.Options.EmoteBack);
            await message.CreateReactionAsync(paginated.Options.EmoteNext);
            await message.CreateReactionAsync(paginated.Options.EmoteLast);
            await message.CreateReactionAsync(paginated.Options.EmoteStop);

            _messages.Add(message.Id, paginated);

            if (paginated.Options.Timeout != TimeSpan.Zero)
            {
                _ = Task.Delay(paginated.Options.Timeout).ContinueWith(async _ =>
                {
                    if (!_messages.ContainsKey(message.Id))
                    {
                        return;
                    }

                    _messages.Remove(message.Id);
                    if (paginated.Options.TimeoutAction == StopAction.DeleteMessage)
                    {
                        await message.DeleteAsync();
                    }
                    else if (paginated.Options.TimeoutAction == StopAction.ClearReactions)
                    {
                        await message.DeleteAllReactionsAsync();
                    }
                });
            }

            return message;
        }

        public bool IsPaginatedMessage(ulong id) => _messages.ContainsKey(id);

        public async Task UpdatePaginatedMessageAsync(DiscordMessage message, PaginatedMessage page)
        {
            if (_messages.ContainsKey(message.Id))
            {
                _messages[message.Id] = page;
                await message.ModifyAsync(embed: page.GetEmbed());
            }
        }

        public async Task EditMessageToPaginatedMessageAsync(DiscordMessage message, PaginatedMessage paginated)
        {
            if (_messages.ContainsKey(message.Id))
            {
                return;
            }

            await message.ModifyAsync(embed: paginated.GetEmbed());
            await message.CreateReactionAsync(paginated.Options.EmoteFirst);
            await message.CreateReactionAsync(paginated.Options.EmoteBack);
            await message.CreateReactionAsync(paginated.Options.EmoteNext);
            await message.CreateReactionAsync(paginated.Options.EmoteLast);
            await message.CreateReactionAsync(paginated.Options.EmoteStop);

            _messages.Add(message.Id, paginated);

            if (paginated.Options.Timeout != TimeSpan.Zero)
            {
                _ = Task.Delay(paginated.Options.Timeout).ContinueWith(async _ =>
                {
                    if (!_messages.ContainsKey(message.Id))
                    {
                        return;
                    }

                    _messages.Remove(message.Id);
                    if (paginated.Options.TimeoutAction == StopAction.DeleteMessage)
                    {
                        await message.DeleteAsync();
                    }
                    else if (paginated.Options.TimeoutAction == StopAction.ClearReactions)
                    {
                        await message.DeleteAllReactionsAsync();
                    }
                });
            }
        }

        public void StopTrackingPaginatedMessage(ulong id)
        {
            if (_messages.ContainsKey(id))
            {
                _messages.Remove(id);
            }
        }

        internal async Task OnReactionAddedAsync(DiscordClient client, MessageReactionAddEventArgs e)
        {
            DiscordMessage message = e.Message;
            if (message == null)
            {
                return;
            }

            if (_messages.TryGetValue(message.Id, out PaginatedMessage page))
            {
                if (e.User.Id == _client.CurrentUser.Id)
                {
                    return;
                }

                if (page.User != null && e.User.Id != page.User.Id)
                {
                    _ = message.DeleteReactionAsync(e.Emoji, e.User);
                    return;
                }

                if (e.Emoji.Equals(page.Options.EmoteFirst))
                {
                    if (page.CurrentPage != 1)
                    {
                        page.CurrentPage = 1;
                        await message.ModifyAsync(embed: page.GetEmbed());
                    }
                }
                else if (e.Emoji.Equals(page.Options.EmoteBack))
                {
                    if (page.CurrentPage != 1)
                    {
                        page.CurrentPage--;
                        await message.ModifyAsync(embed: page.GetEmbed());
                    }
                }
                else if (e.Emoji.Equals(page.Options.EmoteNext))
                {
                    if (page.CurrentPage != page.Count)
                    {
                        page.CurrentPage++;
                        await message.ModifyAsync(embed: page.GetEmbed());
                    }
                }
                else if (e.Emoji.Equals(page.Options.EmoteLast))
                {
                    if (page.CurrentPage != page.Count)
                    {
                        page.CurrentPage = page.Count;
                        await message.ModifyAsync(embed: page.GetEmbed());
                    }
                }
                else if (e.Emoji.Equals(page.Options.EmoteStop))
                {
                    _messages.Remove(message.Id);
                    if (page.Options.EmoteStopAction == StopAction.DeleteMessage)
                    {
                        await message.DeleteAsync();
                    }
                    else if (page.Options.EmoteStopAction == StopAction.ClearReactions)
                    {
                        await message.DeleteAllReactionsAsync();
                    }
                }

                _ = message.DeleteReactionAsync(e.Emoji, e.User);
            }
        }
    }

    public class PaginatedMessage
    {
        public PaginatedMessage(IEnumerable<string> pages, string title = "", int embedColor = 0,
            DiscordUser user = null, AppearanceOptions options = null)
            : this(pages.Select(x => new Page { Description = x }), title, embedColor, user, options)
        {
        }

        public PaginatedMessage(IEnumerable<Page> pages, string title = "", int embedColor = 0, DiscordUser user = null,
            AppearanceOptions options = null)
        {
            List<DiscordEmbed> embeds = new();
            int i = 1;
            foreach (Page page in pages)
            {
                DiscordEmbedBuilder builder = new()
                {
                    Color = new DiscordColor(embedColor),
                    Title = title,
                    Description = page?.Description,
                    ImageUrl = page?.ImageUrl
                };
                builder.WithThumbnail(page?.ThumbnailUrl);
                builder.WithFooter($"Page {i++}/{pages.Count()}");
                List<DiscordEmbedField> pageFields = page?.Fields?.ToList() ?? new List<DiscordEmbedField>();
                if (pageFields != null)
                {
                    foreach (DiscordEmbedField field in pageFields)
                    {
                        builder.AddField(field.Name, field.Value, field.Inline);
                    }
                }

                embeds.Add(builder);
            }

            Pages = embeds;
            Title = title;
            EmbedColor = embedColor;
            User = user;
            Options = options ?? new AppearanceOptions();
            CurrentPage = 1;
        }

        internal DiscordEmbed GetEmbed() => Pages.ElementAtOrDefault(CurrentPage - 1);

        internal string Title { get; }
        internal int EmbedColor { get; }
        internal IReadOnlyCollection<DiscordEmbed> Pages { get; }
        internal DiscordUser User { get; }
        internal AppearanceOptions Options { get; }
        internal int CurrentPage { get; set; }
        internal int Count => Pages.Count;
    }

    public class AppearanceOptions
    {
        public const string FIRST = "⏮";
        public const string BACK = "◀";
        public const string NEXT = "▶";
        public const string LAST = "⏭";
        public const string STOP = "⏹";

        public DiscordEmoji EmoteFirst { get; set; } = DiscordEmoji.FromUnicode(FIRST);
        public DiscordEmoji EmoteBack { get; set; } = DiscordEmoji.FromUnicode(BACK);
        public DiscordEmoji EmoteNext { get; set; } = DiscordEmoji.FromUnicode(NEXT);
        public DiscordEmoji EmoteLast { get; set; } = DiscordEmoji.FromUnicode(LAST);
        public DiscordEmoji EmoteStop { get; set; } = DiscordEmoji.FromUnicode(STOP);
        public TimeSpan Timeout { get; set; } = TimeSpan.Zero;
        public StopAction EmoteStopAction { get; set; } = StopAction.DeleteMessage;
        public StopAction TimeoutAction { get; set; } = StopAction.DeleteMessage;
    }

    public enum StopAction
    {
        ClearReactions,
        DeleteMessage
    }

    public class Page
    {
        public IReadOnlyCollection<DiscordEmbedField> Fields { get; set; } //TODO: Need to change it to a builder
        public string Description { get; set; }
        public string ImageUrl { get; set; }
        public string ThumbnailUrl { get; set; }
    }
}
