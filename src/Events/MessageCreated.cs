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

            // Grab CNext
            CommandsNextExtension commandsNext = client.GetCommandsNext();

            // Remove the mention...
            string fullCommand = eventArgs.Message.Content[client.CurrentUser.Mention.Length..].Trim();

            // See if the message is an actual command...
            Command? command = commandsNext.FindCommand(fullCommand, out string? arguments);

            // It's not, so try seaching up docs instead
            command ??= commandsNext.FindCommand("docs " + fullCommand, out arguments);

            // Off with his head! (Execute the command)
            return commandsNext.ExecuteCommandAsync(commandsNext.CreateContext(eventArgs.Message, client.CurrentUser.Mention, command, arguments));
        }
    }
}
