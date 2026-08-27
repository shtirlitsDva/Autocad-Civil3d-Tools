using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace NSLOAD
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

            // Same two branches AutoCAD's own loader takes: the assembly-level
            // [ExtensionApplication] names the type outright, and only without
            // one does the first non-abstract exported implementation win. An
            // assembly can export several implementations, so scanning where the
            // author declared a type would pick a different one than AutoCAD did.
            Type? declared = pluginAssembly
                .GetCustomAttributes<Autodesk.AutoCAD.Runtime.ExtensionApplicationAttribute>()
                .FirstOrDefault()?.Type;

            if (declared != null)
            {
                if (typeof(TPlugin).IsAssignableFrom(declared) && !declared.IsAbstract)
                    pluginType = declared;
            }
            else
            {
                foreach (Type type in exportedTypes)
                {
                    if (typeof(TPlugin).IsAssignableFrom(type) && !type.IsAbstract)
                    {
                        pluginType = type;
                        break;
                    }
                }
            }

            if (pluginType == null)
                throw new InvalidOperationException(
                    $"Could not find {typeof(TPlugin).Name} implementation in " +
                    $"{Path.GetFileName(assemblyPath)}");

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
