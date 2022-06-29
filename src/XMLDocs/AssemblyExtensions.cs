using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Linq;

namespace DocBot.src.XMLDocs
{
    public static class ReflectionExtensions
    {
        private static readonly Dictionary<Assembly, List<XMLMember>> memberLists = new();

        public static void LoadXmlDocFile(this Assembly assembly, string path)
        {
            //create new entry in dictionary, return if already present
            if (!memberLists.ContainsKey(assembly))
            {
                memberLists.Add(assembly, new List<XMLMember>());
            }
            else
            {
                Console.WriteLine($"Already loaded docs for assembly {assembly.FullName}! Returning.");
                return;
            }

            if (!File.Exists(path))
            {
                Console.WriteLine($"Could not find File {path}! Throwing.");
                throw new FileNotFoundException($"XMLDocs file could not be found at path {path}.", path);
            }

            //load file and find "doc" node
            XElement? doc = XDocument.Load(path).Element("doc");
            if (doc is null)
            {
                return;
            }
            //find members node
            XElement? members = doc.Element("members");
            if (members is null)
            {
                return;
            }

            //iterate nodes in members node
            foreach (XElement element in members.Elements())
            {
                //get content of child nodes
                string? summary = element.Element("summary")?.Value.Trim();
                string? remarks = element.Element("remarks")?.Value.Trim();
                string? returns = element.Element("returns")?.Value.Trim();
                XMLMember member = new()
                {
                    //read node attribute "name"
                    Name = element.Attribute("name")?.Value,
                    //sanitize and set properties
                    Summary = string.IsNullOrWhiteSpace(summary) ? null : Regex.Replace(summary, @"\r?\n *", Environment.NewLine),
                    Remarks = string.IsNullOrWhiteSpace(remarks) ? null : Regex.Replace(remarks, @"\r?\n *", Environment.NewLine),
                    Returns = string.IsNullOrWhiteSpace(returns) ? null : Regex.Replace(returns, @"\r?\n *", Environment.NewLine),
                    //create dicts
                    Exceptions = new(),
                    Params = new(),
                    TypeParams = new()
                };

                //iterate exception child nodes
                foreach (XElement exc in element.Elements("exception"))
                {
                    //read "cref" attribute
                    XAttribute? cref = exc.Attribute("cref");
                    if (cref is null)
                    {
                        continue;
                    }
                    //add exception with type as key, and descrtiption as value
                    member.Exceptions.Add(cref.Value, Regex.Replace(exc.Value.Trim(), @"\r?\n *", Environment.NewLine));
                }
                //iterate param child nodes
                foreach (XElement para in element.Elements("param"))
                {
                    //read "name" attribute
                    XAttribute? name = para.Attribute("name");
                    if (name is null)
                    {
                        continue;
                    }
                    //add param with name as key and description as value
                    member.Params.Add(name.Value, Regex.Replace(para.Value.Trim(), @"\r?\n *", Environment.NewLine));
                }
                //iterate typeparam child nodes
                foreach (XElement tPara in element.Elements("typeparam"))
                {
                    //read "name" attribute
                    XAttribute? name = tPara.Attribute("name");
                    if (name is null)
                    {
                        continue;
                    }
                    //add param with name as key and description as value
                    member.TypeParams.Add(name.Value, Regex.Replace(tPara.Value.Trim(), @"\r?\n *", Environment.NewLine));
                }

                //add member to list
                memberLists[assembly].Add(member);
            }
        }

        public static XMLMember? GetDocs(this MemberInfo member)
        {
            char prefixCode;
            Assembly? assembly;
            string? memberName;
            if (member is Type type)
            {
                assembly = type.Assembly;
                memberName = type.FullName;
            }
            else
            {
                assembly = member.DeclaringType?.Assembly;
                memberName = member.DeclaringType?.FullName + "." + member.Name;
            }

			if (!memberLists.ContainsKey(assembly))
			{
				return null;
			}

            switch (member.MemberType)
            {
                case MemberTypes.Constructor:
                    memberName = memberName?.Replace(".ctor", "#ctor");
                    goto case MemberTypes.Method; //fall through
                case MemberTypes.Method:
                    prefixCode = 'M';
                    string paramTypesList = string.Join(",", 
                        ((MethodBase)member)
                            .GetParameters()
                            .Cast<ParameterInfo>()
                            .Select(x => x.ParameterType.FullName)
                            .ToArray());
                    if (!string.IsNullOrEmpty(paramTypesList))
                    {
                        memberName += "(" + paramTypesList + ")";
                    }
                    break;
                case MemberTypes.Event:
                    prefixCode = 'E';
                    break;
                case MemberTypes.Field:
                    prefixCode = 'F';
                    break;
                case MemberTypes.NestedType:
                    memberName = memberName?.Replace('+', '.');
                    goto case MemberTypes.TypeInfo; //fall through
                case MemberTypes.TypeInfo:
                    prefixCode = 'T';
                    break;
                case MemberTypes.Property:
                    prefixCode = 'P';
                    break;
                default:
                    throw new ArgumentException("Unknown member type", nameof(member));
            }

            string search = string.Format("{0}:{1}", prefixCode, memberName);
            return assembly is null ? null : memberLists[assembly].FirstOrDefault(x => x.Name == search);
        }
    }
}
