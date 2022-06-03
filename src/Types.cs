using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using DSharpPlus.CommandsNext;
using DSharpPlus.Interactivity;
using DSharpPlus.Lavalink;
using DSharpPlus.SlashCommands;
using DSharpPlus.VoiceNext;
using Microsoft.CSharp;

namespace DSharpPlus.DocBot
{
    /// <summary>
    /// All of the D#+ types, methods and properties available in one static class.
    /// </summary>
    public static class CachedReflection
    {
        /// <summary>
        /// A collection of all DSharpPlus types, official extensions included.
        /// </summary>
        public static readonly Type[] Types;

        /// <summary>
        /// A collection of all DSharpPlus methods, grouped by the method name. Official extensions included.
        /// </summary>
        public static readonly Dictionary<string, MethodInfo[]> MethodGroups;

        /// <summary>
        /// A collection of all DSharpPlus properties, grouped by the property name. Official extensions included.
        /// </summary>
        public static readonly PropertyInfo[] Properties;

        /// <summary>
        /// Used by <see cref="ResolveGenericTypes(Type, StringBuilder?)"/> to convert CLR types into their C# representation.
        /// </summary>
        private static readonly CSharpCodeProvider CSharpCodeProvider = new();

        /// <summary>
        /// Cache all D#+ types, methods and properties on startup to prevent having to do it on every call. Holds a large memory footprint but grants us a lot of performance.
        /// </summary>
        static CachedReflection()
        {
            // Gather all D#+ types
            Types = typeof(DiscordClient).Assembly.ExportedTypes
                .Concat(typeof(CommandsNextExtension).Assembly.ExportedTypes)
                .Concat(typeof(InteractivityExtension).Assembly.ExportedTypes)
                .Concat(typeof(LavalinkExtension).Assembly.ExportedTypes)
                .Concat(typeof(SlashCommandsExtension).Assembly.ExportedTypes)
                .Concat(typeof(VoiceNextExtension).Assembly.ExportedTypes)
            .ToArray();

            // Gather all D#+ methods, grouping them by method name and grouping them by method overloads
            MethodGroups = Types
                .SelectMany(t => t
                    .GetMethods() // TODO: I'd like to use binding flags here as I feel it'd be more efficient however I haven't been able to understand how they work yet.
                    .Where(t => t.IsPublic // Public methods
                        && !t.IsSpecialName // Drop property getters/setters
                        && t.GetBaseDefinition().DeclaringType!.Namespace!.StartsWith("DSharpPlus"))) // Drop methods not implemented by us (aka object and Enum methods)
                .GroupBy(t => $"{t.DeclaringType!.Namespace}.{t.DeclaringType.Name}.{t.Name}") // Namespace.Class.Method
                .ToDictionary(t => t.Key, t => t.ToArray()); // Group method overloads by method name

            // I believe this grabs all public properties
            Properties = Types.SelectMany(t => t.GetProperties()).ToArray();
        }

        /// <summary>
        /// Turns a method into a string representation of it's return type, name and parameters. Generic types are resolved.
        /// </summary>
        /// <param name="methodInfo">The method to retrieve information from.</param>
        /// <returns>A string representation of a method's signature.</returns>
        public static string GetMethodSignature(MethodInfo methodInfo)
        {
            StringBuilder stringBuilder = new();
            stringBuilder.Append(ResolveGenericTypes(methodInfo.ReturnType));
            stringBuilder.Append(' ');
            stringBuilder.Append(methodInfo.Name);
            stringBuilder.Append('(');
            stringBuilder.Append(string.Join(", ", methodInfo.GetParameters().Select(x => ResolveGenericTypes(x.ParameterType) + " " + x.Name)));
            stringBuilder.Append(')');
            return stringBuilder.ToString();
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
                stringBuilder.Append(CSharpCodeProvider.GetTypeOutput(new(underlyingNullableType)).Split('.').Last() + "?");
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
                // Remove the last comma and space
                stringBuilder.Remove(stringBuilder.Length - 2, 2);
                stringBuilder.Append('>');
            }
            else
            {
                // As mentioned earlier, we use GetTypeOutput to get the full namespace for the type. We only want the type name.
                stringBuilder.Append(CSharpCodeProvider.GetTypeOutput(new(type)).Split('.').Last());
            }

            return stringBuilder.ToString();
        }
    }
}
