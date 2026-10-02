using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;

using GroupByCluster;

using IntersectUtilities.GraphWrite;
using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.Graphs;
using IntersectUtilities.UtilsCommon.DataManager.CsvData;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static IntersectUtilities.UtilsCommon.Utils;

namespace IntersectUtilities
{
    public partial class Intersect
    {
        public void graphpopulate(Database db = null)
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = db ?? docCol.MdiActiveDocument.Database;
            try
            {
                GraphPopulation.Populate(localDb);
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
            }
        }

        /// <command>GRAPHWRITE</command>
        /// <summary>
        /// Draws a graph of the pipe system. Used to check for connectivity and other issues.
        /// Must be run in fjernevarme fremtid drawing.
        /// </summary>
        /// <category>Quality Assurance</category>
        [CommandMethod("GRAPHWRITE")]
        public void graphwrite()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            graphclear();
            graphpopulate();
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    var komponenter = Csv.FjvDynamicComponents;
                    HashSet<Entity> allEnts = localDb.GetFjvEntities(tx, true, false);
                    //Remove stiktees which are special tee blocks for stikledninger
                    allEnts = allEnts.Where(x =>
                    {
                        if (x is BlockReference br)
                            if (br.RealName() == "STIKTEE") return false;
                        return true;
                    }).ToHashSet();
                    PropertySetManager psm = new PropertySetManager(localDb, PSetDefs.DefinedSets.DriGraph);
                    var graph = new GraphWrite.Graph(localDb, psm, komponenter);
                    foreach (Entity entity in allEnts)
                    {
                        graph.AddEntityToGraphEntities(entity);
                    }
                    graph.CreateAndWriteGraph();
                    //Start the dot engine to create the graph and convert to pdf
                    System.Diagnostics.Process cmd = new System.Diagnostics.Process();
                    cmd.StartInfo.FileName = "cmd.exe";
                    cmd.StartInfo.WorkingDirectory = @"C:\Temp\";
                    cmd.StartInfo.Arguments = @"/c ""dot -Tpdf MyGraph.dot > MyGraph.pdf""";
                    cmd.Start();
                    cmd.WaitForExit();
                    //Start the dot engine to create the graph and convert to pdf
                    cmd = new System.Diagnostics.Process();
                    cmd.StartInfo.FileName = "cmd.exe";
                    cmd.StartInfo.WorkingDirectory = @"C:\Temp\";
                    cmd.StartInfo.Arguments = @"/c ""dot -Tsvg MyGraph.dot > MyGraph.svg""";
                    cmd.Start();
                    cmd.WaitForExit();
                    string svgContent = File.ReadAllText(@"C:\Temp\MyGraph.svg");
                    string htmlContent = $@"
<!DOCTYPE html>
<html lang=""da"">
<head>
    <meta charset=""UTF-8"">
    <title>Rørsystem</title>
    <style>
        body {{
            background-color: #121212;  /* Dark background color */
            color: #ffffff;  /* White text color */
        }}
        svg {{
            filter: invert(1) hue-rotate(180deg);  /* Invert colors and adjust hue */
        }}
    </style>
</head>
<body>
    {svgContent}
</body>
</html>
";
                    File.WriteAllText(@"C:\Temp\MyGraph.html", htmlContent);
                    string mSedgePath = @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe";
                    if (File.Exists(mSedgePath))
                    {
                        Process.Start(mSedgePath, @"C:\Temp\MyGraph.html");
                    }
                    else
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = @"C:\Temp\MyGraph.html",
                            UseShellExecute = true
                        };
                        Process.Start(psi);
                    }
                }
                catch (System.Exception ex)
                {
                    prdDbg(ex);
                    tx.Abort();
                    return;
                }
                tx.Commit();
            }
        }
        public void graphclear(Database? db = null)
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = db ?? docCol.MdiActiveDocument.Database;
            try
            {
                GraphPopulation.Clear(localDb);
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
            }
        }
    }
}
