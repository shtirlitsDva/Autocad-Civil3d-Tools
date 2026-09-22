using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;

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

                if (reg.Plugin.IsLoaded)
                {
                    ed?.WriteMessage($"\n{pluginName} is already loaded.");
                    return;
                }

                if (string.IsNullOrEmpty(reg.DllPath))
                {
                    ed?.WriteMessage($"\n{pluginName} has no path configured.");
                    return;
                }

                if (!File.Exists(reg.DllPath))
                {
                    ed?.WriteMessage($"\n{pluginName} not found: {reg.DllPath}");
                    return;
                }

                reg.Plugin.Load(line => ed?.WriteMessage($"\n{line}"));
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

                if (!reg.Plugin.IsLoaded)
                {
                    ed?.WriteMessage($"\n{pluginName} is not loaded.");
                    return;
                }

                reg.Plugin.Unload(line => ed?.WriteMessage($"\n{line}"));
            }
            catch (Exception ex)
            {
                ed?.WriteMessage($"\n{pluginName} unload error: {ex.Message}");
                ed?.WriteMessage($"\n{ex}");
            }
        }

        /// <summary>AutoCAD is shutting down: let every plugin do its exit work.</summary>
        public static void UnloadAll()
        {
            foreach (var reg in _plugins.Values)
            {
                try { reg.Plugin.Shutdown(); }
                catch { }
            }
        }

        public static IReadOnlyList<string> GetRegisteredPluginNames()
            => _plugins.Keys.ToList();

        public static bool IsRegistered(string pluginName)
            => _plugins.ContainsKey(pluginName);

        public static bool IsLoaded(string pluginName)
            => _plugins.TryGetValue(pluginName, out var reg) && reg.Plugin.IsLoaded;

        /// <summary>The plugin's version line for the manager palette, or null
        /// when it has none (managed plugins, or an unregistered name).</summary>
        public static string? GetVersionStatus(string pluginName)
            => _plugins.TryGetValue(pluginName, out var reg) ? reg.Plugin.VersionStatus : null;

        public static void Unregister(string pluginName)
        {
            if (!_plugins.TryGetValue(pluginName, out var reg))
                return;

            if (reg.Plugin.IsLoaded)
                Unload(pluginName);
            _plugins.Remove(pluginName);
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
        public required ILoadablePlugin Plugin { get; init; }
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
            string path = _dllPath ?? "";
            var reg = new PluginRegistration
            {
                PluginName = _pluginName,
                DllPath = path,
                SharedAssemblyNames = _sharedAssemblyNames,
                Plugin = PluginKinds.Create(
                    _pluginName, path, _useCommands ? new CommandRegistrar() : null),
            };

            PluginManager.AddRegistration(reg);
        }
    }
}
