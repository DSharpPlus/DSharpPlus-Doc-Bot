using System;
using System.IO;
using System.Reflection;

namespace DSharpPlus.DocBot.Types
{
    public sealed class AssemblyLoadInfo
    {
        public Assembly Assembly { get; init; }
        public string? Version { get; init; }
        public string? XmlDocPath { get; init; }

        public AssemblyLoadInfo(Assembly assembly, string? xmlDocPath)
        {
            ArgumentNullException.ThrowIfNull(assembly);
            if (!File.Exists(xmlDocPath))
            {
                throw new FileNotFoundException("XML Documentation file does not exist", xmlDocPath);
            }

            Assembly = assembly;
            Version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            XmlDocPath = xmlDocPath;
        }
    }
}
