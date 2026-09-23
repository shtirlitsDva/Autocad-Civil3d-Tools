using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Internal;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;

using NSLOAD.Views;

[assembly: CommandClass(typeof(NSLOAD.NoCommands))]
[assembly: ExtensionApplication(typeof(NSLOAD.NsLoader))]

namespace NSLOAD
{
    public class NoCommands { }

    public class NsLoader : IExtensionApplication
    {
        private static PaletteSet? _mgmtPalette;
        private static readonly Guid MgmtPaletteGuid =
            new("A7E3F1B2-9C4D-4E8A-B6D5-2F1A3C7E9B04");

        private static NsLoadConfig _config = new();
        private static Dictionary<string, RegisterEntry> _csvApps = new();

        private static readonly List<(string Group, string Name, CommandCallback Callback)>
            _onDemandCommands = new();

        // The apps that answer to their own name as a command. Registered plugins
        // the drafter added by hand are reached from NSLOAD and NSLOADMGR only:
        // their names are theirs to choose and must not become commands.
        private static readonly HashSet<string> _onDemandNames =
            new(StringComparer.OrdinalIgnoreCase);

        public void Initialize()
        {
            Editor? ed = Application.DocumentManager.MdiActiveDocument?.Editor;

            // A plugin that has just been loaded or unloaded gets its load
            // command taken away or given back. Subscribed before anything can
            // load, so the startup loads are seen too.
            PluginManager.PluginStateChanged += SyncOnDemandCommand;

            // Take AutoCAD's assembly scan off NSLOAD-loaded plugins before
            // anything can load one. Without this the host registers their
            // commands permanently and builds its own plugin instance.
            try
            {
                AutoCadScanSuppressor.Install();
            }
            catch (System.Exception ex)
            {
                // Loud, not silent: without suppression every plugin needs the
                // NoCommands marker, and PluginManager must not call Initialize.
                ed?.WriteMessage(
                    "\nNSLOAD: WARNING - could not suppress AutoCAD's assembly scan " +
                    $"({ex.Message}) Plugins on this AutoCAD version still need the " +
                    "NoCommands marker class.");
            }

            string csvPath = @"X:\AutoCAD DRI - 01 Civil 3D\NetloadV2\Register-2025.csv";
            bool registerRead;
            try
            {
                _csvApps = CsvLoader.Load(csvPath);
                registerRead = true;
            }
            catch (System.Exception ex)
            {
                ed?.WriteMessage($"\nNSLOAD: Failed to read CSV: {ex.Message}");
                _csvApps = new Dictionary<string, RegisterEntry>();
                registerRead = false;
            }

            // Merge only against a register that was actually read. Merging against
            // an unreadable one (X: offline, OneDrive not mounted yet) would drop
            // every app from the saved config, and the drafter's own auto-load
            // choices with them.
            _config = NsLoadConfigLoader.Load() ?? new NsLoadConfig();
            if (registerRead)
            {
                _config = NsLoadConfigLoader.MergeWithCsv(_config, _csvApps);
                NsLoadConfigLoader.Save(_config);
            }

            int predefinedLoaded = 0;
            foreach (var app in _config.PredefinedApps)
            {
                if (!_csvApps.TryGetValue(app.DisplayName, out RegisterEntry? entry))
                    continue;

                PluginManager.Register(app.DisplayName)
                    .WithPath(entry.Path)
                    .WithCommands()
                    .Commit();

                // Every app from the register answers to its own name as a
                // command whenever it is not loaded - including after an unload,
                // which is why the command follows the state instead of being
                // handed out once here.
                _onDemandNames.Add(app.DisplayName);
                SyncOnDemandCommand(app.DisplayName);

                if (app.AutoLoad)
                {
                    PluginManager.Load(app.DisplayName);
                    predefinedLoaded++;
                }
            }

            int userLoaded = 0;
            foreach (var plugin in _config.Plugins)
            {
                PluginManager.Register(plugin.Name)
                    .WithPath(plugin.DllPath)
                    .WithCommands()
                    .Commit();

                if (plugin.LoadOnStartup)
                {
                    PluginManager.Load(plugin.Name);
                    userLoaded++;
                }
            }

            Utils.AddCommand("NSLOAD", "NSLOAD", "NSLOAD",
                CommandFlags.Modal, NsLoadCommand);
            Utils.AddCommand("NSLOAD", "NSLOADMGR", "NSLOADMGR",
                CommandFlags.Modal, OpenManager);

            //ed?.WriteMessage(
            //    $"\nNSLOAD: {_config.PredefinedApps.Count} predefined apps " +
            //    $"({predefinedLoaded} auto-loaded), " +
            //    $"{_config.Plugins.Count} user plugins ({userLoaded} auto-loaded).");
        }

        public void Terminate()
        {
            PluginManager.PluginStateChanged -= SyncOnDemandCommand;

            PluginManager.ShutdownAll();

            try { AutoCadScanSuppressor.Restore(); }
            catch (System.Exception ex) { NsLoadDiagnostics.Report("scan suppressor restore", ex); }

            // Dispose the cached management palette so it doesn't survive an unload/reload cycle.
            if (_mgmtPalette != null)
            {
                try
                {
                    _mgmtPalette.Visible = false;
                    _mgmtPalette.Dispose();
                }
                catch (System.Exception ex) { NsLoadDiagnostics.Report("manager palette dispose", ex); }
                _mgmtPalette = null;
            }
        }

        public static void NsLoadCommand()
        {
            var notLoaded = PluginManager.GetRegisteredPluginNames()
                .Where(n => !PluginManager.IsLoaded(n))
                .ToList();

            if (notLoaded.Count == 0)
            {
                var ed = Application.DocumentManager.MdiActiveDocument?.Editor;
                ed?.WriteMessage("\nNSLOAD: All plugins are already loaded.");
                return;
            }

            var selection = IntersectUtilities.StringGridFormCaller.Call(
                notLoaded, "Select plugin to load:");

            if (string.IsNullOrEmpty(selection))
                return;

            PluginManager.Load(selection);
        }

        public static void OpenManager()
        {
            if (_mgmtPalette == null)
            {
                _mgmtPalette = new PaletteSet(
                    "NSLOAD Manager", MgmtPaletteGuid)
                {
                    Size = new Size(400, 500),
                    MinimumSize = new Size(300, 200),
                    DockEnabled = DockSides.Left | DockSides.Right,
                };

                var panel = new NsLoadPanel();
                var vm = (ViewModels.NsLoadViewModel)panel.DataContext;
                vm.Initialize(_config, _csvApps);

                _mgmtPalette.AddVisual("Plugins", panel);
            }
            _mgmtPalette.Visible = true;
        }

        /// <summary>
        /// Gives the app its load command when it is not loaded and takes it away
        /// when it is, so the command says what it does at the moment it is typed.
        /// Idempotent: this is the one place the two are reconciled, called at
        /// startup and again on every load and unload.
        /// </summary>
        private static void SyncOnDemandCommand(string displayName)
        {
            if (!_onDemandNames.Contains(displayName)) return;

            if (PluginManager.IsLoaded(displayName))
                RemoveOnDemandCommand(displayName);
            else
                RegisterOnDemandCommand(displayName);
        }

        private static void RegisterOnDemandCommand(string displayName)
        {
            string group = "NSLOAD";
            string cmdName = displayName.ToUpperInvariant();
            string name = displayName;

            if (_onDemandCommands.Any(c => c.Name == cmdName)) return;

            // Loading raises the state change that takes this command away.
            CommandCallback cb = () => PluginManager.Load(name);

            Utils.AddCommand(group, cmdName, cmdName, CommandFlags.Modal, cb);
            _onDemandCommands.Add((group, cmdName, cb));
        }

        private static void RemoveOnDemandCommand(string displayName)
        {
            string cmdName = displayName.ToUpperInvariant();
            var cmd = _onDemandCommands.FirstOrDefault(c => c.Name == cmdName);
            if (cmd.Name != null)
            {
                Utils.RemoveCommand(cmd.Group, cmd.Name);
                _onDemandCommands.Remove(cmd);
            }
        }
    }
}
