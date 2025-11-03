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
using IntersectUtilities.GraphWriteV2;

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
                var components = builder.BuildForest(allEnts);

                // Attribute selectors
                Func<NodeContext, string?> clusterSelector = nc => nc.Alignment;
                Func<NodeContext, string?> nodeAttrSelector = nc => $"URL=\"ahk://ACCOMSelectByHandle/{nc.Handle}\"";

                // Cluster styling: red; entrypoint cluster thicker
                Func<ComponentTree, string, string?> clusterAttrsSelector =
                    (comp, key) =>
                    {
                        if (string.Equals(key, comp.RootAlignment, StringComparison.Ordinal))
                            return "color=red;\npenwidth=2.5;";
                        return "color=red;";
                    };

                // Edge QA
                Func<GraphWriteV2.ComponentTree, GraphWriteV2.NodeContext, GraphWriteV2.NodeContext, string?> edgeAttrSelector =
                    (comp, a, b) =>
                    {
                        var k = (a.Handle, b.Handle);
                        if (!comp.EdgeEndTypes.TryGetValue(k, out var ends)) return null;
                        return GraphWriteV2.EdgeQaAttributeProvider.GetAttributes(a.Owner, ends.fromEnd, b.Owner, ends.toEnd, komponenter);
                    };

                // Export DOT file
                string dotPath = @"C:\Temp\MyGraph.dot";
                GraphWriteV2.DotExporterV2.Export(
                    components,
                    clusterSelector,
                    nodeAttrSelector,
                    clusterAttrsSelector,
                    edgeAttrSelector,
                    dotPath,
                    includeHeader: true);

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