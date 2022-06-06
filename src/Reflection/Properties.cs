using System.Reflection;
using System.Text;

namespace DSharpPlus.DocBot
{
    public partial class CachedReflection
    {
        public static string GetPropertySignature(PropertyInfo property)
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
                if (property.SetMethod.IsPublic)
                {
                    stringBuilder.Append("set; ");
                }
                else if (property.SetMethod.IsAssembly)
                {
                    stringBuilder.Append("internal set; ");
                }
                else if (property.SetMethod.IsPrivate)
                {
                    stringBuilder.Append("private set; ");
                }
            }

            stringBuilder.Append('}');
            return stringBuilder.ToString();
        }
    }
}
