using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;

namespace NSLOAD
{
    /// <summary>
    /// A .NET plugin DLL, stream-loaded into its own collectible load context so
    /// the file on the share is never locked.
    /// </summary>
    internal sealed class ManagedPlugin : ILoadablePlugin
    {
        private readonly string _pluginName;
        private readonly string _dllPath;
        private readonly CommandRegistrar? _registrar;
        private readonly PluginHost<IExtensionApplication> _host = new();

        public ManagedPlugin(string pluginName, string dllPath, CommandRegistrar? registrar)
        {
            _pluginName = pluginName;
            _dllPath = dllPath;
            _registrar = registrar;
        }

        public bool IsLoaded => _host.IsLoaded;

        public string? VersionStatus => null;

        public void Load(Action<string> say)
        {
            LoadCore();

            string cmdMsg = _registrar != null
                ? $" {_registrar.CommandCount} commands registered."
                : "";
            say($"{_pluginName} loaded.{cmdMsg}");
        }

        public void Unload(Action<string> say)
        {
            TearDown();
            say($"{_pluginName} unloaded.");
        }

        public void Shutdown() => TearDown();

        private void LoadCore()
        {
            TearDown();

            string pluginDir = Path.GetDirectoryName(_dllPath)!;
            var saConfig = SharedAssembliesConfigLoader.Load(pluginDir);
            string[] sharedNames = saConfig.SharedAssemblies?.ToArray() ?? Array.Empty<string>();
            var mixedSet = new HashSet<string>(
                saConfig.MixedModeAssemblies ?? new List<string>(),
                StringComparer.OrdinalIgnoreCase);
            var streamedSet = new HashSet<string>(
                saConfig.StreamedAssemblies ?? new List<string>(),
                StringComparer.OrdinalIgnoreCase);

            var ed = Application.DocumentManager.MdiActiveDocument?.Editor;
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

            var plugin = _host.Load(_dllPath, sharedNames);

            // Before the commands, matching the order AutoCAD's own scan used:
            // it initialized the plugin during LoadFromStream, and NSLOAD
            // registered commands afterwards. Guarded, because when suppression
            // is off the host has already called this and a second call would
            // initialize the plugin twice.
            if (AutoCadScanSuppressor.IsActive)
                plugin.Initialize();

            if (_registrar != null)
                _registrar.RegisterFromAssembly(_host.LoadedAssembly!);
        }

        private void TearDown()
        {
            _registrar?.UnregisterAll();

            if (_host.IsLoaded)
            {
                try { _host.Plugin?.Terminate(); }
                catch (System.Exception ex)
                {
                    // The plugin is unloaded regardless; its own cleanup failing
                    // must not stop that, but it must not pass unheard either.
                    NsLoadDiagnostics.Report($"{_pluginName} Terminate", ex);
                }

                _host.Unload();
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
    }
}
