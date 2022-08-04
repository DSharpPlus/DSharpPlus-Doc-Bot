using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Timers;
using DSharpPlus.DocBot.Interfaces;
using DSharpPlus.DocBot.Types;
using DSharpPlus.Entities;
using Microsoft.Extensions.Configuration;

namespace DSharpPlus.DocBot.Services
{
    public sealed class PaginatorService : IPaginatorService
    {
        private ConcurrentDictionary<Ulid, Paginator> CurrentPaginators { get; init; } = new();
        private Timer CleanupTimer { get; init; } = new(TimeSpan.FromSeconds(1).TotalMilliseconds);
        private TimeSpan PaginatorTimeout { get; init; } = TimeSpan.FromSeconds(30);

        public PaginatorService(IConfiguration configuration)
        {
            PaginatorTimeout = configuration.GetValue("discord:paginator_timeout", TimeSpan.FromSeconds(30));
            CleanupTimer.Elapsed += CleanupPaginationsAsync;
            CleanupTimer.Start();
        }

        public Paginator CreatePaginator(IEnumerable<Page> pages, DiscordUser author)
        {
            ArgumentNullException.ThrowIfNull(author, nameof(author));
            ArgumentNullException.ThrowIfNull(pages, nameof(pages));

            Ulid id = Ulid.NewUlid();
            while (CurrentPaginators.ContainsKey(id))
            {
                id = Ulid.NewUlid();
            }

            Paginator paginator = new(id, pages, author);
            CurrentPaginators.AddOrUpdate(paginator.Id, paginator, (key, value) => paginator);
            return paginator;
        }

        public Paginator CreatePaginator(Paginator existingPaginator, DiscordUser newAuthor, DiscordMessage? newMessage = null)
        {
            ArgumentNullException.ThrowIfNull(existingPaginator, nameof(existingPaginator));

            Ulid id = Ulid.NewUlid();
            while (CurrentPaginators.ContainsKey(id))
            {
                id = Ulid.NewUlid();
            }

            Paginator paginator = new(id, existingPaginator, newAuthor) { CurrentMessage = newMessage };
            CurrentPaginators.AddOrUpdate(paginator.Id, paginator, (key, value) => paginator);
            return paginator;
        }

        public Paginator? GetPaginator(Ulid paginatorId) => CurrentPaginators.TryGetValue(paginatorId, out Paginator? paginator) ? paginator : null;

        public async Task<bool> RemovePaginatorAsync(Ulid paginatorId, bool editMessage = false)
        {
            if (CurrentPaginators.TryRemove(paginatorId, out Paginator? paginator) // If the paginator exists
                && paginator != null // And isn't null
                && editMessage // And we're supposed to edit the message
                && paginator.CurrentMessage != null // And has sent a message
                && (!paginator.CurrentMessage.Flags?.HasFlag(MessageFlags.Ephemeral) ?? true) // And isn't ephemeral
            )
            {
                DiscordMessageBuilder messageBuilder = paginator.Cancel();
                await paginator.CurrentMessage.ModifyAsync(messageBuilder);
                return true;
            }

            return false;
        }

        private async void CleanupPaginationsAsync(object? sender, ElapsedEventArgs e)
        {
            // Theoretically we don't need to call ToArray here since everything *should* be thread-safe.
            foreach (Paginator paginator in CurrentPaginators.Values)
            {
                // 30 second timeout.
                if (paginator != null && paginator.LastUpdatedAt.Add(PaginatorTimeout) <= DateTimeOffset.UtcNow)
                {
                    if (paginator.CurrentMessage != null)
                    {
                        paginator.CurrentMessage = paginator.CurrentMessage!.Flags?.HasMessageFlag(MessageFlags.Ephemeral) ?? false
                            ? await paginator.Interaction!.GetOriginalResponseAsync()
                            : await paginator.CurrentMessage.Channel.GetMessageAsync(paginator.CurrentMessage.Id);
                    }
                    await RemovePaginatorAsync(paginator.Id, true);
                }
            }
        }
    }
}
