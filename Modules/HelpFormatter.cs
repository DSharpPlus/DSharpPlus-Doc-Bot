using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Converters;
using DSharpPlus.CommandsNext.Entities;
using DSharpPlus.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DSharpPlusDocs.Modules
{
    public class HelpFormatter : BaseHelpFormatter
    {
        private DiscordEmbedBuilder _embed;
        private string _name, _desc;
        private bool _gexec;
        private CommandContext _ctx;

        public HelpFormatter(CommandContext ctx)
            : base(ctx)
        {
            _embed = new DiscordEmbedBuilder();
            _name = null;
            _desc = null;
            _gexec = false;
            _ctx = ctx;
        }


        public override BaseHelpFormatter WithCommand(Command command)
        {
            _name = command.Name;
            _desc = command.Description;
            _gexec = true;
            if (command.Aliases.Any())
                _embed.AddField("Aliases", string.Join(", ", command.Aliases.Select(Formatter.InlineCode)), false);
            int i = 0;
            foreach (CommandOverload overload in command.Overloads)
            {
                var sb = new StringBuilder();

                foreach (var arg in overload.Arguments)
                {
                    if (arg.IsOptional || arg.IsCatchAll)
                        sb.Append("`[");
                    else
                        sb.Append("`<");

                    sb.Append(arg.Name);

                    if (arg.IsCatchAll)
                        sb.Append("...");

                    if (arg.IsOptional || arg.IsCatchAll)
                        sb.Append("]: ");
                    else
                        sb.Append(">: ");

                    sb.Append(BuildType(arg.Type)).Append("`: ");

                    sb.Append(string.IsNullOrWhiteSpace(arg.Description) ? "No description provided." : arg.Description);

                    if (arg.IsOptional)
                        sb.Append(" Default value: ").Append(arg.DefaultValue);

                    sb.AppendLine();
                }
                _embed.AddField($"Arguments [{i++}]", sb.ToString(), false);
            }
            return this;
        }

        public static string BuildType(Type type)
        {
            string typeName = type.Name, typeGeneric = "";
            int idx;
            if ((idx = typeName.IndexOf('`')) != -1)
            {
                typeName = typeName.Substring(0, idx);
                var generics = type.GetGenericArguments();
                if (generics.Any())
                    typeGeneric = string.Join(", ", generics.Select(x => BuildType(x)));
            }
            return GetTypeName(type, typeName, typeGeneric);
        }

        private static string GetTypeName(Type type, string name, string generic)
        {
            if (Nullable.GetUnderlyingType(type) != null)
                return $"{generic}?";
            if (type.IsByRef)
                return BuildType(type.GetElementType());
            return Aliases.ContainsKey(type) ? Aliases[type] : $"{name}{(string.IsNullOrEmpty(generic) ? "" : $"<{generic}>")}";
        }

        private static readonly Dictionary<Type, string> Aliases = new Dictionary<Type, string>()
        {
            { typeof(byte), "byte" },
            { typeof(sbyte), "sbyte" },
            { typeof(short), "short" },
            { typeof(ushort), "ushort" },
            { typeof(int), "int" },
            { typeof(uint), "uint" },
            { typeof(long), "long" },
            { typeof(ulong), "ulong" },
            { typeof(float), "float" },
            { typeof(double), "double" },
            { typeof(decimal), "decimal" },
            { typeof(object), "object" },
            { typeof(bool), "bool" },
            { typeof(char), "char" },
            { typeof(string), "string" },
            { typeof(void), "void" }
        };

        public override BaseHelpFormatter WithSubcommands(IEnumerable<Command> subcommands)
        {
            if (subcommands.Any())
                _embed.AddField(_name != null ? "Subcommands" : "Commands", string.Join(", ", subcommands.Select(xc => Formatter.InlineCode(xc.Name))), false);
            return this;
        }

        public override CommandHelpMessage Build()
        {
            _embed.Title = "Help";
            _embed.Color = DiscordColor.Azure;

            //var desc = "Listing all top-level commands and groups. Specify a command to see more information.";
            var desc = "";
            if (_name != null)
            {
                var sb = new StringBuilder();
                sb.Append(Formatter.InlineCode(_name))
                    .Append(": ")
                    .Append(string.IsNullOrWhiteSpace(_desc) ? "No description provided." : _desc);

                if (_gexec)
                    sb.AppendLine().AppendLine().Append("This group can be executed as a standalone command.");

                desc = sb.ToString();
            }
            else
            {
                _embed.AddField("Query help", $"Usage: {_ctx.Client.CurrentUser.Mention} [query]", false);
                _embed.AddField("Keywords", "method, type, property,\nevent, in, list", true);
                _embed.AddField("Examples", "DiscordEmbedBuilder\n" +
                                            "DiscordMember.Nickname\n" +
                                            "ModifyAsync in DiscordMessage\n" +
                                            "send message\n" +
                                            "type DiscordEmoji", true);
            }
            _embed.Description = desc;

            return new CommandHelpMessage(embed: _embed);
        }
    }
}
