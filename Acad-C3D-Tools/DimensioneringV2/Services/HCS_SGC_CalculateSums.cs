using DimensioneringV2.BruteForceOptimization;
using QuikGraph;
using QuikGraph.Algorithms;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.Services.SubGraphs
{
    internal class CalculateSums
    {
        internal static double CalculateBFCost(UndirectedGraph<BFNode, BFEdge> graph,
            List<(
                Func<BFEdge, dynamic> Getter,
                Action<BFEdge, dynamic> Setter)> props)
        {
            // Find the root node
            var rootNode = graph.Vertices.FirstOrDefault(v => v.IsRootNode);
            if (rootNode == null)
                throw new System.Exception("Root node not found.");

            var shortestPathTree = new UndirectedGraph<BFNode, BFEdge>();
            shortestPathTree.AddVertexRange(graph.Vertices);

            // Dijkstra's algorithm for shortest paths from the root node
            var tryGetPaths = graph.ShortestPathsDijkstra(edge => edge.Length, rootNode);

            // Add edges to the shortest path tree based on the shortest paths from the root node
            var query = graph.Vertices.Where(
                x => graph.AdjacentEdges(x).Count() == 1 &&
                    graph.AdjacentEdges(x).First().NumberOfBuildingsConnected == 1);

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
            var visited = new HashSet<BFNode>();
            BFCalcBaseSums(shortestPathTree, rootNode, visited, props);

            BFCalcHydraulics(shortestPathTree);

            return shortestPathTree.Edges.Sum(e => e.Price);
        }
    }
}