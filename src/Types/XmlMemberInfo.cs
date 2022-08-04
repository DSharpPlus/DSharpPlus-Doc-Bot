using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace DSharpPlus.DocBot.Types
{
    public sealed class XmlMemberInfo
    {
        private static readonly Regex WhitespaceRegex = new(@"^[ \t]+", RegexOptions.Compiled);

        public MemberInfo MemberInfo { get; init; } = null!;
        public string Name { get; init; }
        public string? Summary { get; init; }
        public string? Remarks { get; init; }
        public string? Returns { get; init; }
        public IReadOnlyDictionary<string, string> Params { get; init; } = new Dictionary<string, string>();
        public IReadOnlyDictionary<string, string> TypeParams { get; init; } = new Dictionary<string, string>();
        public IReadOnlyDictionary<string, string> Exceptions { get; init; } = new Dictionary<string, string>();
        public IReadOnlyList<Type> InheritedTypes { get; init; } = new List<Type>();

        [SuppressMessage("Roslyn", "IDE0045", Justification = "Conditional expression rabbit hole.")]
        public XmlMemberInfo(MemberInfo memberInfo, IEnumerable<Type> inheritedOnlyTypes, string? summary = null, string? remarks = null, string? returns = null, IDictionary<string, string>? parameters = null, IDictionary<string, string>? typeParameters = null, IDictionary<string, string>? exceptions = null)
        {
            ArgumentNullException.ThrowIfNull(memberInfo);
            ArgumentNullException.ThrowIfNull(inheritedOnlyTypes);

            MemberInfo = memberInfo;
            if (memberInfo is TypeInfo typeInfo)
            {
                Name = typeInfo.FullName!;
            }
            else if (memberInfo.ReflectedType == null)
            {
                Name = memberInfo.Name;
            }
            else
            {
                Name = $"{memberInfo.ReflectedType.FullName}.{memberInfo.Name}";
            }

            InheritedTypes = new List<Type>(inheritedOnlyTypes);

            if (summary != null)
            {
                Summary = WhitespaceRegex.Replace(summary, "\n").Trim();
            }

            if (remarks != null)
            {
                Remarks = WhitespaceRegex.Replace(remarks, "\n").Trim();
            }

            if (returns != null)
            {
                Returns = WhitespaceRegex.Replace(returns, "\n").Trim();
            }

            if (parameters != null && parameters.Count != 0)
            {
                foreach (KeyValuePair<string, string> kvp in parameters)
                {
                    parameters[kvp.Key] = WhitespaceRegex.Replace(kvp.Value, "\n").Trim();
                }
                Params = new ReadOnlyDictionary<string, string>(parameters);
            }

            if (typeParameters != null && typeParameters.Count != 0)
            {
                foreach (KeyValuePair<string, string> kvp in typeParameters)
                {
                    typeParameters[kvp.Key] = WhitespaceRegex.Replace(kvp.Value, "\n").Trim();
                }
                TypeParams = new ReadOnlyDictionary<string, string>(typeParameters);
            }

            if (exceptions != null && exceptions.Count != 0)
            {
                foreach (KeyValuePair<string, string> kvp in exceptions)
                {
                    exceptions[kvp.Key] = WhitespaceRegex.Replace(kvp.Value, "\n").Trim();
                }
                Exceptions = new ReadOnlyDictionary<string, string>(exceptions);
            }
        }

        /// <summary>
        /// Iterates through an Assembly's exported types and members, parsing them into <see cref="XmlMemberInfo"/>s. Additionally looks through the assembly's documentation file for additional information.
        /// </summary>
        /// <param name="assembly">The type to parse.</param>
        /// <param name="relatedAssemblies">A complete <see cref="IEnumerable{Type}"/> of all assembly's exported types.</param>
        /// <param name="xmlFile">The XML file associated with <paramref name="type"/>.</param>
        /// <returns>A </returns>
        public static IEnumerable<XmlMemberInfo> Parse(Assembly assembly, IEnumerable<Assembly>? relatedAssemblies, string? xmlFile = null)
        {
            // You paid for the whole RAM, you're gonna use the whole RAM!
            List<MemberInfo> memberInfos = new();

            static bool filterSpecialAndNonDSharpPlusMembers(MemberInfo x)
            {
                return !x.IsDefined(typeof(SpecialNameAttribute)) && (x.ReflectedType?.FullName?.Contains("DSharpPlus") ?? false);
            }

            foreach (Type type in assembly.ExportedTypes)
            {
                memberInfos.Add(type);
                memberInfos.AddRange(type.GetProperties().Where(filterSpecialAndNonDSharpPlusMembers));
                memberInfos.AddRange(type.GetMethods(BindingFlags.Public).Where(filterSpecialAndNonDSharpPlusMembers));
                memberInfos.AddRange(type.GetEvents(BindingFlags.Public).Where(filterSpecialAndNonDSharpPlusMembers));
                memberInfos.AddRange(type.GetFields().Where(filterSpecialAndNonDSharpPlusMembers));
            }

            XDocument? xmlDocument = xmlFile != null ? XDocument.Load(xmlFile) : null;
            List<XmlMemberInfo> xmlMemberInfos = new();
            foreach (MemberInfo memberInfo in memberInfos)
            {
                List<Type> inheritedTypes = new();
                if (memberInfo is Type memberType && relatedAssemblies != null && relatedAssemblies.Any())
                {
                    foreach (Type completeType in relatedAssemblies.SelectMany(relatedAssembly => relatedAssembly.ExportedTypes))
                    {
                        if (completeType.IsAssignableFrom(memberType) && !completeType.IsInterface && completeType != memberType)
                        {
                            inheritedTypes.Add(completeType);
                        }
                    }
                }

                string xmlifiedMember = XmlifyMember(memberInfo);
                XElement? element = xmlDocument?.Element("doc")?.Element("members")?.Elements()?.FirstOrDefault(e => e.Attribute("name")?.Value == xmlifiedMember);
                XmlMemberInfo xmlMemberInfo = new(memberInfo, inheritedTypes,
                    summary: element?.Element("summary")?.Value,
                    remarks: element?.Element("remarks")?.Value,
                    returns: element?.Element("returns")?.Value,
                    parameters: element?.Element("param")?.Elements()?.ToDictionary(parameter => parameter.Attribute("name")!.Value, e => e.Value),
                    typeParameters: element?.Element("typeparam")?.Elements()?.ToDictionary(typeParameter => typeParameter.Attribute("name")!.Value, e => e.Value),
                    exceptions: element?.Element("exceptions")?.Elements()?.ToDictionary(exception => exception.Attribute("cref")!.Value, e => e.Value)
                );

                xmlMemberInfos.Add(xmlMemberInfo);
            };

            return xmlMemberInfos;
        }

        private static string XmlifyMember(MemberInfo memberInfo)
        {
            StringBuilder stringBuilder = new();
            switch (memberInfo)
            {
                case MethodBase:
                    stringBuilder.Append("M:");
                    goto default;
                case Type:
                    stringBuilder.Append("T:");
                    goto default;
                case FieldInfo:
                    stringBuilder.Append("F:");
                    goto default;
                case PropertyInfo:
                    stringBuilder.Append("P:");
                    goto default;
                case EventInfo:
                    stringBuilder.Append("E:");
                    goto default;
                default:
                    return stringBuilder.Append(FullNameMember(memberInfo)).ToString();
            }
        }

        private static string FullNameMember(MemberInfo memberInfo)
        {
            StringBuilder stringBuilder = new();
            if (memberInfo is Type typeInfo)
            {
                stringBuilder.Append(typeInfo.FullName);
            }
            else
            {
                if (memberInfo.ReflectedType != null)
                {
                    stringBuilder.Append(FullNameMember(memberInfo.ReflectedType));
                    stringBuilder.Append('.');
                }
                stringBuilder.Append(memberInfo is ConstructorInfo ? "#ctor" : memberInfo.Name);
            }

            if (memberInfo is MethodBase methodBase)
            {
                stringBuilder.Append('(');
                ParameterInfo[] parameters = methodBase.GetParameters();
                for (int i = 0; i < parameters.Length; i++)
                {
                    stringBuilder.Append(parameters[i].ParameterType.FullName);
                    if (i != parameters.Length - 1)
                    {
                        stringBuilder.Append(',');
                    }
                }
                stringBuilder.Append(')');
            }

            return stringBuilder.ToString();
        }
    }
}
