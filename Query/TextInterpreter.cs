using DSharpPlusDocs.Query.Results;
using System;
using System.Text.RegularExpressions;

namespace DSharpPlusDocs.Query
{
    public class TextInterpreter
    {
        private static readonly Regex rgx = new Regex("[^0-9a-z_ ]", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.ECMAScript);

        private string _text;
        public TextInterpreter(string text)
        {
            _text = $" {text} ";
        }

        public InterpreterResult Run()
        {
            //TODO: Better text parsing
            bool searchTypes = true, searchMethods = true, searchProperties = true, searchEvents = true, isList = false;
            SearchType search = SearchType.NONE;
            if (_text.IndexOf(" type ", StringComparison.OrdinalIgnoreCase) != -1 || _text.IndexOf(" method ", StringComparison.OrdinalIgnoreCase) != -1 || _text.IndexOf(" property ", StringComparison.OrdinalIgnoreCase) != -1 || _text.IndexOf(" event ", StringComparison.OrdinalIgnoreCase) != -1)
            {
                if (_text.IndexOf(" type ", StringComparison.OrdinalIgnoreCase) == -1)
                    searchTypes = false;
                if (_text.IndexOf(" method ", StringComparison.OrdinalIgnoreCase) == -1)
                    searchMethods = false;
                if (_text.IndexOf(" property ", StringComparison.OrdinalIgnoreCase) == -1)
                    searchProperties = false;
                if (_text.IndexOf(" event ", StringComparison.OrdinalIgnoreCase) == -1)
                    searchEvents = false;
                Regex rgx = new Regex("( property | method | type | event )", RegexOptions.IgnoreCase);
                _text = rgx.Replace(_text, " ");
            }
            if (_text.IndexOf(" list ", StringComparison.OrdinalIgnoreCase) != -1)
            {
                isList = true;
                Regex rgx = new Regex("( list )", RegexOptions.IgnoreCase);
                _text = rgx.Replace(_text, " ");
            }
            string nspace = null;
            int idx;
            if ((idx = _text.IndexOf(" in ", StringComparison.OrdinalIgnoreCase)) != -1)
            {
                search = SearchType.JUST_NAMESPACE;
                idx += 4;
                nspace = _text.Substring(idx);
                int idx2;
                if ((idx2 = nspace.IndexOf(' ')) != -1)
                    nspace = nspace.Substring(0, idx2);
                _text = _text.Replace($" in {nspace}", " ");
            }
            if (_text.Contains(".") && idx == -1)
            {
                if (search == SearchType.JUST_NAMESPACE)
                    return new InterpreterResult("You can't use both \"in\" and \".\" (dot) keywords.");
                nspace = _text.Substring(0, _text.LastIndexOf('.'));
                int lidx;
                if ((lidx = nspace.LastIndexOf(' ')) != -1)
                    nspace = nspace.Substring(lidx);
                _text = _text.Replace($"{nspace}.", "");
            }

            _text = rgx.Replace(_text.Trim(), "");
            if (nspace != null)
                nspace = rgx.Replace(nspace.Trim(), "");
            if (_text == "")
                return new InterpreterResult("No text to search.");

            return new InterpreterResult(_text, nspace, search, searchTypes, searchMethods, searchProperties, searchEvents, isList);
        }
    }
}
