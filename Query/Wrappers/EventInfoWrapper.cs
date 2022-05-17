using System.Reflection;

namespace DSharpPlusDocs.Query.Wrappers
{
    public class EventInfoWrapper
    {
        public EventInfo Event { get; private set; }
        public TypeInfoWrapper Parent { get; private set; }
        public EventInfoWrapper(TypeInfoWrapper parent, EventInfo e)
        {
            Parent = parent;
            Event = e;
        }
    }
}
