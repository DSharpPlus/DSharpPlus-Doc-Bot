using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using DSharpPlus.DocBot.Interfaces;
using DSharpPlus.DocBot.Types;
using DSharpPlus.Entities;
using Microsoft.CSharp;
using Microsoft.Extensions.Configuration;

namespace DSharpPlus.DocBot.Services
{
    public sealed partial class DocumentationService : IDocumentationService
    {
        private static readonly CSharpCodeProvider CodeDom = new();

        private IReadOnlyDictionary<string, Page> FormatDocumentation(IEnumerable<XmlMemberInfo> members)
        {
            ConcurrentDictionary<string, Page> formattedPages = new();
            foreach (XmlMemberInfo memberInfo in members)
            {
                PageBuilder page = new()
                {
                    Title = memberInfo.Name,
                    MemberType = memberInfo.MemberInfo.MemberType
                };

                StringBuilder descriptionBuilder = new();
                if (!string.IsNullOrWhiteSpace(memberInfo.Summary))
                {
                    descriptionBuilder.AppendLine(memberInfo.Summary);
                }

                if (!string.IsNullOrWhiteSpace(memberInfo.Remarks))
                {
                    descriptionBuilder.AppendLine(memberInfo.Remarks);
                }

                if (descriptionBuilder.Length == 0)
                {
                    page.Description = "Undocumented. Free PR.";
                    page.Embed.Description = "Undocumented. Free PR.";
                }
                else
                {
                    page.Description = descriptionBuilder.ToString().Split('\n')[0];
                    page.Embed.Description = descriptionBuilder.ToString();
                }

                page.Embed.Title = memberInfo.Name;
                page.Embed.Color = new DiscordColor(Configuration.GetValue("discord:embed:color", "#323232"));
                if (CurrentVersion == null)
                {
                    page.Content = "Warning: Unable to fetch the latest version of the assembly from Github. Documentation may be outdated or incorrect.";
                }

                switch (memberInfo.MemberInfo)
                {
                    case Type type:
                        FormatType(memberInfo, type, page.Embed);
                        goto default;
                    case PropertyInfo propertyInfo:
                        FormatProperty(memberInfo, propertyInfo, page.Embed);
                        goto default;
                    case MethodBase methodBase:
                        goto default;
                    case EventInfo eventInfo:
                        goto default;
                    case FieldInfo fieldInfo:
                        goto default;
                    default:
                        formattedPages.AddOrUpdate(memberInfo.Name.ToLowerInvariant(), page, (key, value) => page);
                        break;
                }
            };
            return formattedPages;
        }

        private static string FormatNullableObject(object? obj) => obj switch
        {
            null => "null",
            _ when obj is string => $"\"{obj}\"",
            _ => obj.ToString()!
        };
    }
}
