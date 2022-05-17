using System.Collections.Generic;

namespace DSharpPlusDocs.Query.Results
{
    public class SearchResult<T> where T : class
    {
        public int Count { get { return List?.Count ?? -1; } }
        public List<T> List { get; private set; }
        public SearchResult(List<T> list)
        {
            List = list;
        }
    }
}
