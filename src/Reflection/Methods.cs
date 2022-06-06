using System;
using System.Collections.Generic;
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

        public static IEnumerable<string> GetMethodNames(Type type, int maxStringLength)
        {
            foreach (MethodInfo methodInfo in MethodGroups.Values.Select(methods => methods[0]).Where(method => method.DeclaringType == type))
            {
                string methodName = GetMethodSignature(methodInfo, MethodSignatureFormat.Full);
                if (methodName.Length < maxStringLength)
                {
                    yield return methodName;
                }

                methodName = GetMethodSignature(methodInfo, MethodSignatureFormat.IncludeReturnType | MethodSignatureFormat.IncludeParameters);
                if (methodName.Length < maxStringLength)
                {
                    yield return methodName;
                }

                yield return methodName[0..Math.Min(methodName.Length, maxStringLength - 3)] + "...";
            }
        }

        public static string GetMethodSignature(MethodInfo method, MethodSignatureFormat methodSignatureFormat = MethodSignatureFormat.Full)
        {
            StringBuilder stringBuilder = new();
            MethodInfo baseMethod = method.GetBaseDefinition();
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
            if (baseMethod != method)
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
    }
}
