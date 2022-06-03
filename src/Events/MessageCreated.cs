using System.Globalization;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.EventArgs;

namespace DSharpPlus.DocBot.Events
{
    public sealed class MessageCreated
    {
        /// <summary>
        /// Handles commands for CNext.
        /// </summary>
        public static Task MessageCreatedAsync(DiscordClient client, MessageCreateEventArgs eventArgs)
        {
            // If the message doesn't start with a bot ping, ignore it.
            if (!eventArgs.Message.Content.StartsWith(client.CurrentUser.Mention, false, CultureInfo.InvariantCulture))
            {
                return Task.CompletedTask;
            }

            // Ensure the message contains something to search and isn't a random bot ping.
            string fullCommand = $"docs {eventArgs.Message.Content[client.CurrentUser.Mention.Length..].Trim()}";
            if (string.CompareOrdinal(fullCommand, "docs") == 0)
            {
                return Task.CompletedTask;
            }

            // Grab CNext and execute the command.
            CommandsNextExtension commandsNext = client.GetCommandsNext();
            return commandsNext.ExecuteCommandAsync(commandsNext.CreateContext(eventArgs.Message, client.CurrentUser.Mention, commandsNext.FindCommand(fullCommand, out string? rawArguments), rawArguments));
        }
    }
}
