using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using QuikGraph;
using QuikGraph.Algorithms;
using QuikGraph.Algorithms.ShortestPath;

using DimensioneringV2.GraphFeatures;
using System.Diagnostics;

using utils = IntersectUtilities.UtilsCommon.Utils;
using System.IO;
using NorsynHydraulicCalc;

namespace DimensioneringV2.Services
{
    internal partial class HydraulicCalculationsService
    {
        private static DataService _dataService = DataService.Instance;
        internal static void CalculateSPDijkstra(
            List<(
                Func<AnalysisFeature, dynamic> Getter, 
                Action<AnalysisFeature, dynamic> Setter)> props)
        {
            var graphs = _dataService.Graphs;

            //Reset the results
            foreach (var f in graphs.SelectMany(g => g.Edges.Select(e => e.PipeSegment))) f.ResetHydraulicResults();

            foreach (var graph in graphs)
            {
                // Find the root node
                var rootNode = graph.Vertices.FirstOrDefault(v => v.IsRootNode);
                if (rootNode == null)
                    throw new System.Exception("Root node not found.");

                var shortestPathTree = new UndirectedGraph<NodeJunction, EdgePipeSegment>();
                shortestPathTree.AddVertexRange(graph.Vertices);

                // Dijkstra's algorithm for shortest paths from the root node
                var tryGetPaths = graph.ShortestPathsDijkstra(edge => edge.PipeSegment.Length, rootNode);
                
                // Add edges to the shortest path tree based on the shortest paths from the root node
                var query = graph.Vertices.Where(
                    x => graph.AdjacentEdges(x).Count() == 1 &&
                        graph.AdjacentEdges(x).First().PipeSegment.NumberOfBuildingsConnected == 1);

                foreach (var vertex in query)
                {
                    if (tryGetPaths(vertex, out var path))
                    {
                        foreach (var edge in path)
                        {
                            if (!shortestPathTree.ContainsEdge(edge))
                            {
                                shortestPathTree.AddVerticesAndEdge(edge);
                            }
                        }
                    }
                }

                // Traverse the network and calculate
                // the sums of all properties as given in the props list
                // These sums lays the foundation for the hydraulic calculations
                var visited = new HashSet<NodeJunction>();
                CalculateBaseSums(shortestPathTree, rootNode, visited, props);

                CalculateHydraulics(shortestPathTree);
            }

            _dataService.StoreCalculatedData(graphs.Select(g => g.Edges.Select(y => y.PipeSegment)));
        }
    }
}
