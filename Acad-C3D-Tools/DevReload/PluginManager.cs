using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Internal;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;

using Exception = System.Exception;

namespace DevReload
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

                if (reg.Host.IsLoaded && reg.PaletteSet != null)
                {
                    reg.PaletteSet.Visible = true;
                    return;
                }

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

                string dllPath = reg.DllPath;

                if (!File.Exists(dllPath))
                {
                    if (reg.VsProjectName == null)
                    {
                        ed?.WriteMessage($"\n{pluginName} DLL not found and no VS project configured.");
                        return;
                    }
                    ed?.WriteMessage($"\n{pluginName} DLL not found, building...");
                    string? builtPath = DevReloadService.FindAndBuild(reg.VsProjectName, ed);
                    if (builtPath == null) return;
                    dllPath = builtPath;
                }

                LoadCore(reg, dllPath);

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

        public static void DevReload(string pluginName)
        {
            var ed = GetEditor();
            try
            {
                var reg = GetRegistration(pluginName);

                if (reg.VsProjectName == null)
                {
                    ed?.WriteMessage(
                        $"\n{pluginName} has no VS project configured for dev-reload.");
                    return;
                }

                string? dllPath = DevReloadService.FindAndBuild(reg.VsProjectName, ed);
                if (dllPath == null) return;

                LoadCore(reg, dllPath);

                string cmdMsg = reg.Registrar != null
                    ? $" {reg.Registrar.CommandCount} commands registered."
                    : "";
                ed?.WriteMessage($"\n{pluginName} dev-reloaded.{cmdMsg}");
            }
            catch (Exception ex)
            {
                ed?.WriteMessage($"\n{pluginName} dev-reload error: {ex.Message}");
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
                catch { /* best-effort during shutdown */ }
            }
        }

        // ── Public query + management API ─────────────────────────────

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
            UnregisterLoaderCommands(reg);
            _plugins.Remove(pluginName);
        }

        // ── Loader-level command registration ─────────────────────────

        public static void RegisterLoaderCommands(string pluginName, string prefix)
        {
            if (!_plugins.TryGetValue(pluginName, out var reg))
                return;

            prefix = prefix.ToUpperInvariant();
            string group = "DEVRELOAD";
            string name = pluginName;

            void Register(string suffix, Action action)
            {
                string cmdName = prefix + suffix;
                CommandCallback cb = () => action();
                Utils.AddCommand(group, cmdName, cmdName, CommandFlags.Modal, cb);
                reg.LoaderCommands.Add((group, cmdName, cb));
            }

            Register("LOAD", () => Load(name));
            Register("DEV", () => DevReload(name));
            Register("UNLOAD", () => Unload(name));
        }

        private static void UnregisterLoaderCommands(PluginRegistration reg)
        {
            foreach (var (group, cmdName, _) in reg.LoaderCommands)
                Utils.RemoveCommand(group, cmdName);
            reg.LoaderCommands.Clear();
        }

        // ── Private helpers ────────────────────────────────────────────

        /// <summary>
        /// Core load sequence: tear down old → load new from stream →
        /// register commands → create palette.
        /// AutoCAD calls IExtensionApplication.Initialize() automatically.
        /// DevReload does NOT call Initialize().
        /// </summary>
        private static void LoadCore(PluginRegistration reg, string dllPath)
        {
            TearDown(reg);

            var plugin = reg.Host.Load(dllPath, reg.SharedAssemblyNames);

            if (reg.Registrar != null)
            {
                reg.Registrar.RegisterFromAssembly(reg.Host.LoadedAssembly!);
            }

            var paletteObj = (plugin as IPluginPalette)?.CreatePaletteSet();
            if (paletteObj is PaletteSet ps)
            {
                reg.PaletteSet = ps;
                ps.Visible = true;
                ps.Size = reg.PaletteSize;
                ps.Dock = reg.DockSide;
            }
        }

        private static void TearDown(PluginRegistration reg)
        {
            ClosePaletteSet(reg);
            reg.Registrar?.UnregisterAll();

            if (reg.Host.IsLoaded)
            {
                try { reg.Host.Plugin?.Terminate(); }
                catch { /* best-effort */ }

                reg.Host.Unload();
            }
        }

        private static void ClosePaletteSet(PluginRegistration reg)
        {
            if (reg.PaletteSet != null)
            {
                reg.PaletteSet.Close();
                reg.PaletteSet = null;
            }
        }

        private static PluginRegistration GetRegistration(string pluginName)
        {
            if (!_plugins.TryGetValue(pluginName, out var reg))
                throw new InvalidOperationException(
                    $"Plugin '{pluginName}' is not registered. " +
                    $"Call PluginManager.Register(\"{pluginName}\")...Commit() first.");
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
        public required string? VsProjectName { get; init; }
        public required string[] SharedAssemblyNames { get; init; }
        public required Size PaletteSize { get; init; }
        public required DockSides DockSide { get; init; }

        public PluginHost<IPlugin> Host { get; } = new();
        public CommandRegistrar? Registrar { get; init; }
        public PaletteSet? PaletteSet { get; set; }

        public List<(string Group, string Name, CommandCallback Callback)> LoaderCommands { get; }
            = new();
    }

    public class PluginRegistrationBuilder
    {
        private readonly string _pluginName;
        private string? _dllPath;
        private string? _vsProjectName;
        private string[] _sharedAssemblyNames = new[] { "DevReload.Interface" };
        private Size _paletteSize = new Size(400, 600);
        private DockSides _dockSide = DockSides.Right;
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

        public PluginRegistrationBuilder WithVsProject(string vsProjectName)
        {
            _vsProjectName = vsProjectName;
            return this;
        }

        public PluginRegistrationBuilder WithCommands()
        {
            _useCommands = true;
            return this;
        }

        public PluginRegistrationBuilder WithPaletteSize(int width, int height)
        {
            _paletteSize = new Size(width, height);
            return this;
        }

        public PluginRegistrationBuilder WithDockSide(DockSides dockSide)
        {
            _dockSide = dockSide;
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
                VsProjectName = _vsProjectName ?? _pluginName,
                SharedAssemblyNames = _sharedAssemblyNames,
                PaletteSize = _paletteSize,
                DockSide = _dockSide,
                Registrar = _useCommands ? new CommandRegistrar() : null,
            };

            PluginManager.AddRegistration(reg);
        }
    }
}
