using System;
using System.Linq;
using System.Reflection;
using System.Text;
using DSharpPlus.DocBot.Interfaces;
using DSharpPlus.DocBot.Types;
using DSharpPlus.Entities;

namespace DSharpPlus.DocBot.Services
{
    public partial class DocumentationService : IDocumentationService
    {
        private static void FormatType(XmlMemberInfo memberInfo, Type type, DiscordEmbedBuilder embedBuilder)
        {
            if (type.GetInterfaces().Any())
            {
                embedBuilder.AddField("Implements", string.Join(", ", type.GetInterfaces().Select(i => Formatter.InlineCode(ResolveGenericTypes(i)))));
            }

            if (memberInfo.InheritedTypes.Count != 0)
            {
                embedBuilder.AddField("Inherits", string.Join(", ", memberInfo.InheritedTypes.Select(i => Formatter.InlineCode(ResolveGenericTypes(i)))));
            }

            StringBuilder stringBuilder = new("```cs\n");
            foreach (PropertyInfo propertyInfo in type.GetProperties().Take(3))
            {
                stringBuilder.AppendLine(GetPropertySignature(propertyInfo));
            }
            stringBuilder.Append("\n```");

            if (stringBuilder.ToString() != "```cs\n\n```")
            {
                embedBuilder.AddField("Properties", stringBuilder.ToString());
            }

            stringBuilder = new("```cs\n");
            foreach (MethodInfo methodInfo in type.GetMethods().Where(method => method.IsPublic && !method.IsSpecialName).Take(3))
            {
                string methodName = GetMethodSignature(methodInfo, MethodSignatureFormat.Full);
                if (methodName.Length < 341)
                {
                    stringBuilder.AppendLine(methodName);
                    continue;
                }

                methodName = GetMethodSignature(methodInfo, MethodSignatureFormat.IncludeReturnType | MethodSignatureFormat.IncludeParameters);
                if (methodName.Length < 341)
                {
                    stringBuilder.AppendLine(methodName);
                    continue;
                }

                stringBuilder.AppendLine(methodName[0..Math.Min(methodName.Length, 340)] + '…');
            }
            stringBuilder.Append("\n```");

            if (stringBuilder.ToString() != "```cs\n\n```")
            {
                embedBuilder.AddField("Methods", stringBuilder.ToString());
            }
        }

        /// <summary>
        /// Resolves generic types into a string, converting them into their C# representation.
        /// </summary>
        /// <example>
        /// <code>
        ///    ResolveGenericTypes(typeof(List&lt;int&gt;)) // "List&lt;int&gt;"
        ///    ResolveGenericTypes(typeof(Nullable&lt;ulong&gt;)) // "ulong?"
        ///    ResolveGenericTypes(typeof(DSharpPlus.Entities.DiscordApplication)) // "DiscordApplication"
        /// </code>
        /// </example>
        /// <param name="type">The type to resolve.</param>
        /// <param name="stringBuilder">The currently resolved string representation of the (generic) types.</param>
        /// <returns>A string representation of the type.</returns>
        public static string ResolveGenericTypes(Type type, StringBuilder? stringBuilder = null)
        {
            stringBuilder ??= new();

            // Test if the type is nullable.
            Type? underlyingNullableType = Nullable.GetUnderlyingType(type);
            if (underlyingNullableType != null)
            {
                // GetTypeOutput returns the full namespace for the type, which is why we split by `.` and take the last element (which should be the type name)
                // We also append a `?` to the end of the type name to represent the nullable type.
                stringBuilder.Append(GetFriendlyTypeName(underlyingNullableType) + "?");
            }

            // Test if the type is a generic type.
            else if (type.IsGenericType)
            {
                // type.Name contains `1 (Action`1) instead of brackets. We chop off the backticks and append the `<` and `>` to the front and back, with the type arguments in between.
                stringBuilder.Append(type.Name.AsSpan(0, type.Name.IndexOf('`')));
                stringBuilder.Append('<');
                foreach (Type genericType in type.GetGenericArguments())
                {
                    // Surprise! It's a recursive method.
                    ResolveGenericTypes(genericType, stringBuilder);
                    stringBuilder.Append(", ");
                }

                if (stringBuilder[^1] == ' ' && stringBuilder[^2] == ',') // EndsWith(", ")
                {
                    stringBuilder.Remove(stringBuilder.Length - 2, 2);
                }
                stringBuilder.Append('>');
            }
            else
            {
                // As mentioned earlier, we use GetTypeOutput to get the full namespace for the type. We only want the type name.
                stringBuilder.Append(GetFriendlyTypeName(type));
            }

            return stringBuilder.ToString();
        }

        private static string GetFriendlyTypeName(Type type) => CodeDom.GetTypeOutput(new(type)).Split('.').Last();
    }
}
