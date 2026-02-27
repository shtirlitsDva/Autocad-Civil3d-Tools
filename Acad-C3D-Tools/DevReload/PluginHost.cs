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
        public Assembly? LoadedAssembly { get; private set; }

        public TPlugin Load(string assemblyPath, params string[] sharedAssemblyNames)
        {
            if (_context != null)
                Unload();

            _context = new IsolatedPluginContext(assemblyPath, sharedAssemblyNames);

            // Load main DLL from stream — file is NOT locked after reading
            // AssemblyDependencyResolver still resolves NuGet deps via .deps.json on disk
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
            LoadedAssembly = null;

            _context?.Unload();
            _context = null;

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }
}
