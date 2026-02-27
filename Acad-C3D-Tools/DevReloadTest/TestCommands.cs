using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;

namespace DevReloadTest
{
    public class TestCommands
    {
        [CommandMethod("TESTCMD")]
        public void TestCommand()
        {
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;
            ed.WriteMessage($"\nTestCommand executed! Time: {DateTime.Now:HH:mm:ss}");
        }

        [CommandMethod("TESTCMD2")]
        public static void TestStaticCommand()
        {
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;
            ed.WriteMessage($"\nTestStaticCommand (static) executed! Time: {DateTime.Now:HH:mm:ss}");
        }
    }
}
