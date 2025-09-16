using DimensioneringV2.GraphFeatures;
using DimensioneringV2.Services;

using Microsoft.Win32;

using NorsynHydraulicCalc.Pipes;

using QuikGraph;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace DimensioneringV2.MapCommands
{
    internal class WriteStikOgVejklasser
    {
        internal void Execute()
        {
            try
            {
                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Filter = "HTML Files (*.html)|*.html|All Files (*.*)|*.*",
                    DefaultExt = "html",
                    Title = "Skriv \"Stik og vejklasser\" tabel",
                    AddExtension = true
                };

                string fileName;
                if (saveFileDialog.ShowDialog() == true)
                {
                    fileName = saveFileDialog.FileName;
                }
                else
                {
                    return;
                }

                if (string.IsNullOrEmpty(fileName)) return;

                if (File.Exists(fileName))
                {
                    MessageBoxResult result = MessageBox.Show(
                        "The file already exists. Do you want to overwrite it?",
                        "File already exists",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result != MessageBoxResult.Yes) return;
                }

                var graphs = DataService.Instance.Graphs;

                var query = graphs
                    .SelectMany(x => x.Edges)
                    .Where(x => x.PipeSegment.NumberOfBuildingsConnected == 1);

                List<(Dim ownDim, Dim connectionDim, long vejklasse)> collate = new();

                foreach (var edge in query)
                {
                    UndirectedGraph<NodeJunction, EdgePipeSegment>? graph =
                        graphs.Where(x => x.ContainsEdge(edge)).FirstOrDefault();
                    if (graph == null) continue;
                    var adjacentEdges = graph.AdjacentEdges(edge.Source).ToList();
                    if (adjacentEdges.Count == 0)
                        adjacentEdges = graph.AdjacentEdges(edge.Target).ToList();
                    if (adjacentEdges.Count == 0) continue;

                    Dim conDim = adjacentEdges
                        .OrderBy(x => x.PipeSegment.Dim.OrderingPriority)
                        .ThenBy(x => x.PipeSegment.Dim.DimName)
                        .Select(x => x.PipeSegment.Dim)
                        .Last();

                    var obj = edge.PipeSegment["Vejklasse"];
                    if (obj == null) obj = 0;
                    var vejklasse = Convert.ToInt32(obj);
                    if (vejklasse == 0)
                        throw new Exception("Vejklasse er ikke defineret for ét af stikkene!\n" +
                            "Vejklasser skal udfyldes!");

                    collate.Add((edge.PipeSegment.Dim, conDim, vejklasse));
                }

                var html = BuildDimensionMatrixHtml(collate);

                File.WriteAllText(fileName, html);

                Utils.prtDbg($"Table saved to {fileName}");
            }
            catch (System.Exception ex)
            {
                Utils.prtDbg($"An error occurred during saving: {ex.Message}");
                Utils.prtDbg(ex);
            }
        }
        private static string BuildDimensionMatrixHtml(IEnumerable<(Dim ownDim, Dim connectionDim, long vejklasse)> rows)
        {
            var colDims = rows
                .Select(r => r.connectionDim)
                .DistinctBy(d => d.DimName)
                .OrderBy(d => d.DimName)
                .ToArray();

            var groups = rows
                .GroupBy(r => r.vejklasse)
                .OrderBy(g => g.Key);                     // Vejklasse 1, 2, …

            var sb = new StringBuilder("""
        <!DOCTYPE html>
        <html>
        <head>
            <meta charset="utf-8" />
            <style>
                table      { border-collapse:collapse;font-family:Segoe UI,Arial,sans-serif;font-size:14px; }
                th, td     { border:1px solid #444;padding:4px 8px;text-align:center; }
                thead th   { background:#2d2d2d;color:#fff; }
                .section   { background:#374553;color:#fff;text-align:left;font-weight:bold; }
                .ownDim    { background:#000;color:#fff;text-align:left; }
                .dataCell  { background:#6b3e00;color:#fff; }
            </style>
        </head>
        <body>
        <table>
            <thead>
                <tr>
                    <th>(TWIN) DN</th>
    """);

            foreach (var c in colDims)
                sb.Append($"<th>{c.DimName}</th>");
            sb.AppendLine("</tr></thead><tbody>");

            foreach (var g in groups)
            {
                sb.AppendLine($$"""<tr class="section"><th colspan="{{colDims.Length + 1}}">Vejklasse {{g.Key}}</th></tr>""");

                foreach (var own in g.Select(r => r.ownDim).DistinctBy(d => d.DimName).OrderBy(d => d.DimName))
                {
                    sb.Append($"""<tr><th class="ownDim">{own.DimName}</th>""");

                    foreach (var col in colDims)
                    {
                        var value = g.Count(r => r.ownDim.Equals(own) && r.connectionDim.Equals(col)); // change to Sum/Max etc. if needed
                        sb.Append($"""<td class="dataCell">{value}</td>""");
                    }
                    sb.AppendLine("</tr>");
                }
            }

            sb.Append("""
            </tbody>
        </table>
        </body>
        </html>
    """);
            return sb.ToString();
        }
    }
}
