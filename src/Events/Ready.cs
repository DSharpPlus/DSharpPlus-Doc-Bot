using System.Threading.Tasks;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;

namespace DSharpPlus.DocBot.Events
{
    public sealed class Ready
    {
        /// <summary>
        /// Change our bot's status to "Listening to the DSharpPlus library"
        /// </summary>
        public static Task ReadyAsync(DiscordClient client, ReadyEventArgs _) => client.UpdateStatusAsync(new DiscordActivity("the DSharpPlus library", ActivityType.ListeningTo));
    }
}
