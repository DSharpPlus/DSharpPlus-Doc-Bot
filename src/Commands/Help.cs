using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.DocBot.Interfaces;
using DSharpPlus.DocBot.Types;
using DSharpPlus.Entities;

namespace DSharpPlus.DocBot.Commands
{
    public sealed class HelpCommand : BaseCommandModule
    {
        public IPaginatorService PaginatorService { private get; init; } = null!;

        [Command("help")]
        [Description("Lists all commands available or provides more specific information about the requested command.")]
        [RequireBotPermissions(Permissions.SendMessages | Permissions.EmbedLinks)]
        public Task HelpAsync(CommandContext context, [RemainingText, Description("Which command to get more specific information on. If not specified, lists all commands.")] string? searchCommand = null)
        {
            List<Page> pages = new();
            if (searchCommand == null || (!context.CommandsNext.RegisteredCommands.TryGetValue(searchCommand, out Command? command) && command == null))
            {
                pages.AddRange(ListCommands(context, context.CommandsNext.RegisteredCommands.Values));
            }
            else
            {
                pages.AddRange(GetCommand(context, command));
            }

            if (pages.Count == 0)
            {
                return context.RespondAsync("No results found.");
            }
            else if (pages.Count == 1)
            {
                return context.RespondAsync(pages[0].Embed!);
            }
            else
            {
                Paginator paginator = PaginatorService.CreatePaginator(pages, context.User);
                return context.RespondAsync(paginator.GenerateMessage());
            }
        }

        private static IEnumerable<Page> ListCommands(CommandContext context, IEnumerable<Command> commands)
        {
            DiscordEmbedBuilder embedBuilder = new()
            {
                Author = new()
                {
                    Name = context.Member!.DisplayName,
                    IconUrl = context.Member!.GetGuildAvatarUrl(ImageFormat.Png, 4096)
                },
                Color = new(0x323232),
                Footer = new()
                {
                    Text = "Use `help <command>` to get more specific information about a command. Arguments in square brackets `[]` are optional, while arguments in brackets `<>` are required."
                }
            };

            foreach (Command command in commands)
            {
                embedBuilder = new(embedBuilder.Build())
                {
                    Title = command.Name,
                    Description = command.Description
                };

                embedBuilder.ClearFields();
                if (command.Aliases.Count != 0)
                {
                    embedBuilder.AddField("Aliases", string.Join(", ", command.Aliases));
                }

                embedBuilder.AddField("Usage", GetCommandUsage(command, context.Prefix));
                yield return new Page(null, embedBuilder, command.Name, command.Description);
            }
        }

        private static IEnumerable<Page> GetCommand(CommandContext context, Command command)
        {
            List<Page> pages = new();
            DiscordEmbedBuilder embedBuilder = new()
            {
                Author = new()
                {
                    Name = context.Member!.DisplayName,
                    IconUrl = context.Member!.GetGuildAvatarUrl(ImageFormat.Png, 4096)
                },
                Title = command.Name,
                Description = command.Name,
                Color = new(0x323232),
                Footer = new()
                {
                    Text = "Use `help <command>` to get more specific information about a command. Arguments in square brackets `[]` are optional, while arguments in brackets `<>` are required."
                }
            };

            if (command.Aliases.Count != 0)
            {
                embedBuilder.AddField("Aliases", string.Join(", ", command.Aliases));
            }

            embedBuilder.AddField("Usage", GetCommandUsage(command, context.Prefix));
            pages.Add(new Page(null, embedBuilder, command.Name, command.Description));

            foreach (CommandOverload overload in command.Overloads.OrderBy(overload => overload.Priority))
            {
                embedBuilder = new(embedBuilder.Build())
                {
                    Description = GetOverloadUsage(context.Prefix, command.Name, overload).ToString()
                };

                foreach (CommandArgument argument in overload.Arguments)
                {
                    embedBuilder.AddField(argument.Name, argument.Description);
                }

                pages.Add(new Page(null, embedBuilder, command.Name, command.Description));
            }

            return pages;
        }

        private static string GetCommandUsage(Command command, string prefix)
        {
            StringBuilder overloadStringBuilder = new();
            foreach (CommandOverload overload in command.Overloads.OrderBy(overload => overload.Priority))
            {
                GetOverloadUsage(prefix, command.Name, overload, overloadStringBuilder);
                overloadStringBuilder.AppendLine();
            }

            return overloadStringBuilder.ToString();
        }

        private static StringBuilder GetOverloadUsage(string prefix, string commandName, CommandOverload overload, StringBuilder? stringBuilder = null)
        {
            stringBuilder ??= new();
            stringBuilder.AppendFormat("{0} {1} ", prefix, commandName);
            foreach (CommandArgument argument in overload.Arguments)
            {
                if (argument.IsOptional)
                {
                    stringBuilder.AppendFormat("[{0}] ", argument.Name);
                }
                else
                {
                    stringBuilder.AppendFormat("<{0}> ", argument.Name);
                }

                if (argument.DefaultValue != null)
                {
                    stringBuilder.AppendFormat("= {0}", argument.DefaultValue);
                }

                stringBuilder.Append(',');
            }
            stringBuilder.Remove(stringBuilder.Length - 1, 1);
            return stringBuilder;
        }
    }
}
