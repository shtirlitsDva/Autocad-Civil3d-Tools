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
            string name = assemblyName.Name ?? "";

            // Shared assemblies stay in default ALC for type identity (WPF XAML etc.).
            //
            // PluginManager loads them via LoadFrom or via
            // AssemblyLoadContext.Default.LoadFromStream — both put the assembly
            // into the Default ALC. The explicit lookup below is belt-and-braces:
            // returning null and letting the default binder resolve also works, but
            // handing the runtime the resolved instance is unambiguous.
            //
            // Note: Assembly.Load(byte[]) — which we explicitly DO NOT use — would
            // put the assembly in a brand-new anonymous ALC, where it would be
            // invisible to default-binder name resolution.
            if (_sharedAssemblies.Contains(name))
            {
                foreach (var asm in AssemblyLoadContext.Default.Assemblies)
                {
                    if (string.Equals(
                            asm.GetName().Name, name, StringComparison.OrdinalIgnoreCase))
                        return asm;
                }
                return null;
            }

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
