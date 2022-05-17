// This file is part of the DSharpPlus project.
//
// Copyright (c) 2015 Mike Santiago
// Copyright (c) 2016-2022 DSharpPlus Contributors
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Text.RegularExpressions;
using DSharpPlusDocs.Query.Results;

namespace DSharpPlusDocs.Query
{
    public class TextInterpreter
    {
        private static readonly Regex rgx = new("[^0-9a-z_ ]", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.ECMAScript);

        private string _text;
        public TextInterpreter(string text) => _text = $" {text} ";

        public InterpreterResult Run()
        {
            //TODO: Better text parsing
            bool searchTypes = true, searchMethods = true, searchProperties = true, searchEvents = true, isList = false;
            SearchType search = SearchType.None;
            if (_text.IndexOf(" type ", StringComparison.OrdinalIgnoreCase) != -1 || _text.IndexOf(" method ", StringComparison.OrdinalIgnoreCase) != -1 || _text.IndexOf(" property ", StringComparison.OrdinalIgnoreCase) != -1 || _text.IndexOf(" event ", StringComparison.OrdinalIgnoreCase) != -1)
            {
                if (!_text.Contains(" type ", StringComparison.OrdinalIgnoreCase))
                {
                    searchTypes = false;
                }

                if (!_text.Contains(" method ", StringComparison.OrdinalIgnoreCase))
                {
                    searchMethods = false;
                }

                if (!_text.Contains(" property ", StringComparison.OrdinalIgnoreCase))
                {
                    searchProperties = false;
                }

                if (!_text.Contains(" event ", StringComparison.OrdinalIgnoreCase))
                {
                    searchEvents = false;
                }

                Regex rgx = new("( property | method | type | event )", RegexOptions.IgnoreCase);
                _text = rgx.Replace(_text, " ");
            }
            if (_text.IndexOf(" list ", StringComparison.OrdinalIgnoreCase) != -1)
            {
                isList = true;
                Regex rgx = new("( list )", RegexOptions.IgnoreCase);
                _text = rgx.Replace(_text, " ");
            }
            string nspace = null;
            int idx;
            if ((idx = _text.IndexOf(" in ", StringComparison.OrdinalIgnoreCase)) != -1)
            {
                search = SearchType.JustNamespace;
                idx += 4;
                nspace = _text[idx..];
                int idx2;
                if ((idx2 = nspace.IndexOf(' ')) != -1)
                {
                    nspace = nspace[..idx2];
                }

                _text = _text.Replace($" in {nspace}", " ");
            }
            if (_text.Contains('.') && idx == -1)
            {
                if (search == SearchType.JustNamespace)
                {
                    return new InterpreterResult("You can't use both \"in\" and \".\" (dot) keywords.");
                }

                nspace = _text[.._text.LastIndexOf('.')];
                int lidx;
                if ((lidx = nspace.LastIndexOf(' ')) != -1)
                {
                    nspace = nspace[lidx..];
                }

                _text = _text.Replace($"{nspace}.", "");
            }

            _text = rgx.Replace(_text.Trim(), "");
            if (nspace != null)
            {
                nspace = rgx.Replace(nspace.Trim(), "");
            }

            return _text == ""
                ? new InterpreterResult("No text to search.")
                : new InterpreterResult(_text, nspace, search, searchTypes, searchMethods, searchProperties, searchEvents, isList);
        }
    }
}
