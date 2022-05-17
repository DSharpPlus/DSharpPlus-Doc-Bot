using System.Reflection;

namespace DSharpPlusDocs.Query.Wrappers
{
    public class MethodInfoWrapper
    {
        public MethodInfo Method { get; private set; }
        public TypeInfoWrapper Parent { get; private set; }
        public MethodInfoWrapper(TypeInfoWrapper parent, MethodInfo method)
        {
            Parent = parent;
            Method = method;
        }
    }
}
