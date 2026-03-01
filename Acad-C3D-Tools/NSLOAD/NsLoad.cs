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
        private static Dictionary<string, string> _csvApps = new();

        private static readonly List<(string Group, string Name, CommandCallback Callback)>
            _onDemandCommands = new();

        public void Initialize()
        {
            Editor? ed = Application.DocumentManager.MdiActiveDocument?.Editor;

            string csvPath = @"X:\AutoCAD DRI - 01 Civil 3D\NetloadV2\Register-2025.csv";
            try
            {
                _csvApps = CsvLoader.Load(csvPath);
            }
            catch (System.Exception ex)
            {
                ed?.WriteMessage($"\nNSLOAD: Failed to read CSV: {ex.Message}");
                _csvApps = new Dictionary<string, string>();
            }

            _config = NsLoadConfigLoader.MergeWithCsv(
                NsLoadConfigLoader.Load(), _csvApps);
            NsLoadConfigLoader.Save(_config);

            int predefinedLoaded = 0;
            foreach (var app in _config.PredefinedApps)
            {
                if (!_csvApps.TryGetValue(app.DisplayName, out string? dllPath))
                    continue;

                PluginManager.Register(app.DisplayName)
                    .WithDllPath(dllPath)
                    .WithCommands()
                    .Commit();

                if (app.AutoLoad)
                {
                    PluginManager.Load(app.DisplayName);
                    predefinedLoaded++;
                }
                else
                {
                    RegisterOnDemandCommand(app.DisplayName);
                }
            }

            int userLoaded = 0;
            foreach (var plugin in _config.Plugins)
            {
                PluginManager.Register(plugin.Name)
                    .WithDllPath(plugin.DllPath)
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

            ed?.WriteMessage(
                $"\nNSLOAD: {_config.PredefinedApps.Count} predefined apps " +
                $"({predefinedLoaded} auto-loaded), " +
                $"{_config.Plugins.Count} user plugins ({userLoaded} auto-loaded).");
        }

        public void Terminate() => PluginManager.UnloadAll();

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

            RemoveOnDemandCommand(selection);
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

        private static void RegisterOnDemandCommand(string displayName)
        {
            string group = "NSLOAD";
            string cmdName = displayName.ToUpperInvariant();
            string name = displayName;

            CommandCallback cb = () =>
            {
                RemoveOnDemandCommand(name);
                PluginManager.Load(name);
            };

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
