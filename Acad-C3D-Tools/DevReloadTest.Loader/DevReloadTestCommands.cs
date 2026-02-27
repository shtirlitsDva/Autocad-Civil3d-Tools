using System;
using System.IO;
using System.Reflection;

using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;

using DevReload;

[assembly: CommandClass(typeof(DevReloadTest.Loader.DevReloadTestCommands))]
[assembly: ExtensionApplication(typeof(DevReloadTest.Loader.DevReloadTestCommands))]

namespace DevReloadTest.Loader
{
    public class DevReloadTestCommands : IExtensionApplication
    {
        private static PluginHost<IPlugin> _host = new();
        private static CommandRegistrar _registrar = new();
        private static PaletteSet? _paletteSet;

        public void Initialize()
        {
            Editor? ed = Application.DocumentManager.MdiActiveDocument?.Editor;
            ed?.WriteMessage("\nDEVRELOADTEST Loader initialized.");
        }

        public void Terminate()
        {
            ClosePaletteSet();
            _registrar.UnregisterAll();
            if (_host.IsLoaded) _host.Unload();
        }

        [CommandMethod("TESTLOAD")]
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
                    loaderDir, "Isolated", "DevReloadTest.Core.dll");

                Load(pluginPath);
                ed?.WriteMessage(
                    $"\nTESTLOAD complete. {_registrar.CommandCount} commands registered.");
            }
            catch (System.Exception ex)
            {
                ed?.WriteMessage($"\nError: {ex.Message}\n{ex.StackTrace}");
            }
        }

        [CommandMethod("TESTDEV")]
        public static void DevReloadPlugin()
        {
            Editor? ed = Application.DocumentManager.MdiActiveDocument?.Editor;
            try
            {
                ClosePaletteSet();
                _registrar.UnregisterAll();
                if (_host.IsLoaded) _host.Unload();

                // "DevReloadTest" must match the project name in VS Solution Explorer
                string? dllPath = DevReloadService.FindAndBuild("DevReloadTest", ed);
                if (dllPath == null) return;

                Load(dllPath);
                ed?.WriteMessage(
                    $"\nTESTDEV complete. {_registrar.CommandCount} commands registered.");
            }
            catch (System.Exception ex)
            {
                ed?.WriteMessage($"\nError: {ex.Message}\n{ex.StackTrace}");
            }
        }

        [CommandMethod("TESTUNLOAD")]
        public static void UnloadPlugin()
        {
            Editor? ed = Application.DocumentManager.MdiActiveDocument?.Editor;
            ClosePaletteSet();
            _registrar.UnregisterAll();
            if (_host.IsLoaded) _host.Unload();
            ed?.WriteMessage("\nDEVRELOADTEST unloaded.");
        }

        private static void Load(string dllPath)
        {
            ClosePaletteSet();
            _registrar.UnregisterAll();
            if (_host.IsLoaded) _host.Unload();

            var plugin = _host.Load(dllPath, "DevReload.Interface");

            // Register [CommandMethod]s found in the loaded Core assembly
            _registrar.RegisterFromAssembly(_host.LoadedAssembly!);

            plugin.Initialize();

            _paletteSet = (PaletteSet)plugin.CreatePaletteSet();
            _paletteSet.Visible = true;
            _paletteSet.Size = new System.Drawing.Size(400, 300);
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
