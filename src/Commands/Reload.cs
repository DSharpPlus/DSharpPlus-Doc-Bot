using System;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.DocBot.Interfaces;

namespace DSharpPlus.DocBot.Commands
{
    public sealed class Reload : BaseCommandModule
    {
        public IDocumentationService DocumentationService { private get; init; }
        public BotStats BotStats { private get; init; }

        public Reload(IDocumentationService documentationService)
        {
            ArgumentNullException.ThrowIfNull(documentationService, nameof(documentationService));

            DocumentationService = documentationService;
            BotStats = new() { DocumentationService = documentationService };
        }

        [Command("reload"), Description("Reloads the bot."), RequireOwner]
        public async Task ReloadAsync(CommandContext context)
        {
            await context.RespondAsync("Reloading...");
            await DocumentationService.ReloadAsync();
            await BotStats.BotStatsAsync(context);
        }
    }
}
