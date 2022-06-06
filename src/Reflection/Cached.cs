using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.CSharp;

namespace DSharpPlus.DocBot
{
    public static partial class CachedReflection
    {
        /// <summary>
        /// A collection of all DSharpPlus types, official extensions included.
        /// </summary>
        public static Type[] Types { get; private set; }

        /// <summary>
        /// A collection of all DSharpPlus methods, grouped by the method name. Official extensions included.
        /// </summary>
        public static Dictionary<string, MethodInfo[]> MethodGroups { get; private set; }

        /// <summary>
        /// A collection of all DSharpPlus properties, grouped by the property name. Official extensions included.
        /// </summary>
        public static PropertyInfo[] Properties { get; private set; }

        /// <summary>
        /// A collection of all DSharpPlus events, grouped by the event name. Official extensions included.
        /// </summary>
        public static EventInfo[] Events { get; private set; }

        /// <summary>
        /// Used by <see cref="ResolveGenericTypes(Type, StringBuilder?)"/> to convert CLR types into their C# representation.
        /// </summary>
        private static readonly CSharpCodeProvider CSharpCodeProvider = new();

        static CachedReflection() => DownloadNightliesAsync().GetAwaiter().GetResult();

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

                if (stringBuilder[^1] == ' ' && stringBuilder[^2] == ',') // EndsWith(", ")
                {
                    stringBuilder.Remove(stringBuilder.Length - 2, 2);
                }
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
