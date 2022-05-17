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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using DSharpPlus;
using DSharpPlusDocs.Query.Wrappers;

namespace DSharpPlusDocs.Query
{
    public class CacheBag
    {
        public ConcurrentBag<MethodInfo> Methods;
        public ConcurrentBag<PropertyInfo> Properties;
        public ConcurrentBag<EventInfo> Events;
        public CacheBag()
        {
            Methods = new ConcurrentBag<MethodInfo>();
            Properties = new ConcurrentBag<PropertyInfo>();
            Events = new ConcurrentBag<EventInfo>();
        }
        public CacheBag(CacheBag cb)
        {
            Methods = new ConcurrentBag<MethodInfo>(cb.Methods);
            Properties = new ConcurrentBag<PropertyInfo>(cb.Properties);
            Events = new ConcurrentBag<EventInfo>(cb.Events);
        }
    }
    public class Cache
    {
        private readonly ConcurrentDictionary<TypeInfoWrapper, CacheBag> allTypes;
        private readonly ConcurrentDictionary<TypeInfoWrapper, ConcurrentBag<MethodInfo>> extensions;
        private int methodCount, propertyCount, extensionMethods, eventCount;
        private bool ready;
        public Cache()
        {
            allTypes = new ConcurrentDictionary<TypeInfoWrapper, CacheBag>();
            extensions = new ConcurrentDictionary<TypeInfoWrapper, ConcurrentBag<MethodInfo>>();
            ready = false;
            methodCount = propertyCount = extensionMethods = eventCount = 0;
        }

        public void Initialize()
        {
            methodCount = propertyCount = 0;
            ready = false;
            Populate();
            ready = true;
            methodCount = allTypes.Sum(x => x.Value.Methods.Count);
            propertyCount = allTypes.Sum(x => x.Value.Properties.Count);
            extensionMethods = extensions.Sum(x => x.Value.Count);
            eventCount = allTypes.Sum(x => x.Value.Events.Count);
        }

        public int GetTypeCount() => allTypes.Count;

        public int GetMethodCount() => methodCount;

        public int GetPropertyCount() => propertyCount;

        public int GetEventCount() => eventCount;

        public int GetExtensionTypesCount() => extensions.Count;

        public int GetExtensioMethodsCount() => extensionMethods;

        public CacheBag GetCacheBag(TypeInfoWrapper type)
        {
            if (!allTypes.ContainsKey(type))
            {
                return null;
            }

            CacheBag cb = new CacheBag(allTypes[type]);
            foreach (ConcurrentBag<MethodInfo> bag in extensions.Values)
            {
                foreach (MethodInfo mi in bag)
                {
                    if (CheckTypeAndInterfaces(type, mi.GetParameters().FirstOrDefault()?.ParameterType.GetTypeInfo()))
                    {
                        cb.Methods.Add(mi);
                    }
                }
            }

            return cb;
        }

        private bool CheckTypeAndInterfaces(TypeInfoWrapper toBeChecked, TypeInfo toSearch) => CheckTypeAndInterfaces(toBeChecked.TypeInfo, toSearch);
        private bool CheckTypeAndInterfaces(TypeInfo toBeChecked, TypeInfo toSearch)
        {
            if (toSearch == null || toBeChecked == null)
            {
                return false;
            }

            if (toBeChecked == toSearch)
            {
                return true;
            }

            foreach (Type type in toBeChecked.GetInterfaces())
            {
                if (CheckTypeAndInterfaces(type.GetTypeInfo(), toSearch))
                {
                    return true;
                }
            }

            return false;
        }

        public List<TypeInfoWrapper> SearchTypes(string name, bool exactName = true) => allTypes.Keys.Where(x => exactName ? x.DisplayName.ToLower() == name.ToLower() : SearchFunction(name, x.DisplayName.ToLower())).ToList();

        public List<MethodInfoWrapper> SearchMethods(string name, bool exactName = true)
        {
            List<MethodInfoWrapper> result = new List<MethodInfoWrapper>();
            foreach (TypeInfoWrapper type in allTypes.Keys)
            {
                result.AddRange(GetCacheBag(type).Methods.Where(x => exactName ? x.Name.ToLower() == name.ToLower() : SearchFunction(name, x.Name.ToLower())).Select(x => new MethodInfoWrapper(type, x)));
            }

            return result;
        }

        public List<PropertyInfoWrapper> SearchProperties(string name, bool exactName = true)
        {
            List<PropertyInfoWrapper> result = new List<PropertyInfoWrapper>();
            foreach (TypeInfoWrapper type in allTypes.Keys)
            {
                result.AddRange(GetCacheBag(type).Properties.Where(x => exactName ? x.Name.ToLower() == name.ToLower() : SearchFunction(name, x.Name.ToLower())).Select(x => new PropertyInfoWrapper(type, x)));
            }

            return result;
        }

        public List<EventInfoWrapper> SearchEvents(string name, bool exactName = true)
        {
            List<EventInfoWrapper> result = new List<EventInfoWrapper>();
            foreach (TypeInfoWrapper type in allTypes.Keys)
            {
                result.AddRange(GetCacheBag(type).Events.Where(x => exactName ? x.Name.ToLower() == name.ToLower() : SearchFunction(name, x.Name.ToLower())).Select(x => new EventInfoWrapper(type, x)));
            }

            return result;
        }

        private bool SearchFunction(string searchString, string objectName)
        {
            foreach (string s in searchString.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (objectName.IndexOf(s, StringComparison.OrdinalIgnoreCase) == -1)
                {
                    return false;
                }
            }

            return true;
        }

        private void Populate()
        {
            foreach (AssemblyName a in Assembly.GetEntryAssembly().GetReferencedAssemblies())
            {
                if (a.Name.StartsWith("DSharpPlus") /*& !a.Name.StartsWith("DSharpPlus.Addons")*/)
                {
                    foreach (Type type in Assembly.Load(a).GetExportedTypes())
                    {
                        LoadType(type);
                    }
                }
            }
        }

        private void LoadType(Type type)
        {
            if (type.IsGenericParameter)
            {
                type = type.GetGenericTypeDefinition();
            }

            if (!CheckNamespace(type.Namespace))
            {
                return;
            }

            if (allTypes.Keys.FirstOrDefault(x => x.TypeInfo == type.GetTypeInfo()) == null)
            {
                TypeInfoWrapper tiw = new TypeInfoWrapper(type);
                CacheBag cb = new CacheBag();
                allTypes[tiw] = cb;
                foreach (MethodInfo mi in type.GetRuntimeMethods())
                {
                    if (CheckNamespace(mi.DeclaringType.Namespace) && (mi.IsPublic || mi.IsFamily) && !mi.IsSpecialName && !cb.Methods.Contains(mi))
                    {
                        cb.Methods.Add(mi);
                    }

                    if (mi.IsDefined(typeof(ExtensionAttribute), false) && mi.IsStatic && mi.IsPublic)
                    {
                        if (extensions.Keys.FirstOrDefault(x => x.TypeInfo == type.GetTypeInfo()) == null)
                        {
                            extensions[tiw] = new ConcurrentBag<MethodInfo>(new MethodInfo[] { mi });
                        }
                        else
                        {
                            extensions[tiw].Add(mi);
                        }
                    }
                }
                IEnumerable<PropertyInfo> rt = type.GetRuntimeProperties();
                foreach (PropertyInfo pi in type.GetRuntimeProperties())
                {
                    if ((pi.GetMethod.IsFamily || pi.GetMethod.IsPublic) && !cb.Properties.Any(x => x.Name == pi.Name))
                    {
                        cb.Properties.Add(pi);
                    }
                }

                foreach (EventInfo ei in type.GetRuntimeEvents())
                {
                    cb.Events.Add(ei);
                }

                if (type.GetTypeInfo().IsInterface)
                {
                    foreach (Type t in type.GetInterfaces())
                    {
                        LoadInterface(t, tiw);
                    }
                }
            }
        }

        private void LoadInterface(Type _interface, TypeInfoWrapper parent)
        {
            if (CheckNamespace(_interface.Namespace))
            {
                LoadType(_interface);
            }

            foreach (MethodInfo mi in _interface.GetRuntimeMethods())
            {
                if (CheckNamespace(mi.DeclaringType.Namespace) && (mi.IsPublic || mi.IsFamily) && !mi.IsSpecialName && !allTypes[parent].Methods.Contains(mi))
                {
                    if (!allTypes[parent].Methods.Contains(mi))
                    {
                        allTypes[parent].Methods.Add(mi);
                    }
                }
            }

            foreach (PropertyInfo pi in _interface.GetRuntimeProperties())
            {
                if (!allTypes[parent].Properties.Contains(pi) && !allTypes[parent].Properties.Any(x => x.Name == pi.Name))
                {
                    allTypes[parent].Properties.Add(pi);
                }
            }

            foreach (EventInfo ei in _interface.GetRuntimeEvents())
            {
                if (!allTypes[parent].Events.Contains(ei))
                {
                    allTypes[parent].Events.Add(ei);
                }
            }

            foreach (Type type in _interface.GetInterfaces())
            {
                LoadInterface(type, parent);
            }
        }

        public bool IsReady() => ready;

        private bool CheckNamespace(string ns) => ns.StartsWith("DSharpPlus") && !ns.StartsWith("DSharpPlusDocs");
    }
}
