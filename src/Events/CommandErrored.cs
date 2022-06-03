using System;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.Exceptions;
using Microsoft.Extensions.Logging;

namespace DSharpPlus.DocBot.Events
{
    public sealed class CommandErrored
    {
        public static Task CommandErroredAsync(CommandsNextExtension _, CommandErrorEventArgs eventArgs)
        {
            // Build a list of errors that went wrong.
            StringBuilder stringBuilder = new();
            switch (eventArgs.Exception)
            {
                // Discord fucked up, probably. Also the smallest chance that it was the bot who fucked up.
                case DiscordException discordException:
                    stringBuilder.AppendFormat("Discord API returned {0}: {1}\n", discordException.WebResponse.ResponseCode, discordException.JsonMessage);
                    break;
                // The bot fucked up.
                case Exception:
                    stringBuilder.AppendFormat("Unexpected exception: {0} threw a {1}: {2}", eventArgs.Command?.QualifiedName ?? "Unknown", eventArgs.Exception.GetType().Name, eventArgs.Exception.Message);
                    eventArgs.Context.Client.Logger.LogError(eventArgs.Exception, "Unexpected exception.");
                    break;
            }

            // TODO: This may cause a recursive loop if the bot doesn't have permission to send messages. Fix this by checking if the exception isn't a 403 Unauthorized exception.
            return eventArgs.Context.RespondAsync(stringBuilder.ToString());
        }
    }
}
