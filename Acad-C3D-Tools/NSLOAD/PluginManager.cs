using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;

using Exception = System.Exception;

namespace NSLOAD
{
    public static class PluginManager
    {
        private static readonly Dictionary<string, PluginRegistration> _plugins = new();

        public static PluginRegistrationBuilder Register(string pluginName)
        {
            return new PluginRegistrationBuilder(pluginName);
        }

        public static void Load(string pluginName)
        {
            var ed = GetEditor();
            try
            {
                var reg = GetRegistration(pluginName);

                if (reg.Host.IsLoaded)
                {
                    ed?.WriteMessage($"\n{pluginName} is already loaded.");
                    return;
                }

                if (string.IsNullOrEmpty(reg.DllPath))
                {
                    ed?.WriteMessage($"\n{pluginName} has no DLL path configured.");
                    return;
                }

                if (!File.Exists(reg.DllPath))
                {
                    ed?.WriteMessage($"\n{pluginName} DLL not found: {reg.DllPath}");
                    return;
                }

                LoadCore(reg);

                string cmdMsg = reg.Registrar != null
                    ? $" {reg.Registrar.CommandCount} commands registered."
                    : "";
                ed?.WriteMessage($"\n{pluginName} loaded.{cmdMsg}");
            }
            catch (Exception ex)
            {
                ed?.WriteMessage($"\n{pluginName} load error: {ex.Message}");
                ed?.WriteMessage($"\n{ex}");
            }
        }

        public static void Unload(string pluginName)
        {
            var ed = GetEditor();
            try
            {
                var reg = GetRegistration(pluginName);

                if (!reg.Host.IsLoaded)
                {
                    ed?.WriteMessage($"\n{pluginName} is not loaded.");
                    return;
                }

                TearDown(reg);
                ed?.WriteMessage($"\n{pluginName} unloaded.");
            }
            catch (Exception ex)
            {
                ed?.WriteMessage($"\n{pluginName} unload error: {ex.Message}");
                ed?.WriteMessage($"\n{ex}");
            }
        }

        public static void UnloadAll()
        {
            foreach (var reg in _plugins.Values)
            {
                try { TearDown(reg); }
                catch { }
            }
        }

        public static IReadOnlyList<string> GetRegisteredPluginNames()
            => _plugins.Keys.ToList();

        public static bool IsRegistered(string pluginName)
            => _plugins.ContainsKey(pluginName);

        public static bool IsLoaded(string pluginName)
            => _plugins.TryGetValue(pluginName, out var reg) && reg.Host.IsLoaded;

        public static void Unregister(string pluginName)
        {
            if (!_plugins.TryGetValue(pluginName, out var reg))
                return;

            TearDown(reg);
            _plugins.Remove(pluginName);
        }

        private static void LoadCore(PluginRegistration reg)
        {
            TearDown(reg);

            string pluginDir = Path.GetDirectoryName(reg.DllPath)!;
            var saConfig = SharedAssembliesConfigLoader.Load(pluginDir);
            string[] sharedNames = saConfig.SharedAssemblies?.ToArray() ?? Array.Empty<string>();
            var mixedSet = new HashSet<string>(
                saConfig.MixedModeAssemblies ?? new List<string>(),
                StringComparer.OrdinalIgnoreCase);
            var streamedSet = new HashSet<string>(
                saConfig.StreamedAssemblies ?? new List<string>(),
                StringComparer.OrdinalIgnoreCase);

            var ed = GetEditor();
            foreach (string asmName in sharedNames)
            {
                // External assemblies (recorded in AssemblyLocations) load from their
                // referenced dir (e.g. Appload); everything else from the plugin dir.
                string dir = saConfig.AssemblyLocations.TryGetValue(asmName, out var extDir)
                    ? extDir
                    : pluginDir;
                string dllPath = Path.Combine(dir, asmName + ".dll");
                if (!File.Exists(dllPath)) continue;

                // If a shared assembly is already in the default ALC — brought in by an
                // external loader (e.g. a Civil 3D object-enabler demand-load from its
                // own install path) or a previous load — bind to THAT instance; a second
                // LoadFrom of a different-path copy of the same name throws. Parity with
                // DevReload's SharedAssemblyPreloader.
                if (IsLoadedInDefaultAlc(asmName)) continue;

                if (mixedSet.Contains(asmName))
                {
                    EnsureRuntimeConfig(dllPath, asmName, ed);
                    Assembly.LoadFrom(dllPath);
                }
                else if (streamedSet.Contains(asmName))
                {
                    LoadSharedFromStream(dllPath);
                }
                else
                {
                    Assembly.LoadFrom(dllPath);
                }
            }

            var plugin = reg.Host.Load(reg.DllPath, sharedNames);

            if (reg.Registrar != null)
                reg.Registrar.RegisterFromAssembly(reg.Host.LoadedAssembly!);
        }

        private static void TearDown(PluginRegistration reg)
        {
            reg.Registrar?.UnregisterAll();

            if (reg.Host.IsLoaded)
            {
                try { reg.Host.Plugin?.Terminate(); }
                catch { }

                reg.Host.Unload();
            }
        }

        // Stream-loads a shared assembly INTO the default ALC.
        //
        // Must use AssemblyLoadContext.Default.LoadFromStream(...) — NOT
        // Assembly.Load(byte[]), which (per the documented .NET algorithm)
        // loads into a brand-new anonymous ALC and would be invisible to
        // name-based binding from the isolated plugin ALC.
        //
        // Default.LoadFromStream behaves like LoadFrom for binding (assembly
        // ends up in Default.Assemblies and is findable by name) but does
        // not lock the DLL on disk, so the developer can push a new build.
        // The running image stays loaded until AutoCAD restarts.
        private static void LoadSharedFromStream(string asmPath)
        {
            byte[] asmBytes = File.ReadAllBytes(asmPath);
            string pdbPath = Path.ChangeExtension(asmPath, ".pdb");
            using var asmStream = new MemoryStream(asmBytes);
            if (File.Exists(pdbPath))
            {
                byte[] pdbBytes = File.ReadAllBytes(pdbPath);
                using var pdbStream = new MemoryStream(pdbBytes);
                AssemblyLoadContext.Default.LoadFromStream(asmStream, pdbStream);
            }
            else
            {
                AssemblyLoadContext.Default.LoadFromStream(asmStream);
            }
        }

        private static void EnsureRuntimeConfig(string asmPath, string asmName, Editor? ed)
        {
            string asmDir = Path.GetDirectoryName(asmPath)!;
            string rcPath = Path.Combine(asmDir, asmName + ".runtimeconfig.json");
            if (!File.Exists(rcPath))
            {
                ed?.WriteMessage($"\n[NSLOAD] Creating runtimeconfig.json for mixed-mode: {asmName}");
                File.WriteAllText(rcPath,
                    """
                    {
                      "runtimeOptions": {
                        "tfm": "net8.0",
                        "framework": {
                          "name": "Microsoft.NETCore.App",
                          "version": "8.0.0"
                        }
                      }
                    }
                    """);
            }

            string ijwPath = Path.Combine(asmDir, "Ijwhost.dll");
            if (!File.Exists(ijwPath))
                ed?.WriteMessage($"\n[NSLOAD] WARNING: Ijwhost.dll not found in {asmDir}");
        }

        // True when an assembly with this simple name is already present in the default
        // ALC (an external demand-load at startup, or a previous plugin load). An ALC
        // holds at most one assembly per simple name; once present, name-based binding
        // from the collectible plugin ALC already resolves to it, so any further load is
        // a no-op at best and a hard error at worst (different on-disk path, same name).
        private static bool IsLoadedInDefaultAlc(string simpleName)
        {
            foreach (var asm in AssemblyLoadContext.Default.Assemblies)
            {
                if (string.Equals(
                        asm.GetName().Name, simpleName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static PluginRegistration GetRegistration(string pluginName)
        {
            if (!_plugins.TryGetValue(pluginName, out var reg))
                throw new InvalidOperationException(
                    $"Plugin '{pluginName}' is not registered.");
            return reg;
        }

        private static Editor? GetEditor()
        {
            return Application.DocumentManager.MdiActiveDocument?.Editor;
        }

        internal static void AddRegistration(PluginRegistration reg)
        {
            _plugins[reg.PluginName] = reg;
        }
    }

    internal class PluginRegistration
    {
        public required string PluginName { get; init; }
        public required string DllPath { get; init; }
        public required string[] SharedAssemblyNames { get; init; }

        public PluginHost<IExtensionApplication> Host { get; } = new();
        public CommandRegistrar? Registrar { get; init; }
    }

    public class PluginRegistrationBuilder
    {
        private readonly string _pluginName;
        private string? _dllPath;
        private string[] _sharedAssemblyNames = Array.Empty<string>();
        private bool _useCommands;

        internal PluginRegistrationBuilder(string pluginName)
        {
            _pluginName = pluginName;
        }

        public PluginRegistrationBuilder WithDllPath(string dllPath)
        {
            _dllPath = dllPath;
            return this;
        }

        public PluginRegistrationBuilder WithCommands()
        {
            _useCommands = true;
            return this;
        }

        public PluginRegistrationBuilder WithSharedAssemblies(params string[] assemblyNames)
        {
            _sharedAssemblyNames = assemblyNames;
            return this;
        }

        public void Commit()
        {
            var reg = new PluginRegistration
            {
                PluginName = _pluginName,
                DllPath = _dllPath ?? "",
                SharedAssemblyNames = _sharedAssemblyNames,
                Registrar = _useCommands ? new CommandRegistrar() : null,
            };

            PluginManager.AddRegistration(reg);
        }
    }
}
