using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using DSharpPlus.DocBot.Interfaces;

namespace DSharpPlus.DocBot.Services
{
    public partial class DocumentationService : IDocumentationService
    {
        [Flags]
        private enum AttributeFormat
        {
            Declaration,
            ShowParameters
        }

        private static string FormatAttributes(IEnumerable<CustomAttributeData> customAttributeData, AttributeFormat attributeFormat)
        {
            IEnumerable<CustomAttributeData> trueCustomAttributes = customAttributeData.Where(attribute =>
                attribute.AttributeType != typeof(OptionalAttribute)
                && attribute.AttributeType != typeof(ParamArrayAttribute)
                && attribute.AttributeType != typeof(InAttribute)
                && attribute.AttributeType != typeof(OutAttribute)
                && (attribute.AttributeType.Name != "NullableAttribute" || !(attribute.AttributeType.Namespace?.Contains("System.Runtime") ?? true))
            );

            if (!attributeFormat.HasFlag(AttributeFormat.Declaration) || !trueCustomAttributes.Any())
            {
                return string.Empty;
            }

            StringBuilder stringBuilder = new();
            stringBuilder.Append("\n  [");
            foreach (CustomAttributeData attribute in trueCustomAttributes)
            {
                // Should support generic attributes without issue, when supported by the compiler.
                string attributeName = GetFriendlyTypeName(attribute.AttributeType);
                stringBuilder.Append(attributeName.Remove(attributeName.LastIndexOf("Attribute")));
                if (attributeFormat.HasFlag(AttributeFormat.ShowParameters) && (attribute.ConstructorArguments.Count != 0 || attribute.NamedArguments.Count != 0))
                {
                    stringBuilder.Append('(');
                    // [Attribute(1, 2, 3)]
                    foreach (CustomAttributeTypedArgument constructorArgument in attribute.ConstructorArguments)
                    {
                        stringBuilder.Append(FormatNullableObject(constructorArgument.Value));
                        stringBuilder.Append(", ");
                    }

                    // [Attribute(Name = "value")]
                    foreach (CustomAttributeNamedArgument namedArgument in attribute.NamedArguments)
                    {
                        stringBuilder.Append(namedArgument.MemberName);
                        stringBuilder.Append(" = ");
                        stringBuilder.Append(FormatNullableObject(namedArgument.TypedValue.Value));
                        stringBuilder.Append(", ");
                    }

                    // Remove the last command and space (parameter)
                    stringBuilder.Remove(stringBuilder.Length - 2, 2);
                    stringBuilder.Append(')');
                }

                stringBuilder.Append(", ");
            }
            // Remove the last command and space (attribute)
            stringBuilder.Remove(stringBuilder.Length - 2, 2);
            stringBuilder.Append("] ");

            return stringBuilder.ToString();
        }
    }
}
