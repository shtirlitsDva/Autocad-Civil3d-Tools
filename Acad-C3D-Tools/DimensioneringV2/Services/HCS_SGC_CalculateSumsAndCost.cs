using DimensioneringV2.BruteForceOptimization;
using DimensioneringV2.Common;
using DimensioneringV2.GraphModel;
using DimensioneringV2.GraphUtilities;
using DimensioneringV2.ResultCache;

using NorsynHydraulicCalc;

using QuikGraph;
using QuikGraph.Algorithms;

using System.Collections.Generic;
using System.Linq;

namespace DimensioneringV2.Services
{
    internal class HCS_SGC_CalculateSumsAndCost
    {
        /// <summary>
        /// Calculates the cost of the graph by building shortest path tree,
        /// calculating sums, and applying hydraulic calculations.
        /// Returns only the price (not the graph) for fitness evaluation.
        /// </summary>
        internal static double CalculateSumsAndCost(
            UndirectedGraph<BFNode, BFEdge> graph,
            UndirectedGraph<BFNode, BFEdge> originalSubGraph,
            List<SumProperty<BFEdge>> props,
            MetaGraph<UndirectedGraph<BFNode, BFEdge>> metaGraph,
            HydraulicCalculationCache<BFEdge> cache)
        {
            var result = CalculateSumsAndCostWithGraph(graph, originalSubGraph, props, metaGraph, cache);
            return result.price;
        }

        /// <summary>
        /// Calculates the cost of the graph by building shortest path tree,
        /// calculating sums, and applying hydraulic calculations.
        /// Returns both the price and the resulting graph.
        /// </summary>
        internal static (double price, UndirectedGraph<BFNode, BFEdge> graph) CalculateSumsAndCostWithGraph(
            UndirectedGraph<BFNode, BFEdge> graph,
            UndirectedGraph<BFNode, BFEdge> originalSubGraph,
            List<SumProperty<BFEdge>> props,
            MetaGraph<UndirectedGraph<BFNode, BFEdge>> metaGraph,
            HydraulicCalculationCache<BFEdge> cache)
        {
            var rootNode = metaGraph.GetRootForSubgraph(originalSubGraph);

            // We need to isolate calculations from the graph passed here
            // We don't want to store the results in the original graph
            // We just calculate the price
            var tempGraph = graph.CopyWithNewEdges();

            // Build shortest path tree from root to all terminals
            var spt = new UndirectedGraph<BFNode, BFEdge>();
            spt.AddVertexRange(tempGraph.Vertices);
            
            var tryGetPaths = tempGraph.ShortestPathsDijkstra(edge => edge.Length, rootNode);
            var terminals = metaGraph.GetTerminalsForSubgraph(originalSubGraph);

            foreach (var vertex in terminals)
            {
                if (tryGetPaths(vertex, out var path))
                {
                    foreach (var edge in path)
                    {
                        if (!spt.ContainsEdge(edge))
                        {
                            spt.AddEdge(edge);
                        }
                    }
                }
            }

            // Calculate sums (with injected sums from child subgraphs)
            GraphSumCalculator.CalculateSums(spt, rootNode, props, metaGraph.Sums);

            // Calculate hydraulics for distribution pipes
            foreach (var edge in spt.Edges)
            {
                if (edge.SegmentType == SegmentType.Stikledning) continue;
                var result = cache.GetOrCalculate(edge);
                edge.ApplyResult(result);
            }

            return (spt.Edges.Sum(x => x.Price), spt);
        }
    }
}
