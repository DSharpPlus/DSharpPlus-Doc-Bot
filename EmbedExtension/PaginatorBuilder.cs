using DSharpPlus.Entities;
using System.Collections.Generic;

namespace DSharpPlusDocs.EmbedExtension
{
    class PaginatorBuilder
    {
        public IEnumerable<string> Pages { get; set; }
        public DiscordEmbedBuilder DiscordEmbedBuilder { get; set; }
    }
}
