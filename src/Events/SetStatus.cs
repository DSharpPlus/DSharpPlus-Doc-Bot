using System;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DSharpPlus.DocBot.Events
{
    public sealed class SetStatus
    {
        public IConfiguration Configuration { private get; init; } = null!;
        public ILogger<SetStatus> Logger { private get; init; } = null!;

        public SetStatus(IConfiguration configuration, ILogger<SetStatus> logger)
        {
            ArgumentNullException.ThrowIfNull(configuration, nameof(configuration));
            ArgumentNullException.ThrowIfNull(logger, nameof(logger));

            Configuration = configuration;
            Logger = logger;
        }

        public Task SetStatusAsync(DiscordClient client, GuildDownloadCompletedEventArgs eventArgs)
        {
            foreach (DiscordGuild guild in eventArgs.Guilds.Values.OrderByDescending(x => x.MemberCount))
            {
                Logger.LogInformation("Discord guild {GuildId} ({GuildName}) is ready with {MemberCount:N0} members.", guild.Id, guild.Name, guild.MemberCount);
            }

            return client.UpdateStatusAsync(new DiscordActivity(Configuration.GetValue("discord:status:text", "for documentation requests."), Configuration.GetValue("discord:status:type", ActivityType.Watching)));
        }
    }
}
