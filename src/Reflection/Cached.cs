using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DSharpPlus.CommandsNext;
using DSharpPlus.Interactivity;
using DSharpPlus.Lavalink;
using DSharpPlus.SlashCommands;
using DSharpPlus.VoiceNext;
using Microsoft.CSharp;

namespace DSharpPlus.DocBot
{
    public static partial class CachedReflection
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
        /// A collection of all DSharpPlus events, grouped by the event name. Official extensions included.
        /// </summary>
        public static readonly EventInfo[] Events;

        /// <summary>
        /// Used by <see cref="ResolveGenericTypes(Type, StringBuilder?)"/> to convert CLR types into their C# representation.
        /// </summary>
        private static readonly CSharpCodeProvider CSharpCodeProvider = new();

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
                .SelectMany(type => type
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy | BindingFlags.DeclaredOnly) // TODO: I'd like to use binding flags here as I feel it'd be more efficient however I haven't been able to understand how they work yet.
                    .Where(method => method.GetBaseDefinition().DeclaringType?.Namespace?.StartsWith("DSharpPlus") ?? false)) // Drop methods not implemented by us (aka object and Enum methods)
                .GroupBy(method => method.Name) // Method name
                .ToDictionary(method => method.Key, method => method.ToArray()); // Group method overloads by method name

            // I believe this grabs all public properties
            Properties = Types.SelectMany(t => t.GetProperties()).ToArray();

            // Grab all events from all extensions
            // Not even sure if these binding flags are needed...
            Events = Types.SelectMany(t => t.GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)).ToArray();
        }
    }
}
