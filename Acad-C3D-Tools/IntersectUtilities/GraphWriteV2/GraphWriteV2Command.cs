using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;

using IntersectUtilities.UtilsCommon;
using static IntersectUtilities.UtilsCommon.Utils;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using IntersectUtilities.GraphWriteV2;
using IntersectUtilities.UtilsCommon.Graphs;

namespace IntersectUtilities
{
    public partial class Intersect
    {
        /// <command>GRAPHWRITEV2</command>
        /// <summary>
        /// Writes a DOT graph of the pipe system using Graph with QA and clustering.
        /// </summary>
        [CommandMethod("GRAPHWRITEV2")]
        public void graphwritev2()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            // Ensure connectivity is fresh if using PropertySet-based connections
            graphclear();
            graphpopulate();

            using var tx = localDb.TransactionManager.StartTransaction();

            try
            {
                System.Data.DataTable komponenter = CsvData.FK;
                HashSet<Entity> allEnts = localDb.GetFjvEntities(tx, true, false);
                // Remove STIKTEE
                allEnts = allEnts.Where(x =>
                {
                    if (x is BlockReference br)
                        if (br.RealName() == "STIKTEE") return false;
                    return true;
                }).ToHashSet();

                // Build spanning forest
                var builder = new GraphBuilderV2(localDb, komponenter);
                var graphs = builder.BuildGraphs(allEnts);

                // Attribute selectors
                Func<GraphEntity, string?> clusterSelector = n => n.Alignment;
                Func<GraphEntity, string?> nodeAttrSelector = n =>
                $"URL=\"ahk://ACCOMSelectByHandle/{n.OwnerHandle}\"";

                // Cluster styling: red; entrypoint cluster thicker
                Func<Graph<GraphEntity>, string, string?> clusterAttrsSelector =
                    (comp, key) =>
                    {
                        if (string.Equals(key, comp.Root.Value.Alignment, StringComparison.Ordinal))
                            return "color=red;\npenwidth=2.5;";
                        return "color=red;";
                    };

                // Edge QA (derive end types from connectivity on GraphEntity)
                string EdgeAttrsSelector(Node<GraphEntity> a, Node<GraphEntity> b)
                {
                    var con = a.Value.Cons.FirstOrDefault(c => c.ConHandle == b.Value.OwnerHandle);
                    var attr = con == null
                        ? null
                        : GraphWriteV2.EdgeQaAttributeProvider.GetAttributes(
                            a.Value.Owner, con.OwnEndType,
                            b.Value.Owner, con.ConEndType,
                            komponenter);
                    return attr ?? string.Empty;
                }

                // Export DOT file
                string dotPath = @"C:\Temp\MyGraph.dot";
                var sbAll = new StringBuilder();
                sbAll.AppendLine("digraph G {");

                int idx = 0;
                foreach (var g in graphs)
                {
                    idx++;
                    sbAll.AppendLine($"subgraph G_{idx} {{");
                    sbAll.AppendLine("node [shape=record];");

                    // Nodes with clusters and attributes
                    sbAll.Append(g.NodesToDot(
                        clusterSelector,
                        nodeAttrSelector,
                        key => clusterAttrsSelector(g, key)));

                    // Edges with QA attributes
                    sbAll.Append(g.EdgesToDot(EdgeAttrsSelector));

                    sbAll.AppendLine("}");
                }

                sbAll.AppendLine("}");

                var dir = Path.GetDirectoryName(dotPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir!);
                File.WriteAllText(dotPath, sbAll.ToString());

                // Run Graphviz (PDF)
                var cmd = new Process();
                cmd.StartInfo.FileName = "cmd.exe";
                cmd.StartInfo.WorkingDirectory = @"C:\Temp\";
                cmd.StartInfo.Arguments = @"/c ""dot -Tpdf MyGraph.dot > MyGraph.pdf""";
                cmd.Start();
                cmd.WaitForExit();

                // Run Graphviz (SVG)
                cmd = new Process();
                cmd.StartInfo.FileName = "cmd.exe";
                cmd.StartInfo.WorkingDirectory = @"C:\Temp\";
                cmd.StartInfo.Arguments = @"/c ""dot -Tsvg MyGraph.dot > MyGraph.svg""";
                cmd.Start();
                cmd.WaitForExit();

                // Build dark HTML wrapper
                string svgContent = File.ReadAllText(@"C:\Temp\MyGraph.svg");
                string htmlContent = $@"
<!DOCTYPE html>
<html lang=""da"">
<head>
    <meta charset=""UTF-8"">
    <title>Rørsystem</title>
    <style>
        body {{
            background-color: #121212;
            color: #ffffff;
        }}
        svg {{
            filter: invert(1) hue-rotate(180deg);
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
            catch (DebugEntityException dbex)
            {
                prdDbg(dbex);

                tx.Abort();

                using var dtx = localDb.TransactionManager.StartTransaction();
                foreach (var e in dbex.DebugEntities)
                {
                    switch (e)
                    {
                        case Polyline pl:
                            DebugHelper.CreateDebugLine(
                                pl.GetPointAtDist(pl.Length / 2), ColorByName("red"));
                            break;
                        case BlockReference br:
                            DebugHelper.CreateDebugLine(
                                br.Position, ColorByName("red"));
                            break;
                        default:
                            break;
                    }
                }
                dtx.Commit();
                return;
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