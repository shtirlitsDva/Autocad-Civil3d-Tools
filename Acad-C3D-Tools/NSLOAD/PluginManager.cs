using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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

            var plugin = reg.Host.Load(reg.DllPath, reg.SharedAssemblyNames);

            if (reg.Registrar != null)
                reg.Registrar.RegisterFromAssembly(reg.Host.LoadedAssembly!);

            plugin.Initialize();
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
