using System;
using System.IO;
using System.Reflection;

using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;

using DevReload;

[assembly: CommandClass(typeof(NSPalette.Loader.NSPaletteCommands))]
[assembly: ExtensionApplication(typeof(NSPalette.Loader.NSPaletteCommands))]

namespace NSPalette.Loader
{
    public class NSPaletteCommands : IExtensionApplication
    {
        private static PluginHost<IPlugin> _host = new();
        private static PaletteSet? _paletteSet;

        public void Initialize()
        {
            Editor? ed = Application.DocumentManager.MdiActiveDocument?.Editor;
            ed?.WriteMessage("\nNSPALETTE Loader initialized.");
        }

        public void Terminate()
        {
            ClosePaletteSet();
            if (_host.IsLoaded) _host.Unload();
        }

        [CommandMethod("NSPALETTE")]
        public static void LoadPlugin()
        {
            Editor? ed = Application.DocumentManager.MdiActiveDocument?.Editor;
            try
            {
                if (_paletteSet != null)
                {
                    _paletteSet.Visible = true;
                    return;
                }

                string loaderDir = Path.GetDirectoryName(
                    Assembly.GetExecutingAssembly().Location)!;
                string pluginPath = Path.Combine(
                    loaderDir, "Isolated", "NSPalette.Core.dll");

                Load(pluginPath);
                ed?.WriteMessage("\nNSPALETTE loaded successfully.");
            }
            catch (System.Exception ex)
            {
                ed?.WriteMessage($"\nError: {ex.Message}\n{ex.StackTrace}");
            }            
        }

        [CommandMethod("NSPALETTEDEV")]
        public static void DevReloadPlugin()
        {
            Editor? ed = Application.DocumentManager.MdiActiveDocument?.Editor;
            try
            {
                ClosePaletteSet();
                if (_host.IsLoaded) _host.Unload();

                // "NSPaletteSet" must match the project name in VS Solution Explorer
                string? dllPath = DevReloadService.FindAndBuild("NSPalette", ed);
                if (dllPath == null) return;

                Load(dllPath);
                ed?.WriteMessage("\nNSPALETTE dev-reloaded successfully.");
            }
            catch (System.Exception ex)
            {
                ed?.WriteMessage($"\nError: {ex.Message}\n{ex.StackTrace}");
            }
        }

        [CommandMethod("NSPALETTEUNLOAD")]
        public static void UnloadPlugin()
        {
            Editor? ed = Application.DocumentManager.MdiActiveDocument?.Editor;
            if (!_host.IsLoaded)
            {
                ed?.WriteMessage("\nNSPALETTE is not loaded.");
                return;
            }
            ClosePaletteSet();
            _host.Unload();
            ed?.WriteMessage("\nNSPALETTE unloaded.");
        }

        private static void Load(string dllPath)
        {
            ClosePaletteSet();
            if (_host.IsLoaded) _host.Unload();

            var plugin = _host.Load(dllPath, "DevReload.Interface");
            plugin.Initialize();

            _paletteSet = (PaletteSet)plugin.CreatePaletteSet();
            _paletteSet.Visible = true;
            _paletteSet.Size = new System.Drawing.Size(500, 1500);
            _paletteSet.Dock = DockSides.Right;
        }

        private static void ClosePaletteSet()
        {
            if (_paletteSet != null)
            {
                _paletteSet.Close();
                _paletteSet = null;
            }
        }
    }
}
