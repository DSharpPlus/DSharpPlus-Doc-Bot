using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DSharpPlus.DocBot.Types;
using DSharpPlus.Entities;

namespace DSharpPlus.DocBot.Interfaces
{
    public interface IPaginatorService
    {
        Paginator CreatePaginator(IEnumerable<Page> pages, DiscordUser author);
        Paginator CreatePaginator(Paginator existingPaginator, DiscordUser newAuthor, DiscordMessage? newMessage = null);
        Paginator? GetPaginator(Ulid paginatorId);
        Task<bool> RemovePaginatorAsync(Ulid paginatorId, bool editMessage = false);
    }
}
