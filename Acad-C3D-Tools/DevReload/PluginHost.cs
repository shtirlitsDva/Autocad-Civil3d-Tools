using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace DevReload
{
    public class StalePluginException : Exception
    {
        public StalePluginException(string message) : base(message) { }
    }

    public class PluginHost<TPlugin> where TPlugin : class
    {
        private IsolatedPluginContext? _context;

        public bool IsLoaded => _context != null;
        public TPlugin? Plugin { get; private set; }
        public Assembly? LoadedAssembly { get; private set; }

        public TPlugin Load(string assemblyPath, params string[] sharedAssemblyNames)
        {
            if (_context != null)
                Unload();

            _context = new IsolatedPluginContext(assemblyPath, sharedAssemblyNames);

            byte[] asmBytes = File.ReadAllBytes(assemblyPath);
            Assembly pluginAssembly;

            using (var asmStream = new MemoryStream(asmBytes))
            {
                string pdbPath = Path.ChangeExtension(assemblyPath, ".pdb");
                if (File.Exists(pdbPath))
                {
                    byte[] pdbBytes = File.ReadAllBytes(pdbPath);
                    using var pdbStream = new MemoryStream(pdbBytes);
                    pluginAssembly = _context.LoadFromStream(asmStream, pdbStream);
                }
                else
                {
                    pluginAssembly = _context.LoadFromStream(asmStream);
                }
            }

            LoadedAssembly = pluginAssembly;

            Type? pluginType = null;
            Type[] exportedTypes;
            try
            {
                exportedTypes = pluginAssembly.GetExportedTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                exportedTypes = ex.Types.Where(t => t != null).ToArray()!;
            }

            foreach (Type type in exportedTypes)
            {
                if (typeof(TPlugin).IsAssignableFrom(type) && !type.IsAbstract)
                {
                    pluginType = type;
                    break;
                }
            }

            if (pluginType == null)
            {
                bool nameMatch = exportedTypes.Any(t =>
                    t.GetInterfaces().Any(i => i.Name == typeof(TPlugin).Name));
                if (nameMatch)
                    throw new StalePluginException(
                        $"{typeof(TPlugin).Name} found by name but compiled against a different " +
                        $"DevReload.Interface version — rebuilding.");
                throw new InvalidOperationException(
                    $"Could not find {typeof(TPlugin).Name} implementation in " +
                    $"{Path.GetFileName(assemblyPath)}");
            }

            Plugin = (TPlugin)Activator.CreateInstance(pluginType)!;
            return Plugin;
        }

        public void Unload()
        {
            Plugin = null;
            LoadedAssembly = null;

            _context?.Unload();
            _context = null;

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }
}
