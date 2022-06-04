using DSharpPlus.Entities;

namespace DSharpPlus.DocBot.Pagination
{
    /// <summary>
    /// Used for dropdown and button navigation.
    /// </summary>
    public sealed class MenuPagination
    {
        /// <summary>
        /// The title shown in the dropdown.
        /// </summary>
        public string Title { get; internal set; }

        /// <summary>
        /// The message to be shown upon user selection.
        /// </summary>
        public DiscordMessageBuilder Message { get; init; }

        public MenuPagination(string title, DiscordMessageBuilder message)
        {
            Title = title;
            Message = message;
        }
    }
}
