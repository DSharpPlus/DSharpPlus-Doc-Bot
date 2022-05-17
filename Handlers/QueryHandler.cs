using System.Threading.Tasks;
using DSharpPlus.Entities;
using DSharpPlusDocs.Query;
using DSharpPlusDocs.Query.Results;

namespace DSharpPlusDocs.Handlers
{
    public class QueryHandler
    {
        public Cache Cache { get; private set; }
        public static string DocsBaseUrl { get; set; } = "https://dsharpplus.emzi0767.com/";

        public QueryHandler()
        {
            Cache = new Cache();
        }

        public void Initialize()
        {
            Cache.Initialize();
        }

        public async Task<(string, object)> RunAsync(string text)
        {
            var interpreterResult = new TextInterpreter(text).Run();
            if (!interpreterResult.IsSuccess)
                return ($"{interpreterResult.Error}", null);

            object result;
            if (interpreterResult.Search == SearchType.JUST_NAMESPACE)
                result = await SearchAsync(interpreterResult, SearchType.NONE) ?? await SearchAsync(interpreterResult, SearchType.JUST_NAMESPACE) ?? await SearchAsync(interpreterResult, SearchType.JUST_TEXT) ?? await SearchAsync(interpreterResult, SearchType.ALL);
            else
                result = await SearchAsync(interpreterResult, SearchType.NONE) ?? await SearchAsync(interpreterResult, SearchType.JUST_TEXT) ?? await SearchAsync(interpreterResult, SearchType.JUST_NAMESPACE) ?? await SearchAsync(interpreterResult, SearchType.ALL);

            return result == null ? ($"No results found for `{text}`.", null) : ("", result);
        }

        private async Task<object> SearchAsync(InterpreterResult interpreterResult, SearchType type)
        {
            interpreterResult.Search = type;
            var searchResult = new Search(interpreterResult, Cache).Run();
            if (searchResult.Count != 0)
                return await new ResultDisplay(searchResult, Cache, interpreterResult.IsList).RunAsync();
            return null;
        }

        public bool IsReady()
        {
            return Cache.IsReady();
        }
    }
}
