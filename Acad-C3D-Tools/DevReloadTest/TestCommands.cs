using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

using Newtonsoft.Json;

using System;
using System.Collections.Generic;

// Suppress AutoCAD's auto-discovery of [CommandMethod] attributes.
// Without this, ExtensionLoader.ProcessAssembly scans all types and
// registers commands via CommandClass.AddCommand — a separate registry
// from Utils.AddCommand/RemoveCommand that we cannot clean up.
// Pointing at an empty class means AutoCAD finds zero commands to register.
// Our CommandRegistrar handles registration via Utils.AddCommand instead.
[assembly: CommandClass(typeof(DevReloadTest.NoAutoCommands))]

namespace DevReloadTest
{
    /// <summary>
    /// Empty marker class for [assembly: CommandClass].
    /// AutoCAD only scans this type for commands and finds none.
    /// </summary>
    internal class NoAutoCommands { }
    public class TestCommands
    {
        [CommandMethod("TESTCMD")]
        public void TestCommand()
        {
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;
            ed.WriteMessage($"\nTestCommand executed Ver10! Time: {DateTime.Now:HH:mm:ss}");

            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var btr = (BlockTableRecord)tr.GetObject(
                db.CurrentSpaceId, OpenMode.ForWrite);

                var line = new Line(
                new Point3d(0, 100, 0),
                new Point3d(-100, 100, 0));

                btr.AppendEntity(line);
                tr.AddNewlyCreatedDBObject(line, true);
                tr.Commit();
            }
        }

        [CommandMethod("TESTCMD2")]
        public static void TestStaticCommand()
        {
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;
            ed.WriteMessage($"\nTestStaticCommand (static) executed Ver10! Time: {DateTime.Now:HH:mm:ss}");
        }

        [CommandMethod("TESTJSON")]
        public void TestNuGetJson()
        {
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;

            // Create a test object and serialize it with Newtonsoft.Json
            // This proves NuGet packages resolve correctly from the isolated ALC
            var testData = new Dictionary<string, object>
            {
                ["Plugin"] = "DevReloadTest",
                ["LoadedAt"] = DateTime.Now.ToString("HH:mm:ss"),
                ["ALC"] = System.Runtime.Loader.AssemblyLoadContext.GetLoadContext(
                    typeof(TestCommands).Assembly)?.Name ?? "unknown",
                ["NewtonsoftVersion"] = typeof(JsonConvert).Assembly.GetName().Version?.ToString() ?? "?"
            };

            string json = JsonConvert.SerializeObject(testData, Formatting.Indented);
            ed.WriteMessage($"\nNuGet test — Newtonsoft.Json from isolated ALC:\n{json}");
        }
    }
}
