using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.DocBot.Pagination;
using DSharpPlus.Entities;
using FuzzySharp;

namespace DSharpPlus.DocBot.Commands
{
    public sealed class DocumentationLookup : BaseCommandModule
    {
        [Command("docs"), Description("Look up documentation for a command."), RequireBotPermissions(Permissions.SendMessages | Permissions.EmbedLinks)]
        public Task DocumentationLookupAsync(CommandContext context, [RemainingText] string documentationRequest)
        {
            // Search types, methods and properties and order by relevance.
            IEnumerable<MenuPagination> pages = LookupTypes(documentationRequest)
                .Concat(LookupMethods(documentationRequest))
                .Concat(LookupProperties(documentationRequest))
                .DistinctBy(x => x.Title)
                .GroupBy(x => x.Title.Split('.').Last())
                .SelectMany(x =>
                {
                    if (x.Count() > 1)
                    {
                        return x;
                    }
                    else
                    {
                        x.First().Title = x.Key;
                        return x;
                    }
                })
                .Select(x =>
                {
                    x.Title = string.Join("", x.Title.Take(0..100));
                    x.Message.WithEmbed(new DiscordEmbedBuilder(x.Message.Embeds[0])
                    {
                        Title = x.Title
                    });
                    return x;
                })
                .OrderBy(x => x.Message.Embeds[0].Footer.Text) // Sort by whichever is closest to the search query.
                .OrderBy(x => x.Message.Embeds[0].Title.Contains("Type:"))
                .OrderBy(x => x.Message.Embeds[0].Title.Contains("Method:"))
                .OrderBy(x => x.Message.Embeds[0].Title.Contains("Property:"));

            return pages.Count() switch
            {
                0 => context.RespondAsync("No documentation found."),
                1 => context.RespondAsync(pages.First().Message),
                _ => MenuPaginator.SendNewPaginationAsync(context.User, context.Channel, pages.Take(new Range(0, 25)).ToArray()),
            };
        }

        private static IEnumerable<MenuPagination> LookupTypes(string documentationRequest)
        {
            List<MenuPagination> pages = new();
            DiscordEmbedBuilder embedBuilder;
            foreach (Type type in CachedReflection.Types)
            {
                int matchRatio = Fuzz.TokenAbbreviationRatio(documentationRequest, type.FullName);
                if (matchRatio <= 80) // Ignore types with a low match ratio.
                {
                    continue;
                }

                // Grab the type's methods and properties.
                IEnumerable<MethodInfo> methods = CachedReflection.MethodGroups.Where(x => x.Value[0].DeclaringType == type).OrderBy(x => x.Key).Select(x => x.Value[0]).Take(0..3).Distinct();
                IEnumerable<PropertyInfo> properties = CachedReflection.Properties.Where(x => x.DeclaringType == type).OrderBy(x => x.Name).Take(0..3).Distinct();

                // We're reusing the same embed builder, so clear it.
                embedBuilder = new();
                embedBuilder.WithTitle($"Type: {type.FullName}");
                embedBuilder.WithFooter($"Match Percentage: {matchRatio}%"); // Haha make fun of the user for making a typo. Also known as a match percentage.

                // If there are methods or properties, add them. Otherwise don't create empty fields.
                if (methods.Any())
                {
                    embedBuilder.AddField("Methods", string.Join("\n", methods.Select(x => $"- {Formatter.InlineCode(CachedReflection.GetMethodSignature(x, CachedReflection.MethodSignatureFormat.IncludeReturnType | CachedReflection.MethodSignatureFormat.IncludeParameters))}").GroupBy(x => x).Select(x => x.First())));
                }

                if (properties.Any())
                {
                    embedBuilder.AddField("Properties", string.Join("\n", properties.Select(x => $"- {Formatter.InlineCode(CachedReflection.ResolveGenericTypes(x.PropertyType) + " " + x.Name)}")));
                }

                pages.Add(new MenuPagination($"Type: {type.FullName!}", new DiscordMessageBuilder().WithEmbed(embedBuilder)));
            }

            return pages;
        }

        private static IEnumerable<MenuPagination> LookupMethods(string documentationRequest)
        {
            List<MenuPagination> pages = new();
            DiscordEmbedBuilder embedBuilder;

            // Iterate through all methods. Methods are grouped by overloads and indexed using their method name.
            foreach ((string methodName, MethodInfo[] methodGroup) in CachedReflection.MethodGroups)
            {
                int matchRatio = Fuzz.TokenAbbreviationRatio(documentationRequest, methodName);
                if (matchRatio <= 80) // Ignore types with a low match ratio.
                {
                    continue;
                }

                // We're reusing the same embed builder, so clear it.
                embedBuilder = new();
                embedBuilder.WithTitle($"Method: {PruneNamespace(methodName).Take(0..256)}");
                embedBuilder.WithFooter($"Match Percentage: {matchRatio}%"); // Haha make fun of the user for making a typo. Also known as a match percentage.

                // Add the method overloads into one field by displaying it's signature.
                StringBuilder stringBuilder = new();
                foreach (MethodInfo method in methodGroup)
                {
                    string methodSignature = $"- {Formatter.InlineCode(CachedReflection.GetMethodSignature(method))}\n";
                    if ((stringBuilder.Length + methodSignature.Length) > 1024)
                    {
                        break;
                    }
                    stringBuilder.Append(methodSignature);
                }
                embedBuilder.AddField("Overloads", stringBuilder.ToString());
                pages.Add(new MenuPagination($"Method: {methodName}", new DiscordMessageBuilder().WithEmbed(embedBuilder)));
            }

            return pages;
        }

        private static IEnumerable<MenuPagination> LookupProperties(string documentationRequest)
        {
            List<MenuPagination> pages = new();
            DiscordEmbedBuilder embedBuilder;

            // Iterate through all properties
            foreach (PropertyInfo property in CachedReflection.Properties)
            {
                int matchRatio = Fuzz.TokenAbbreviationRatio(documentationRequest, $"{property.DeclaringType!.FullName}.{property.Name}");
                if (matchRatio <= 80) // Ignore types with a low match ratio.
                {
                    continue;
                }

                embedBuilder = new();
                embedBuilder.WithTitle($"Property: {property.DeclaringType!.FullName}.{property.Name}");
                embedBuilder.WithFooter($"Match Percentage: {matchRatio}%"); // Haha make fun of the user for making a typo. Also known as a match percentage.
                embedBuilder.AddField("Type", Formatter.InlineCode(CachedReflection.ResolveGenericTypes(property.PropertyType)));
                pages.Add(new MenuPagination($"Property: {property.DeclaringType!.FullName}.{property.Name}", new DiscordMessageBuilder().WithEmbed(embedBuilder)));
            }

            return pages;
        }

        private static string PruneNamespace(string typeOrMethodWithNamespace) => typeOrMethodWithNamespace.Split('.').Last();
    }
}
