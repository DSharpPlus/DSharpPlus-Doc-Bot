using System.Linq;
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
        private static void FormatProperty(XmlMemberInfo memberInfo, PropertyInfo propertyInfo, DiscordEmbedBuilder embedBuilder)
        {
            embedBuilder.AddField("Declaration", Formatter.BlockCode(GetPropertySignature(propertyInfo), "cs"));
            if (propertyInfo.ReflectedType != null)
            {
                embedBuilder.AddField("Type", Formatter.InlineCode(ResolveGenericTypes(propertyInfo.ReflectedType)));
            }
        }

        private static string GetPropertySignature(PropertyInfo property)
        {
            StringBuilder stringBuilder = new();
            _ = ResolveGenericTypes(property.PropertyType, stringBuilder);
            stringBuilder.Append(' ');
            stringBuilder.Append(property.Name);
            if (property.GetMethod == null && property.SetMethod == null)
            {
                stringBuilder.Append(';');
                return stringBuilder.ToString();
            }

            stringBuilder.Append(" { ");
            if (property.GetMethod != null)
            {
                if (property.GetMethod.IsPublic)
                {
                    stringBuilder.Append("get; ");
                }
                else if (property.GetMethod.IsAssembly)
                {
                    stringBuilder.Append("internal get; ");
                }
                else if (property.GetMethod.IsPrivate)
                {
                    stringBuilder.Append("private get; ");
                }
            }

            if (property.SetMethod != null)
            {
                if (property.SetMethod.IsFamily)
                {
                    stringBuilder.Append("protected ");
                }

                if (property.SetMethod.IsAssembly)
                {
                    stringBuilder.Append("internal ");
                }
                else if (property.SetMethod.IsPrivate)
                {
                    stringBuilder.Append("private ");
                }

                if (property.SetMethod.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit)))
                {
                    stringBuilder.Append("init; ");
                }
                else
                {
                    stringBuilder.Append("set; ");
                }
            }

            stringBuilder.Append('}');
            return stringBuilder.ToString();
        }
    }
}
