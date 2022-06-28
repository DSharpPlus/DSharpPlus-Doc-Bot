using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;

namespace DSharpPlus.DocBot.Commands
{
    public class HelpCommand : BaseCommandModule
    {
        [Command("help"), Description("Lists all the commands available.")]
        public async Task HelpAsync(CommandContext context)
        {
            DiscordEmbedBuilder embedBuilder = new();
            embedBuilder.WithTitle("Help");
            embedBuilder.WithDescription($"Use {Formatter.InlineCode("help <command name>")} to get more information about a command.");
            embedBuilder.WithColor(new DiscordColor("#323232"));

            foreach (Command command in context.CommandsNext.RegisteredCommands.Values)
            {
                StringBuilder argumentStringBuilder = new();
                foreach (CommandOverload commandOverload in command.Overloads)
                {
                    foreach (CommandArgument overloadArgument in commandOverload.Arguments)
                    {
                        argumentStringBuilder.Append(CachedReflection.GetFriendlyTypeName(overloadArgument.Type));
                        argumentStringBuilder.Append(' ');
                        argumentStringBuilder.Append(overloadArgument.Name);
                        if (overloadArgument.DefaultValue != null)
                        {
                            argumentStringBuilder.Append(" = ");
                            argumentStringBuilder.Append(overloadArgument.DefaultValue);
                        }
                    }
                }

                embedBuilder.AddField(command.Name, $"{Formatter.InlineCode(command.Name)}: {command.Description}");
            }

            await context.RespondAsync(embedBuilder.Build());
        }

        [Command("help"), Description("Shows more information about a specific command.")]
        public async Task HelpAsync(CommandContext context, [Description("Which command to specifically help you with."), RemainingText] string commandName)
        {
            Command? command = context.CommandsNext.RegisteredCommands.FirstOrDefault(x => string.CompareOrdinal(x.Key, commandName) == 0).Value;
            if (command == null)
            {
                await context.RespondAsync($"Command \"{Formatter.InlineCode(commandName.ToLowerInvariant())}\" not found.");
                return;
            }

            DiscordEmbedBuilder embedBuilder = new();
            embedBuilder.WithTitle("Help");
            embedBuilder.WithColor(new DiscordColor("#323232"));
            foreach (CommandOverload commandOverload in command.Overloads)
            {
                StringBuilder argumentStringBuilder = new();
            }
        }
    }
}
