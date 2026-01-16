using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;

using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.DataManager.CsvData;
using static IntersectUtilities.UtilsCommon.Utils;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using IntersectUtilities.GraphWriteV2;
using IntersectUtilities.UtilsCommon.Graphs;
using IntersectUtilities.GraphWriteV2.DotStyling;

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
                HashSet<Entity> allEnts = localDb.GetFjvEntities(tx, true, false);
                // Remove STIKTEE
                allEnts = allEnts.Where(x =>
                {
                    if (x is BlockReference br)
                        if (br.RealName() == "STIKTEE") return false;
                    return true;
                }).ToHashSet();

                var komponenter = Csv.FjvDynamicComponents;

                // Build spanning forest
                var builder = new GraphBuilderV2(localDb);
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

                // Edge QA using topology-aware analysis

                // Export DOT file
                string dotPath = @"C:\Temp\MyGraph.dot";
                var sbAll = new StringBuilder();
                sbAll.AppendLine("digraph G {");

                int idx = 0;
                // Collect QA error records across all graphs for HTML report
                var qaRecords = new List<(string Group, string Code, string Description, string A, string B)>();
                var seenKeys = new HashSet<string>(StringComparer.Ordinal);

                static string MapDescription(string code)
                {
                    switch (code)
                    {
                        case "T/E": return "Pipe type mismatch (T/E)";
                        case "DN": return "DN mismatch";
                        case "DN-RED": return "Reducer DN inconsistency";
                        case "DN-TEE": return "Tee DN inconsistency";
                        default: return code;
                    }
                }
                static string ScaleSvg(string svg, double scale)
                {
                    if (string.IsNullOrEmpty(svg)) return svg;
                    try
                    {
                        var doc = System.Xml.Linq.XDocument.Parse(svg);
                        var ns = (System.Xml.Linq.XNamespace)"http://www.w3.org/2000/svg";
                        var root = doc.Root;
                        if (root == null) return svg;

                        void ScaleDim(string name)
                        {
                            var a = root.Attribute(name);
                            if (a == null) return;
                            var s = a.Value;
                            int i = 0;
                            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.')) i++;
                            var num = s.Substring(0, i);
                            var unit = s.Substring(i);
                            if (double.TryParse(num, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v))
                                a.Value = (v * scale).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + unit;
                        }

                        // Scale width/height if present
                        ScaleDim("width");
                        ScaleDim("height");

                        // Wrap existing children in a scaling <g>
                        var children = root.Elements().ToList();
                        foreach (var c in children) c.Remove();
                        var g = new System.Xml.Linq.XElement(ns + "g",
                            new System.Xml.Linq.XAttribute("transform", $"scale({scale.ToString(System.Globalization.CultureInfo.InvariantCulture)})"));
                        g.Add(children);
                        root.Add(g);

                        return doc.ToString(System.Xml.Linq.SaveOptions.DisableFormatting);
                    }
                    catch
                    {
                        return svg;
                    }
                }
                void AddQaRecord(string code, string fromH, string toH)
                {
                    var group = string.Equals(code, "T/E", StringComparison.Ordinal) ? "T/E" : "DN";
                    // de-duplicate by unordered pair and code+group
                    var a = string.CompareOrdinal(fromH, toH) <= 0 ? fromH : toH;
                    var b = string.CompareOrdinal(fromH, toH) <= 0 ? toH : fromH;
                    var key = group + "|" + code + "|" + a + "|" + b;
                    if (!seenKeys.Add(key)) return;
                    qaRecords.Add((group, code, MapDescription(code), a, b));
                }
                foreach (var g in graphs)
                {
                    idx++;
                    sbAll.AppendLine($"subgraph G_{idx} {{");
                    // Global node defaults for HTML labels
                    sbAll.AppendLine("node [shape=plaintext, fontname=\"monospace bold\", fontsize=13];");

                    // Nodes with clusters and attributes
                    var styler = new UniformWidthHtmlStyler();
                    sbAll.Append(g.NodesToDot(
                        styler,
                        clusterSelector,
                        key => clusterAttrsSelector(g, key)));

                    // Pre-compute edge attributes per graph (directed parent->child)
                    var edgeAttrs = EdgeQaAttributeProvider.BuildEdgeAttributes(g, komponenter);
                    string EdgeAttrsSelectorV2(Node<GraphEntity> a, Node<GraphEntity> b)
                    {
                        var key = (a.Value.OwnerHandle.ToString(), b.Value.OwnerHandle.ToString());
                        return edgeAttrs.TryGetValue(key, out var s) ? s : string.Empty;
                    }
                    // Extract QA error codes for the HTML report
                    foreach (var kv in edgeAttrs)
                    {
                        var attr = kv.Value;
                        // Parse label content: [ label="...", color="red" ]
                        int li = attr.IndexOf("label=\"", StringComparison.Ordinal);
                        if (li >= 0)
                        {
                            li += 7;
                            int lj = attr.IndexOf("\"", li, StringComparison.Ordinal);
                            if (lj > li)
                            {
                                var label = attr.Substring(li, lj - li);
                                foreach (var code in label.Split(','))
                                {
                                    var c = code.Trim();
                                    if (c.Length == 0) continue;
                                    AddQaRecord(c, kv.Key.fromHandle, kv.Key.toHandle);
                                }
                            }
                        }
                    }
                    // Edges with QA attributes (topology-aware)
                    sbAll.Append(g.EdgesToDot(styler, EdgeAttrsSelectorV2));
                    // Also write cycle edges (non-tree) with dashed style and QA labels if any
                    string CycleAttrsSelector(Node<GraphEntity> a, Node<GraphEntity> b)
                    {
                        var key = (a.Value.OwnerHandle.ToString(), b.Value.OwnerHandle.ToString());
                        return edgeAttrs.TryGetValue(key, out var s) ? s : string.Empty;
                    }
                    sbAll.Append(g.ExtraEdgesToDot(styler, CycleAttrsSelector));

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
                // Scale outer width/height and wrap content in a transform group
                svgContent = ScaleSvg(svgContent, 0.5);
                // Prepend QA report (simple tables with links) before the SVG
                if (qaRecords.Count > 0)
                {
                    var reportSb = new StringBuilder();
                    reportSb.AppendLine("<div id=\"qa-report\"><h2>QA Error Report</h2>");
                    foreach (var grp in qaRecords
                        .GroupBy(r => r.Group, StringComparer.Ordinal)
                        .OrderBy(g => g.Key, StringComparer.Ordinal))
                    {
                        var title = grp.Key == "T/E" ? "Pipe Type mismatches" : "DN inconsistencies";
                        reportSb.AppendLine($"<h3>{title} ({grp.Count()})</h3>");
                        reportSb.AppendLine("<table><thead><tr><th>Description</th><th>Element A</th><th>Element B</th></tr></thead><tbody>");
                        foreach (var r in grp.OrderBy(x => x.Code, StringComparer.Ordinal).ThenBy(x => x.A, StringComparer.Ordinal).ThenBy(x => x.B, StringComparer.Ordinal))
                        {
                            string aHref = $"ahk://ACCOMSelectByHandle/{r.A}";
                            string bHref = $"ahk://ACCOMSelectByHandle/{r.B}";
                            reportSb.AppendLine($"<tr><td>{System.Net.WebUtility.HtmlEncode(r.Description)}</td><td><a href=\"{aHref}\">{System.Net.WebUtility.HtmlEncode(r.A)}</a></td><td><a href=\"{bHref}\">{System.Net.WebUtility.HtmlEncode(r.B)}</a></td></tr>");
                        }
                        reportSb.AppendLine("</tbody></table>");
                    }
                    reportSb.AppendLine("</div>");
                    svgContent = reportSb.ToString() + "\n" + svgContent;
                }
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