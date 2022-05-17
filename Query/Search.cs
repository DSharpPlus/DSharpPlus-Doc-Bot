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
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using DSharpPlusDocs.Query.Results;
using DSharpPlusDocs.Query.Wrappers;

namespace DSharpPlusDocs.Query
{
    public class Search
    {
        private readonly InterpreterResult _result;
        private readonly Cache _cache;
        public Search(InterpreterResult result, Cache cache)
        {
            _result = result;
            _cache = cache;
        }

        public SearchResult<object> Run()
        {
            List<object> found = new();
            bool searchText = _result.Search is SearchType.All or SearchType.JustText;
            if (_result.SearchTypes)
            {
                found.AddRange(_cache.SearchTypes(_result.Text, !searchText));
            }

            if (_result.SearchMethods)
            {
                found.AddRange(_cache.SearchMethods(_result.Text, !searchText));
            }

            if (_result.SearchProperties)
            {
                found.AddRange(_cache.SearchProperties(_result.Text, !searchText));
            }

            if (_result.SearchEvents)
            {
                found.AddRange(_cache.SearchEvents(_result.Text, !searchText));
            }

            found = NamespaceFilter(found);
            return new SearchResult<object>(found);
        }

        private List<object> NamespaceFilter(List<object> oldList)
        {
            List<object> list = new();
            foreach (object o in oldList)
            {
                if (o is TypeInfoWrapper type /*&& !type.TypeInfo.Namespace.StartsWith("Discord.API")*/ && CompareNamespaces(type.TypeInfo.Namespace))
                {
                    list.Add(o);
                }
                else if (o is MethodInfoWrapper method /*&& !method.Parent.TypeInfo.Namespace.StartsWith("Discord.API")*/ && CompareNamespaces(method.Parent.TypeInfo))
                {
                    list.Add(o);
                }
                else if (o is PropertyInfoWrapper property /*&& !property.Parent.TypeInfo.Namespace.StartsWith("Discord.API")*/ && CompareNamespaces(property.Parent.TypeInfo))
                {
                    list.Add(o);
                }
                else if (o is EventInfoWrapper eve /*&& !eve.Parent.TypeInfo.Namespace.StartsWith("Discord.API")*/ && CompareNamespaces(eve.Parent.TypeInfo))
                {
                    list.Add(o);
                }
            }
            return list;
        }

        private bool CompareNamespaces(TypeInfo toCompare) => CompareNamespaces($"{toCompare.Namespace}.{toCompare.Name}");
        private bool CompareNamespaces(string toCompare)
        {
            if (_result.Namespace == null)
            {
                return true;
            }

            if (_result.Search is SearchType.All or SearchType.JustNamespace)
            {
                return toCompare.IndexOf(_result.Namespace, StringComparison.OrdinalIgnoreCase) != -1;
            }
            //Regex rgx = new Regex($"(\\.{_result.Namespace}\\b|\\b{_result.Namespace}\\.|\\b{_result.Namespace}\\b)", RegexOptions.IgnoreCase);
            Regex rgx = new($"(\\.{_result.Namespace}\\b|^{_result.Namespace}$)", RegexOptions.IgnoreCase);
            return rgx.IsMatch(toCompare);
        }
    }
}
