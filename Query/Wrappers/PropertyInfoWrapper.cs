using System.Reflection;

namespace DSharpPlusDocs.Query.Wrappers
{
    public class PropertyInfoWrapper
    {
        public PropertyInfo Property { get; private set; }
        public TypeInfoWrapper Parent { get; private set; }
        public PropertyInfoWrapper(TypeInfoWrapper parent, PropertyInfo property)
        {
            Parent = parent;
            Property = property;
        }
    }
}
