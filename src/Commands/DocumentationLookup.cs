using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
    public sealed class DocumentationLookupCommand : BaseCommandModule
    {
        [Command("docs"), Description("Look up documentation for a command.")]
        [RequireBotPermissions(Permissions.SendMessages | Permissions.EmbedLinks), SuppressMessage("Roslyn", "CA1822", Justification = "CommandsNext cannot comprehend the power behind static commands.")]
        public Task DocumentationLookupAsync(CommandContext context, [Description("Does reflection magic to find the nearest type, event, method or property that's closest to the documentation request. Includes XML docs and a link to the source code on Github."), RemainingText] string documentationRequest)
        {
            // Search types, methods and properties and order by relevance.
            IEnumerable<MenuPagination> pages = LookupTypes(documentationRequest)
                // TODO: .Concat(LookupEvents(documentationRequest))
                .Concat(LookupMethods(documentationRequest))
                .Concat(LookupProperties(documentationRequest))
                .DistinctBy(x => x.Title)
                .OrderBy(x => x.Message.Embeds[0].Footer.Text) // Sort by whichever is closest to the search query.
                .OrderBy(x => x.Title.Contains("Type:"))
                .OrderBy(x => x.Title.Contains("Method:"))
                .OrderBy(x => x.Title.Contains("Property:"));

            return pages.Count() switch
            {
                0 => context.RespondAsync("No documentation found."),
                1 => context.RespondAsync(pages.First().Message),
                _ => MenuPaginator.SendNewPaginationAsync(context.User, context.Channel, pages.Take(0..25).ToArray()),
            };
        }

        private static IEnumerable<MenuPagination> LookupTypes(string documentationRequest)
        {
            List<MenuPagination> pages = new();
            DiscordEmbedBuilder embedBuilder;
            foreach (Type type in CachedReflection.Types)
            {
                int matchRatio = Fuzz.PartialTokenAbbreviationRatio(documentationRequest, type.FullName);
                if (matchRatio <= 80) // Ignore types with a low match ratio.
                {
                    continue;
                }

                // Grab the type's methods and properties.
                IEnumerable<MethodInfo> methods = CachedReflection.MethodGroups.Where(x => x.Value[0].DeclaringType == type).OrderBy(x => x.Key).Select(x => x.Value[0]).Take(0..3).Distinct();
                IEnumerable<PropertyInfo> properties = CachedReflection.Properties.Where(x => x.DeclaringType == type).OrderBy(x => x.Name).Take(0..3).Distinct();

                // We're reusing the same embed builder, so clear it.
                embedBuilder = new();
                embedBuilder.WithTitle($"Type: {type.Name}");
                embedBuilder.WithFooter($"Match Percentage: {matchRatio}%"); // Haha make fun of the user for making a typo. Also known as a match percentage.

                // If there are methods or properties, add them. Otherwise don't create empty fields.
                if (methods.Any())
                {
                    // 1024 / 3 = 341
                    embedBuilder.AddField("Methods", string.Join('\n', CachedReflection.GetMethodNames(type, 341).Take(0..3).Select(methodSig => $"- {Formatter.InlineCode(methodSig)}")));
                }

                if (properties.Any())
                {
                    embedBuilder.AddField("Properties", string.Join('\n', properties.Take(0..3).Select(x => $"- {Formatter.InlineCode(CachedReflection.GetPropertySignature(x))}")));
                }

                pages.Add(new MenuPagination($"Type: {type.Name}", new DiscordMessageBuilder().WithEmbed(embedBuilder)));
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
                int matchRatio = Fuzz.PartialTokenAbbreviationRatio(documentationRequest, methodName);
                if (matchRatio <= 80) // Ignore types with a low match ratio.
                {
                    continue;
                }

                // We're reusing the same embed builder, so clear it.
                embedBuilder = new();
                embedBuilder.WithTitle($"Method: {string.Join("", (methodGroup[0].DeclaringType!.Name + '.' + PruneNamespace(methodName)).Take(0..256))}");
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

                embedBuilder.WithDescription(string.Join("\n", stringBuilder.ToString().Split('\n').OrderBy(x => x).Distinct()));
                pages.Add(new MenuPagination($"Method: {methodGroup[0].DeclaringType!.Name}.{methodName}", new DiscordMessageBuilder().WithEmbed(embedBuilder)));
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
                int matchRatio = Fuzz.PartialTokenAbbreviationRatio(documentationRequest, $"{property.DeclaringType!.FullName}.{property.Name}");
                if (matchRatio <= 80) // Ignore types with a low match ratio.
                {
                    continue;
                }

                embedBuilder = new();
                embedBuilder.WithTitle($"Property: {property.DeclaringType.Name}.{property.Name}");
                embedBuilder.WithFooter($"Match Percentage: {matchRatio}%"); // Haha make fun of the user for making a typo. Also known as a match percentage.
                embedBuilder.AddField("Type", Formatter.InlineCode(CachedReflection.ResolveGenericTypes(property.PropertyType)));
                pages.Add(new MenuPagination($"Property: {property.DeclaringType.Name}.{property.Name}", new DiscordMessageBuilder().WithEmbed(embedBuilder)));
            }

            return pages;
        }

        private static string PruneNamespace(string typeOrMethodWithNamespace) => typeOrMethodWithNamespace.Split('.').Last();
    }
}
