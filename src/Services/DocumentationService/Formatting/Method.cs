using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using DSharpPlus.DocBot.Interfaces;
using DSharpPlus.DocBot.Types;
using DSharpPlus.Entities;

namespace DSharpPlus.DocBot.Services
{
    public partial class DocumentationService : IDocumentationService
    {
        private static void FormatMethod(XmlMemberInfo memberInfo, MethodInfo methodInfo, DiscordEmbedBuilder embedBuilder)
        {
            embedBuilder.AddField("Declaration", Formatter.BlockCode(GetMethodSignature(methodInfo, MethodSignatureFormat.Full), "cs"));
            foreach (ParameterInfo parameter in methodInfo.GetParameters())
            {
            }
        }

        [Flags]
        private enum MethodSignatureFormat
        {
            IncludeModifiers,
            IncludeReturnType,
            IncludeParameters,
            IncludeParameterName,
            IncludeParameterDefaultValues,
            Full = int.MaxValue // All the flags
        }

        private static string GetMethodSignature(MethodInfo method, MethodSignatureFormat methodSignatureFormat = MethodSignatureFormat.Full)
        {
            StringBuilder stringBuilder = new();
            MethodInfo baseMethod = method.GetBaseDefinition();
            if (methodSignatureFormat.HasFlag(MethodSignatureFormat.IncludeModifiers))
            {
                if (method.IsStatic)
                {
                    stringBuilder.Append("static ");
                }

                if (method.IsVirtual && baseMethod == method)
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
                stringBuilder.Append(GetFriendlyTypeName(baseMethod.ReflectedType!));
                stringBuilder.Append('.');
            }
            stringBuilder.Append(method.Name);
            stringBuilder.Append('(');

            if (methodSignatureFormat.HasFlag(MethodSignatureFormat.IncludeParameters))
            {
                foreach (ParameterInfo parameterInfo in method.GetParameters())
                {
                    stringBuilder.Append(FormatAttributes(parameterInfo.GetCustomAttributesData(), AttributeFormat.ShowParameters));

                    // When the method is an extension method
                    if (method.IsDefined(typeof(ExtensionAttribute), true) && parameterInfo.Position == 0)
                    {
                        stringBuilder.Append("this ");
                    }
                    else if (parameterInfo.IsDefined(typeof(ParamArrayAttribute)))
                    {
                        stringBuilder.Append("params ");
                    }
                    else if (parameterInfo.IsOut)
                    {
                        stringBuilder.Append("out ");
                    }
                    else if (parameterInfo.IsIn)
                    {
                        stringBuilder.Append("in ");
                    }

                    _ = ResolveGenericTypes(parameterInfo.ParameterType, stringBuilder);
                    if (parameterInfo.IsOptional)
                    {
                        stringBuilder.Append('?');
                    }

                    if (methodSignatureFormat.HasFlag(MethodSignatureFormat.IncludeParameterName))
                    {
                        stringBuilder.Append(' ');
                        stringBuilder.Append(parameterInfo.Name);
                        if (methodSignatureFormat.HasFlag(MethodSignatureFormat.IncludeParameterDefaultValues))
                        {
                            if (parameterInfo.HasDefaultValue)
                            {
                                stringBuilder.Append(" = ");
                                stringBuilder.Append(FormatNullableObject(parameterInfo.DefaultValue));
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
