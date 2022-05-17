// This file is part of the DSharpPlus project.
//
// Copyright (c) 2015 Mike Santiago
// Copyright (c) 2016-2022 DSharpPlus Contributors
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Converters;
using DSharpPlus.CommandsNext.Entities;
using DSharpPlus.Entities;

namespace DSharpPlusDocs.Modules
{
    public class HelpFormatter : BaseHelpFormatter
    {
        private readonly DiscordEmbedBuilder _embed;
        private string _name, _desc;
        private bool _gexec;
        private readonly CommandContext _ctx;

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
            {
                _embed.AddField("Aliases", string.Join(", ", command.Aliases.Select(Formatter.InlineCode)), false);
            }

            int i = 0;
            foreach (CommandOverload overload in command.Overloads)
            {
                StringBuilder sb = new();

                foreach (CommandArgument arg in overload.Arguments)
                {
                    if (arg.IsOptional || arg.IsCatchAll)
                    {
                        sb.Append("`[");
                    }
                    else
                    {
                        sb.Append("`<");
                    }

                    sb.Append(arg.Name);

                    if (arg.IsCatchAll)
                    {
                        sb.Append("...");
                    }

                    if (arg.IsOptional || arg.IsCatchAll)
                    {
                        sb.Append("]: ");
                    }
                    else
                    {
                        sb.Append(">: ");
                    }

                    sb.Append(BuildType(arg.Type)).Append("`: ");

                    sb.Append(string.IsNullOrWhiteSpace(arg.Description) ? "No description provided." : arg.Description);

                    if (arg.IsOptional)
                    {
                        sb.Append(" Default value: ").Append(arg.DefaultValue);
                    }

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
                typeName = typeName[..idx];
                Type[] generics = type.GetGenericArguments();
                if (generics.Any())
                {
                    typeGeneric = string.Join(", ", generics.Select(x => BuildType(x)));
                }
            }
            return GetTypeName(type, typeName, typeGeneric);
        }

        private static string GetTypeName(Type type, string name, string generic) => Nullable.GetUnderlyingType(type) != null
                ? $"{generic}?"
                : type.IsByRef
                ? BuildType(type.GetElementType())
                : Aliases.ContainsKey(type) ? Aliases[type] : $"{name}{(string.IsNullOrEmpty(generic) ? "" : $"<{generic}>")}";

        private static readonly Dictionary<Type, string> Aliases = new()
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
            {
                _embed.AddField(_name != null ? "Subcommands" : "Commands", string.Join(", ", subcommands.Select(xc => Formatter.InlineCode(xc.Name))), false);
            }

            return this;
        }

        public override CommandHelpMessage Build()
        {
            _embed.Title = "Help";
            _embed.Color = DiscordColor.Azure;

            //var desc = "Listing all top-level commands and groups. Specify a command to see more information.";
            string desc = "";
            if (_name != null)
            {
                StringBuilder sb = new();
                sb.Append(Formatter.InlineCode(_name))
                    .Append(": ")
                    .Append(string.IsNullOrWhiteSpace(_desc) ? "No description provided." : _desc);

                if (_gexec)
                {
                    sb.AppendLine().AppendLine().Append("This group can be executed as a standalone command.");
                }

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
