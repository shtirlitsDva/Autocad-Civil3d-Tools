using System;
using System.Drawing;
using System.Linq;

using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;

using DevReload.Views;

[assembly: CommandClass(typeof(DevReload.DevReloaderCommands))]
[assembly: ExtensionApplication(typeof(DevReload.DevReloaderCommands))]

namespace DevReload
{
    /// <summary>
    /// Config-driven loader — reads plugins.json at startup, registers
    /// dynamic commands per plugin, and provides the DEVRELOAD management
    /// palette for visual plugin management.
    /// <para>
    /// AutoCAD loads this DLL once via autoload (acad2025.lsp).
    /// If no plugins.json exists, initialization is silent.
    /// Plugins are registered + commands created for all entries,
    /// but only those with <c>loadOnStartup = true</c> are auto-loaded.
    /// </para>
    /// </summary>
    public class DevReloaderCommands : IExtensionApplication
    {
        private static PaletteSet? _mgmtPalette;
        private static readonly Guid MgmtPaletteGuid =
            new("fb1be221-4d6f-48ff-a0d3-39dc935bf749");

        public void Initialize()
        {
            Editor? ed = Application.DocumentManager.MdiActiveDocument?.Editor;

            var config = PluginConfigLoader.Load();
            if (config == null || config.Plugins.Count == 0)
            {
                ed?.WriteMessage("\nDevReload initialized (no plugins configured).");
                return;
            }

            // Register all plugins + their LOAD/DEV/UNLOAD commands
            foreach (var entry in config.Plugins)
                RegisterFromConfig(entry);

            // Auto-load only plugins with loadOnStartup = true
            int loaded = 0;
            foreach (var entry in config.Plugins.Where(e => e.LoadOnStartup))
            {
                PluginManager.Load(entry.Name);
                loaded++;
            }

            var names = PluginManager.GetRegisteredPluginNames();
            ed?.WriteMessage(
                $"\nDevReload: {names.Count} plugin(s) registered, {loaded} auto-loaded.");
        }

        public void Terminate() => PluginManager.UnloadAll();

        // ── Management palette ────────────────────────────────────────

        [CommandMethod("DEVRELOAD")]
        public static void OpenManager()
        {
            if (_mgmtPalette == null)
            {
                _mgmtPalette = new PaletteSet(
                    "DevReload Manager", MgmtPaletteGuid)
                {
                    Size = new Size(400, 500),
                    MinimumSize = new Size(300, 200),
                    DockEnabled = DockSides.Left | DockSides.Right,
                };
                _mgmtPalette.AddVisual("Plugins", new DevReloadPanel());
            }
            _mgmtPalette.Visible = true;
        }

        // ── Config → PluginManager bridge ─────────────────────────────

        /// <summary>
        /// Register a single plugin from a <see cref="PluginEntry"/> config
        /// entry. Creates the PluginManager registration and the 3 loader
        /// commands ({prefix}LOAD/DEV/UNLOAD).
        /// </summary>
        internal static void RegisterFromConfig(PluginEntry entry)
        {
            var builder = PluginManager.Register(entry.Name);

            if (entry.DllPath != null) builder.WithDllPath(entry.DllPath);
            if (entry.VsProject != null) builder.WithVsProject(entry.VsProject);
            if (entry.Commands) builder.WithCommands();

            builder.WithPaletteSize(entry.PaletteWidth, entry.PaletteHeight);

            if (Enum.TryParse<DockSides>(entry.DockSide, true, out var dock))
                builder.WithDockSide(dock);

            builder.Commit();

            string prefix = entry.CommandPrefix ?? entry.Name;
            PluginManager.RegisterLoaderCommands(entry.Name, prefix);
        }
    }
}
