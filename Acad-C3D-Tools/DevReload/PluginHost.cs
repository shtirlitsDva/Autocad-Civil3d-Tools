using System;
using System.IO;
using System.Reflection;

namespace DevReload
{
    public class PluginHost<TPlugin> where TPlugin : class
    {
        private IsolatedPluginContext? _context;

        public bool IsLoaded => _context != null;
        public TPlugin? Plugin { get; private set; }

        public TPlugin Load(string assemblyPath, params string[] sharedAssemblyNames)
        {
            if (_context != null)
                Unload();

            _context = new IsolatedPluginContext(assemblyPath, sharedAssemblyNames);
            Assembly pluginAssembly = _context.LoadFromAssemblyPath(assemblyPath);

            Type? pluginType = null;
            foreach (Type type in pluginAssembly.GetExportedTypes())
            {
                if (typeof(TPlugin).IsAssignableFrom(type) && !type.IsAbstract)
                {
                    pluginType = type;
                    break;
                }
            }

            if (pluginType == null)
                throw new InvalidOperationException(
                    $"Could not find {typeof(TPlugin).Name} implementation in {Path.GetFileName(assemblyPath)}");

            Plugin = (TPlugin)Activator.CreateInstance(pluginType)!;
            return Plugin;
        }

        public void Unload()
        {
            Plugin = null;

            _context?.Unload();
            _context = null;

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }
}
