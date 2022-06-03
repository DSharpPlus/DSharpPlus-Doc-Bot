using System;
using DSharpPlus.Entities;

namespace DSharpPlus.DocBot.Pagination
{
    public sealed class MenuIndex
    {
        /// <summary>
        /// The current page that the user is on.
        /// </summary>
        public int CurrentIndex { get; internal set; }

        /// <summary>
        /// A unix timestamp of when the user last interaction with this pagination session.
        /// </summary>
        public long LastUpdate { get; internal set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        /// <summary>
        /// The pages to be shown in the pagination.
        /// </summary>
        public MenuPagination[] Pages { get; internal set; } = Array.Empty<MenuPagination>();

        /// <summary>
        /// Who's in charge of the pagination session.
        /// </summary>
        public DiscordUser Author { get; internal set; }

        public MenuIndex(DiscordUser author, params MenuPagination[] pages)
        {

            if (pages.Length < 2)
            {
                throw new ArgumentException("There must be at least 2 pages.", nameof(pages));
            }
            else if (pages.Length > 25)
            {
                throw new ArgumentException("There cannot be more than 25 pages.", nameof(pages));
            }
            Pages = pages;
            Author = author;
        }

        /// <summary>
        /// Goes to the first page.
        /// </summary>
        public MenuPagination First()
        {
            CurrentIndex = 0;
            LastUpdate = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            return Pages[0];
        }

        /// <summary>
        /// Goes to the previous page, looping around if required.
        /// </summary>
        public MenuPagination Previous()
        {
            CurrentIndex = CurrentIndex == 0 ? Pages.Length - 1 : CurrentIndex - 1;
            LastUpdate = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            return Pages[CurrentIndex];
        }

        /// <summary>
        /// Goes to the next page, looping around if required.
        /// </summary>
        public MenuPagination Next()
        {
            CurrentIndex = CurrentIndex == Pages.Length - 1 ? 0 : CurrentIndex + 1;
            LastUpdate = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            return Pages[CurrentIndex];
        }

        /// <summary>
        /// Goes to the last page.
        /// </summary>
        public MenuPagination Last()
        {
            CurrentIndex = Pages.Length - 1;
            LastUpdate = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            return Pages[CurrentIndex];
        }

        /// <summary>
        /// Sets the current paged to the one with the given title. Used by the dropdown menu.
        /// </summary>
        /// <param name="title">The title to iterate to.</param>
        public MenuPagination Set(string title)
        {
            for (int i = 0; i < Pages.Length; i++)
            {
                if (Pages[i].Title == title)
                {
                    CurrentIndex = i;
                    LastUpdate = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                    return Pages[i];
                }
            }

            throw new ArgumentException("The specified page does not exist.", nameof(title));
        }
    }
}
