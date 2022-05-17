namespace DSharpPlusDocs.Query.Results
{
    public class InterpreterResult
    {
        public string Text { get; internal set; }
        public string Namespace { get; internal set; }
        public bool SearchTypes { get; internal set; }
        public bool SearchMethods { get; internal set; }
        public bool SearchProperties { get; internal set; }
        public bool SearchEvents { get; internal set; }
        public bool IsList { get; internal set; }
        public SearchType Search { get; internal set; }
        public bool IsSuccess { get; internal set; }
        public string Error { get; internal set; }
        public InterpreterResult(string text, string nspace = null, SearchType search = SearchType.NONE, bool searchTypes = true, bool searchMethods = true, bool searchProperties = true, bool searchEvents = true, bool isList = false)
        {
            Text = text;
            Namespace = nspace;
            SearchTypes = searchTypes;
            SearchMethods = searchMethods;
            SearchProperties = searchProperties;
            SearchEvents = searchEvents;
            IsList = isList;
            Search = search;
            Error = null;
            IsSuccess = true;
        }

        public InterpreterResult(string error)
        {
            Error = error;
            IsSuccess = false;
        }
    }
}
