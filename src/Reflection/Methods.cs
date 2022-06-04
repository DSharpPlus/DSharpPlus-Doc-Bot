using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace DSharpPlus.DocBot
{
    public partial class CachedReflection
    {
        [Flags]
        public enum MethodSignatureFormat
        {
            IncludeModifiers,
            IncludeReturnType,
            IncludeParameters,
            IncludeParameterName,
            IncludeParameterDefaultValues,
            Full = int.MaxValue // All the flags
        }

        private static readonly MethodSignatureFormat[] CachedMethodSignatureFormats = Enum.GetValues(typeof(MethodSignatureFormat)).Cast<MethodSignatureFormat>().ToArray();

        public static string[] GetMethodNames(Type type, int maxStringLength)
        {
            foreach (MethodInfo methodInfo in MethodGroups.Values.Select(methods => methods[0]).Where(method => method.DeclaringType == type))
            {
                string methodName = GetMethodSignature(methodInfo, MethodSignatureFormat.Full);
            }
        }

        public static string GetMethodSignature(MethodInfo method, MethodSignatureFormat methodSignatureFormat = MethodSignatureFormat.Full)
        {
            StringBuilder stringBuilder = new();
            MethodInfo? baseMethod = null;
            if (methodSignatureFormat.HasFlag(MethodSignatureFormat.IncludeModifiers))
            {
                if (method.IsStatic)
                {
                    stringBuilder.Append("static ");
                }

                if (method.IsVirtual)
                {
                    stringBuilder.Append("virtual ");
                }

                if (method.IsAbstract)
                {
                    stringBuilder.Append("abstract ");
                }

                baseMethod = method.GetBaseDefinition();
                if (baseMethod != method)
                {
                    stringBuilder.Append("override ");
                }
            }

            if (methodSignatureFormat.HasFlag(MethodSignatureFormat.IncludeReturnType))
            {
                // Appends the (generic) type arguments since we're passing our current string builder, which means we don't need the result.
                _ = ResolveGenericTypes(method.ReturnType, stringBuilder);
                stringBuilder.Append(' ');
            }

            // Show which method we're overriding
            if (baseMethod != null)
            {
                stringBuilder.Append(baseMethod.DeclaringType!.Name);
                stringBuilder.Append('.');
            }
            stringBuilder.Append(method.Name);
            stringBuilder.Append('(');

            if (methodSignatureFormat.HasFlag(MethodSignatureFormat.IncludeParameters))
            {
                foreach (ParameterInfo parameterInfo in method.GetParameters())
                {
                    // When the method is an extension method
                    if (method.IsDefined(typeof(ExtensionAttribute), true) && parameterInfo.Position == 0)
                    {
                        stringBuilder.Append("this ");
                    }

                    _ = ResolveGenericTypes(parameterInfo.ParameterType, stringBuilder);

                    if (methodSignatureFormat.HasFlag(MethodSignatureFormat.IncludeParameterName))
                    {
                        stringBuilder.Append(' ');
                        stringBuilder.Append(parameterInfo.Name);
                        if (methodSignatureFormat.HasFlag(MethodSignatureFormat.IncludeParameterDefaultValues))
                        {
                            if (parameterInfo.HasDefaultValue)
                            {
                                stringBuilder.Append(" = ");
                                stringBuilder.Append(parameterInfo.DefaultValue?.ToString() ?? "null");
                            }
                        }
                    }
                    stringBuilder.Append(", ");
                }

                if (stringBuilder[^1] == ' ' && stringBuilder[^2] == ',') // EndsWith(", ")
                {
                    stringBuilder.Remove(stringBuilder.Length - 2, 2);
                }
            }
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
