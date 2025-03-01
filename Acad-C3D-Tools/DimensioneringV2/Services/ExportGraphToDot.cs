using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using DimensioneringV2.GraphFeatures;

using QuikGraph;
using QuikGraph.Graphviz;

namespace DimensioneringV2.Services
{
    internal class ExportGraphToDot
    {
        public static void Export(
            UndirectedGraph<NodeJunction, EdgePipeSegment> graph)
        {
            string dotFilePath = @"C:\Temp\graph.dot";

            var graphviz = new GraphvizAlgorithm<NodeJunction, EdgePipeSegment>(graph);
            graphviz.FormatVertex += (sender, args) =>
            {
                args.VertexFormat.Label = args.Vertex.ToString();
            };
            string dot = graphviz.Generate();
            File.WriteAllText(dotFilePath, dot);

            // Convert to PDF using Graphviz (Assuming Graphviz is installed)
            string pdfFilePath = @"C:\Temp\graph.pdf";
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "dot",
                    Arguments = $"-Tpdf {dotFilePath} -o {pdfFilePath}",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            process.WaitForExit();
        }
    }
}