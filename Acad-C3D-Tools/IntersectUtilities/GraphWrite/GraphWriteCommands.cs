using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;

using GroupByCluster;

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
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    var komponenter = Csv.FjvDynamicComponents;
                    HashSet<Entity> allEnts = localDb.GetFjvEntities(tx, true, false);
                    PropertySetManager psm = new PropertySetManager(localDb, PSetDefs.DefinedSets.DriGraph);
                    var graph = new GraphWrite.Graph(localDb, psm, komponenter);
                    foreach (Entity entity in allEnts) graph.AddEntityToPOIs(entity);
                    //Create clusters of POIs based on a maximum distance
                    //Distance is reduced, because was having a bad day
                    IEnumerable<IGrouping<POI, POI>> clusters
                        = graph.POIs.GroupByCluster((x, y) => x.Point.GetDistanceTo(y.Point), 0.003);
                    //Iterate over clusters
                    foreach (IGrouping<POI, POI> cluster in clusters)
                    {
                        //Create unique pairs
                        var pairs = cluster.SelectMany((value, index) => cluster.Skip(index + 1),
                                                       (first, second) => new { first, second });
                        //Create reference to each other for each pair
                        foreach (var pair in pairs)
                        {
                            if (pair.first.Owner.Handle == pair.second.Owner.Handle) continue;
                            pair.first.AddReference(pair.second);
                            pair.second.AddReference(pair.first);
                        }
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
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    var komponenter = Csv.FjvDynamicComponents;
                    HashSet<Entity> allEnts = localDb.GetFjvEntities(tx, true, false);
                    PropertySetManager psm = new PropertySetManager(localDb, PSetDefs.DefinedSets.DriGraph);
                    PSetDefs.DriGraph driGraph = new PSetDefs.DriGraph();
                    foreach (var item in allEnts)
                        psm.WritePropertyString(item, driGraph.ConnectedEntities, "");
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
    }
}
