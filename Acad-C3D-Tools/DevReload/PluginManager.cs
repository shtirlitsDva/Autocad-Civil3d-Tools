using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;

using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Internal;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;

using Exception = System.Exception;

namespace DevReload
{
    /// <summary>
    /// Centralized lifecycle manager for DevReload hot-reload plugins.
    /// <para>
    /// Each plugin is registered once via a fluent builder, then managed
    /// through <see cref="Load"/>, <see cref="DevReload"/>, and <see cref="Unload"/>
    /// calls from thin <c>[CommandMethod]</c> wrappers in the Loader.
    /// </para>
    /// <para>
    /// <b>Build-first dev-reload:</b> The <see cref="DevReload"/> method builds from VS
    /// before tearing down the running plugin. If the build fails, the old plugin
    /// stays fully loaded and functional.
    /// </para>
    /// </summary>
    public static class PluginManager
    {
        private static readonly Dictionary<string, PluginRegistration> _plugins = new();

        /// <summary>
        /// Begin registering a plugin. Call builder methods to configure,
        /// then call <see cref="PluginRegistrationBuilder.Commit"/> to finalize.
        /// </summary>
        /// <param name="pluginName">
        /// Unique display name for this plugin, used in all lifecycle calls
        /// and editor messages. Example: <c>"NSPalette"</c>, <c>"DevReloadTest"</c>.
        /// </param>
        public static PluginRegistrationBuilder Register(string pluginName)
        {
            return new PluginRegistrationBuilder(pluginName);
        }

        /// <summary>
        /// Load the plugin from its release subfolder
        /// (<c>{LoaderDir}/{subfolder}/{dll}</c>).
        /// If already loaded and has a palette, just makes the palette visible.
        /// </summary>
        public static void Load(string pluginName)
        {
            var ed = GetEditor();
            try
            {
                var reg = GetRegistration(pluginName);

                // If already loaded with a palette, just show it
                if (reg.Host.IsLoaded && reg.PaletteSet != null)
                {
                    reg.PaletteSet.Visible = true;
                    return;
                }

                // If already loaded without palette (command-only), inform user
                if (reg.Host.IsLoaded)
                {
                    ed?.WriteMessage($"\n{pluginName} is already loaded.");
                    return;
                }

                string loaderDir = Path.GetDirectoryName(
                    Assembly.GetExecutingAssembly().Location)!;
                string pluginPath = Path.Combine(
                    loaderDir, reg.Subfolder, reg.DllFileName);

                LoadCore(reg, pluginPath);

                string cmdMsg = reg.Registrar != null
                    ? $" {reg.Registrar.CommandCount} commands registered."
                    : "";
                ed?.WriteMessage($"\n{pluginName} loaded.{cmdMsg}");
            }
            catch (Exception ex)
            {
                ed?.WriteMessage($"\n{pluginName} load error: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Dev-reload: build from VS <b>first</b>, then tear down and reload.
        /// <para>
        /// If the build fails, the old plugin stays loaded — nothing is destroyed.
        /// This is safe because plugin DLLs are stream-loaded (no file lock),
        /// so VS can rebuild freely while the old plugin is running.
        /// </para>
        /// </summary>
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

                // BUILD FIRST — old plugin stays loaded if this fails
                string? dllPath = DevReloadService.FindAndBuild(reg.VsProjectName, ed);
                if (dllPath == null) return;

                // Build succeeded — now safe to tear down and reload
                LoadCore(reg, dllPath);

                string cmdMsg = reg.Registrar != null
                    ? $" {reg.Registrar.CommandCount} commands registered."
                    : "";
                ed?.WriteMessage($"\n{pluginName} dev-reloaded.{cmdMsg}");
            }
            catch (Exception ex)
            {
                ed?.WriteMessage(
                    $"\n{pluginName} dev-reload error: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Fully unload the plugin: close palette, unregister commands,
        /// call <see cref="IPlugin.Terminate"/>, and release the isolated ALC.
        /// </summary>
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
                ed?.WriteMessage(
                    $"\n{pluginName} unload error: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Unload all registered plugins. Call from
        /// <see cref="Autodesk.AutoCAD.Runtime.IExtensionApplication.Terminate"/>.
        /// Best-effort: exceptions during individual plugin teardown are swallowed
        /// to ensure all plugins get a chance to clean up.
        /// </summary>
        public static void UnloadAll()
        {
            foreach (var reg in _plugins.Values)
            {
                try { TearDown(reg); }
                catch { /* best-effort during shutdown */ }
            }
        }

        // ── Public query + management API ─────────────────────────────

        /// <summary>
        /// Get names of all registered plugins.
        /// </summary>
        public static IReadOnlyList<string> GetRegisteredPluginNames()
            => _plugins.Keys.ToList();

        /// <summary>
        /// Check if a plugin is registered (regardless of loaded state).
        /// </summary>
        public static bool IsRegistered(string pluginName)
            => _plugins.ContainsKey(pluginName);

        /// <summary>
        /// Check if a plugin is currently loaded.
        /// </summary>
        public static bool IsLoaded(string pluginName)
            => _plugins.TryGetValue(pluginName, out var reg) && reg.Host.IsLoaded;

        /// <summary>
        /// Fully unregister a plugin: tear down if loaded, remove loader
        /// commands, and remove from the registry.
        /// </summary>
        public static void Unregister(string pluginName)
        {
            if (!_plugins.TryGetValue(pluginName, out var reg))
                return;

            TearDown(reg);
            UnregisterLoaderCommands(reg);
            _plugins.Remove(pluginName);
        }

        // ── Loader-level command registration ─────────────────────────

        /// <summary>
        /// Register the 3 per-plugin commands: {prefix}LOAD, {prefix}DEV,
        /// {prefix}UNLOAD. These are the loader's commands, not the plugin's
        /// internal commands (which are handled by <see cref="CommandRegistrar"/>).
        /// </summary>
        public static void RegisterLoaderCommands(string pluginName, string prefix)
        {
            if (!_plugins.TryGetValue(pluginName, out var reg))
                return;

            prefix = prefix.ToUpperInvariant();
            string group = "DEVRELOAD";
            string name = pluginName; // captured for closures

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
        /// register commands → initialize → create palette.
        /// </summary>
        private static void LoadCore(PluginRegistration reg, string dllPath)
        {
            // Always tear down first (idempotent)
            TearDown(reg);

            var plugin = reg.Host.Load(dllPath, reg.SharedAssemblyNames);

            // Register commands if this plugin uses CommandRegistrar
            if (reg.Registrar != null)
            {
                reg.Registrar.RegisterFromAssembly(reg.Host.LoadedAssembly!);
            }

            plugin.Initialize();

            // Create palette if the plugin provides one
            var paletteObj = plugin.CreatePaletteSet();
            if (paletteObj is PaletteSet ps)
            {
                reg.PaletteSet = ps;
                ps.Visible = true;
                ps.Size = reg.PaletteSize;
                ps.Dock = reg.DockSide;
            }
        }

        /// <summary>
        /// Full teardown: close palette → unregister commands →
        /// terminate plugin → unload ALC. Idempotent.
        /// </summary>
        private static void TearDown(PluginRegistration reg)
        {
            ClosePaletteSet(reg);
            reg.Registrar?.UnregisterAll();

            if (reg.Host.IsLoaded)
            {
                // Give plugin a chance to clean up (events, COM refs, etc.)
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

        /// <summary>
        /// Called by <see cref="PluginRegistrationBuilder.Commit"/> to finalize registration.
        /// </summary>
        internal static void AddRegistration(PluginRegistration reg)
        {
            _plugins[reg.PluginName] = reg;
        }
    }

    /// <summary>
    /// Per-plugin state managed by <see cref="PluginManager"/>.
    /// Holds the isolated ALC host, optional command registrar,
    /// optional palette, and configuration.
    /// </summary>
    internal class PluginRegistration
    {
        public required string PluginName { get; init; }
        public required string DllFileName { get; init; }
        public required string Subfolder { get; init; }
        public required string? VsProjectName { get; init; }
        public required string[] SharedAssemblyNames { get; init; }
        public required Size PaletteSize { get; init; }
        public required DockSides DockSide { get; init; }

        public PluginHost<IPlugin> Host { get; } = new();
        public CommandRegistrar? Registrar { get; init; }
        public PaletteSet? PaletteSet { get; set; }

        /// <summary>
        /// Loader-level commands ({prefix}LOAD/DEV/UNLOAD) registered via Utils.AddCommand.
        /// Stored for cleanup on unregister.
        /// </summary>
        public List<(string Group, string Name, CommandCallback Callback)> LoaderCommands { get; }
            = new();
    }

    /// <summary>
    /// Fluent builder for plugin registration. Provides sensible defaults
    /// so minimal configuration is needed for typical plugins.
    /// <para>
    /// Defaults: DLL = <c>{name}.Core.dll</c>, subfolder = <c>{name}</c>,
    /// VS project = <c>{name}</c>, no CommandRegistrar, palette 400x600, dock Right.
    /// </para>
    /// </summary>
    public class PluginRegistrationBuilder
    {
        private readonly string _pluginName;
        private string? _dllFileName;
        private string? _subfolder;
        private string? _vsProjectName;
        private string[] _sharedAssemblyNames = new[] { "DevReload.Interface" };
        private Size _paletteSize = new Size(400, 600);
        private DockSides _dockSide = DockSides.Right;
        private bool _useCommands;

        internal PluginRegistrationBuilder(string pluginName)
        {
            _pluginName = pluginName;
        }

        /// <summary>
        /// Set the DLL filename found in the plugin's subfolder.
        /// Default: <c>"{pluginName}.Core.dll"</c>.
        /// </summary>
        public PluginRegistrationBuilder WithDll(string dllFileName)
        {
            _dllFileName = dllFileName;
            return this;
        }

        /// <summary>
        /// Set the subfolder name under the Loader directory where the
        /// release build of this plugin resides.
        /// Default: same as <c>pluginName</c>.
        /// </summary>
        public PluginRegistrationBuilder WithSubfolder(string subfolder)
        {
            _subfolder = subfolder;
            return this;
        }

        /// <summary>
        /// Set the VS Solution Explorer project name used by
        /// <see cref="DevReloadService.FindAndBuild"/> for dev-reload.
        /// Default: same as <c>pluginName</c>.
        /// </summary>
        public PluginRegistrationBuilder WithVsProject(string vsProjectName)
        {
            _vsProjectName = vsProjectName;
            return this;
        }

        /// <summary>
        /// Enable dynamic command registration via <see cref="CommandRegistrar"/>.
        /// Core assemblies using this must include
        /// <c>[assembly: CommandClass(typeof(EmptyClass))]</c> to suppress
        /// AutoCAD's auto-registration.
        /// Omit for palette-only plugins.
        /// </summary>
        public PluginRegistrationBuilder WithCommands()
        {
            _useCommands = true;
            return this;
        }

        /// <summary>
        /// Set palette size. Default: 400x600.
        /// Ignored if the plugin's <see cref="IPlugin.CreatePaletteSet"/>
        /// returns null.
        /// </summary>
        public PluginRegistrationBuilder WithPaletteSize(int width, int height)
        {
            _paletteSize = new Size(width, height);
            return this;
        }

        /// <summary>
        /// Set palette dock side. Default: <see cref="DockSides.Right"/>.
        /// </summary>
        public PluginRegistrationBuilder WithDockSide(DockSides dockSide)
        {
            _dockSide = dockSide;
            return this;
        }

        /// <summary>
        /// Override shared assembly names passed to
        /// <see cref="PluginHost{TPlugin}.Load"/>.
        /// Default: <c>["DevReload.Interface"]</c>.
        /// </summary>
        public PluginRegistrationBuilder WithSharedAssemblies(params string[] assemblyNames)
        {
            _sharedAssemblyNames = assemblyNames;
            return this;
        }

        /// <summary>
        /// Finalize registration and add the plugin to <see cref="PluginManager"/>.
        /// </summary>
        public void Commit()
        {
            var reg = new PluginRegistration
            {
                PluginName = _pluginName,
                DllFileName = _dllFileName ?? $"{_pluginName}.Core.dll",
                Subfolder = _subfolder ?? _pluginName,
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
