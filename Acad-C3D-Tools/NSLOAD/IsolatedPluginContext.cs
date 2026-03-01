using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Loader;

namespace NSLOAD
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
            if (_sharedAssemblies.Contains(assemblyName.Name!))
                return null;

            string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            if (assemblyPath != null)
                return LoadFromAssemblyPath(assemblyPath);

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
