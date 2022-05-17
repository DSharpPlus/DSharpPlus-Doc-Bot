using System;
using System.Reflection;

namespace DSharpPlusDocs.Query.Wrappers
{
    public class TypeInfoWrapper
    {
        public TypeInfo TypeInfo { get; private set; }
        public string DisplayName { get; private set; }
        public string Name { get; private set; }
        public TypeInfoWrapper(Type type)
        {
            TypeInfo = type.GetTypeInfo();
            DisplayName = Utils.BuildType(type);
            Name = type.Name;
            int idx;
            if ((idx = Name.IndexOf('`')) != -1)
                Name = Name.Substring(0, idx);
        }
    }
}
