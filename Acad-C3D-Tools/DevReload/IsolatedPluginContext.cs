using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Loader;

namespace DevReload
{
    public class IsolatedPluginContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;
        private readonly HashSet<string> _sharedAssemblies;

        public IsolatedPluginContext(string pluginPath, params string[] sharedAssemblyNames)
            : base("PluginIsolated", isCollectible: true)
        {
            _resolver = new AssemblyDependencyResolver(pluginPath);
            _sharedAssemblies = new HashSet<string>(sharedAssemblyNames);
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            // Keep shared interface assemblies in default context for type identity
            if (_sharedAssemblies.Contains(assemblyName.Name!))
                return null;

            // Use deps.json to resolve isolated dependencies
            string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            if (assemblyPath != null)
                return LoadFromAssemblyPath(assemblyPath);

            // Return null -> falls back to default context
            // AutoCAD assemblies, .NET framework, and WPF types are shared this way
            return null;
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            string? libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            if (libraryPath != null)
                return LoadUnmanagedDllFromPath(libraryPath);
            return IntPtr.Zero;
        }
    }
}
