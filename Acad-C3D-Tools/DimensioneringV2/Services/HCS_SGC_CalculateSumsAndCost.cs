using DimensioneringV2.BruteForceOptimization;
using DimensioneringV2.Genetic;
using DimensioneringV2.GraphModel;

using QuikGraph;
using QuikGraph.Algorithms;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.Services
{
    internal class HCS_SGC_CalculateSumsAndCost
    {
        internal static double CalculateSumsAndCost(GraphChromosomeOptimized chr,
            List<(
                Func<BFEdge, dynamic> Getter,
                Action<BFEdge, dynamic> Setter)> props)
        {
            return CalculateSumsAndCost(
                chr.LocalGraph, chr.CoherencyManager.OriginalGraph, props, chr.CoherencyManager.MetaGraph);
        }
        internal static double CalculateSumsAndCost(UndirectedGraph<BFNode, BFEdge> graph,
            UndirectedGraph<BFNode, BFEdge> subGraph,
            List<(
                Func<BFEdge, dynamic> Getter,
                Action<BFEdge, dynamic> Setter)> props,
            MetaGraph<UndirectedGraph<BFNode, BFEdge>> metaGraph)
        {
            var rootNode = metaGraph.GetRootForSubgraph(subGraph);

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
                            shortestPathTree.AddEdge(edge);
                        }
                    }
                }
            }
            // Calculate the sums
            var sums = CalculateSums(shortestPathTree, rootNode, new HashSet<BFNode>(), props);
            // Calculate the cost
            var cost = CalculateCost(shortestPathTree, rootNode, new HashSet<BFNode>(), props);
            return cost;
        }
    }
}
