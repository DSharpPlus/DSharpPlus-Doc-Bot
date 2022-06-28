using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;

namespace DSharpPlus.DocBot.Commands
{
    public class ReloadCommand : BaseCommandModule
    {
        private static readonly DiscordEmoji ReloadEmoji = DiscordEmoji.FromUnicode("🔃");

        [Command("reload"), Description("Downloads and reloads the latest documentation from Github Actions.")]
        [RequireOwner, SuppressMessage("Roslyn", "CA1822", Justification = "CommandsNext requires all commands to be instanced, for reasons unknown to me. Exa must be mad, not being able to use static.")]
        public async Task ReloadAsync(CommandContext context)
        {
            // Completely untested, requires active maintainers to contribute to the main repo. Good luck finding those anytime soon.
            await context.Message.CreateReactionAsync(ReloadEmoji);
            await CachedReflection.DownloadNightliesAsync();
            await context.Message.DeleteOwnReactionAsync(ReloadEmoji);
            await context.RespondAsync("Done!");
        }
    }
}
