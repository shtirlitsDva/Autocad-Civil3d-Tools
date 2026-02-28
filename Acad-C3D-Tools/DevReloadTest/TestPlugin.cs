using System;

using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;

using DevReload;

[assembly: ExtensionApplication(typeof(DevReloadTest.TestPlugin))]

namespace DevReloadTest
{
    public class TestPlugin : IPlugin, IPluginPalette, IExtensionApplication
    {
        public void Initialize()
        {
            Log("Initialize() called");
        }

        public void Terminate()
        {
            Log("Terminate() called");
        }

        public object CreatePaletteSet()
        {
            Log("CreatePaletteSet() called");
            return new TestPaletteSet();
        }

        private static void Log(string msg)
        {
            var ed = Application.DocumentManager.MdiActiveDocument?.Editor;
            ed?.WriteMessage($"\n[DevReloadTest] {msg} @ {DateTime.Now:HH:mm:ss}");
        }
    }
}
