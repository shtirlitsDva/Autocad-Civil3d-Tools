using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using DimensioneringV2.GraphFeatures;

using QuikGraph;
using QuikGraph.Graphviz;
using QuikGraph.Algorithms.Search;

namespace DimensioneringV2.Services
{
    internal class ExportGraphToDot
    {
        public static void Export(
            UndirectedGraph<NodeJunction, EdgePipeSegment> graph)
        {
            #region Assign node names
            AssignNodeNames(graph, graph.Vertices.First(v => v.IsRootNode));
            #endregion

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

        private static void AssignNodeNames(UndirectedGraph<NodeJunction, EdgePipeSegment> graph, NodeJunction root)
        {
            var visited = new HashSet<NodeJunction>();
            int componentIndex = 0;

            // Use a queue for BFS
            var queue = new Queue<NodeJunction>();

            foreach (var node in graph.Vertices)
            {
                if (visited.Contains(node))
                    continue;

                componentIndex++;
                int runningNumber = 0;

                // Start from root if in the first component, otherwise pick a random node
                var startNode = componentIndex == 1 ? root : node;

                queue.Enqueue(startNode);
                visited.Add(startNode);

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    current.Name = $"{componentIndex}.{++runningNumber}";

                    foreach (var edge in graph.AdjacentEdges(current))
                    {
                        var neighbor = edge.Target.Equals(current) ? edge.Source : edge.Target;
                        if (!visited.Contains(neighbor))
                        {
                            visited.Add(neighbor);
                            queue.Enqueue(neighbor);
                        }
                    }
                }
            }
        }
    }
}