using System;
using System.Reflection;
using System.Runtime.Loader;

namespace DSharpPlus.DocBot.Types
{
    public sealed class DocumentationLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver Resolver;

        public DocumentationLoadContext(string assemblyPath) => Resolver = new AssemblyDependencyResolver(assemblyPath);

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            string? assemblyPath = Resolver.ResolveAssemblyToPath(assemblyName);
            return assemblyPath != null ? LoadFromAssemblyPath(assemblyPath) : null;
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            string? libraryPath = Resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            return libraryPath != null ? LoadUnmanagedDllFromPath(libraryPath) : IntPtr.Zero;
        }
    }
}
