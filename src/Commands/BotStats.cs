using System;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.DocBot.Interfaces;
using DSharpPlus.Entities;
using Humanizer;

namespace DSharpPlus.DocBot.Commands
{
    public sealed class BotStats : BaseCommandModule
    {
        public IDocumentationService DocumentationService { private get; init; } = null!;

        [Command("bot_stats"), Description("Gets general info about the bot."), Aliases("bot_info", "bs", "bi")]
        public Task BotStatsAsync(CommandContext context)
        {
            DiscordEmbedBuilder embedBuilder = new()
            {
                Title = "Bot Info",
                Color = new DiscordColor("#323232")
            };
            embedBuilder.AddField("Documentation Version", DocumentationService.GetCurrentVersion() ?? "Local, likely outdated.", true);
            embedBuilder.AddField("DSharpPlus Version", typeof(DiscordClient).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion, true);
            embedBuilder.AddField("\u200b", "\u200b", true); // Blank field
            embedBuilder.AddField("Heap Memory", GC.GetTotalMemory(true).Bytes().ToString("MB", CultureInfo.InvariantCulture), true);
            embedBuilder.AddField("Process Memory", Process.GetCurrentProcess().WorkingSet64.Bytes().ToString("MB", CultureInfo.InvariantCulture), true);
            embedBuilder.AddField("Thread Count", ThreadPool.ThreadCount.ToMetric(), true);
            embedBuilder.AddField("Uptime", (Process.GetCurrentProcess().StartTime - DateTime.Now).Humanize(3), true);
            return context.RespondAsync(embedBuilder.Build());
        }
    }
}
