using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DSharpPlusDocs.Query.Results
{
    public class DocsHttpResult
    {
        public string Url { get; private set; }
        public string Summary { get; private set; }
        public string Example { get; private set; }
        public DocsHttpResult(string url, string summary = null, string example = null)
        {
            Url = url;
            Summary = summary == "" ? null : summary;
            Example = example == "" ? null : example;
        }
    }
}
