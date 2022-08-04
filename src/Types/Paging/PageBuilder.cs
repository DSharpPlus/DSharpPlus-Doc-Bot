using System.Reflection;
using DSharpPlus.Entities;

namespace DSharpPlus.DocBot.Types
{
    public sealed class PageBuilder
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public DiscordEmoji? Emoji { get; set; }
        public string? Content { get; set; }
        public DiscordEmbedBuilder Embed { get; set; } = new();
        public MemberTypes? MemberType { get; set; }

        public static implicit operator Page(PageBuilder builder) => new(builder.Content, builder.Embed.Build(), builder.Title, builder.Description, builder.Emoji, builder.MemberType);
    }
}
