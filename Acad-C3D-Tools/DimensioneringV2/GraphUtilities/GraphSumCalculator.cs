using DimensioneringV2.BruteForceOptimization;
using DimensioneringV2.Common;

using QuikGraph;

using System.Collections.Generic;
using System.Linq;

namespace DimensioneringV2.GraphUtilities
{
    /// <summary>
    /// Calculates accumulated property sums from leaf nodes to root in tree-structured graphs.
    /// Supports both standalone graphs and metagraph subgraphs with injected sums.
    /// </summary>
    internal static class GraphSumCalculator
    {
        /// <summary>
        /// Calculates accumulated sums from leaves to root for a standalone graph.
        /// </summary>
        /// <param name="graph">The tree structure (undirected graph with tree topology)</param>
        /// <param name="rootNode">The root node to start traversal from</param>
        /// <param name="props">Properties to accumulate</param>
        /// <returns>Array of total accumulated sums at root</returns>
        public static double[] CalculateSums(
            UndirectedGraph<BFNode, BFEdge> graph,
            BFNode rootNode,
            List<SumProperty<BFEdge>> props)
        {
            return CalculateSumsRecursively(graph, rootNode, null, props, null);
        }

        /// <summary>
        /// Calculates accumulated sums from leaves to root for a metagraph subgraph,
        /// with support for injecting pre-calculated sums from connected child subgraphs.
        /// </summary>
        /// <param name="graph">The tree structure</param>
        /// <param name="rootNode">The root node to start traversal from</param>
        /// <param name="props">Properties to accumulate</param>
        /// <param name="injectedSums">Pre-calculated sums to inject at specific nodes (from child subgraphs)</param>
        /// <returns>Array of total accumulated sums at root</returns>
        public static double[] CalculateSums(
            UndirectedGraph<BFNode, BFEdge> graph,
            BFNode rootNode,
            List<SumProperty<BFEdge>> props,
            Dictionary<BFNode, List<double>> injectedSums)
        {
            return CalculateSumsRecursively(graph, rootNode, null, props, injectedSums);
        }

        /// <summary>
        /// Recursively calculates accumulated sums from leaves to root.
        /// Traverses depth-first to leaves, then accumulates values on the way back up.
        /// </summary>
        private static double[] CalculateSumsRecursively(
            UndirectedGraph<BFNode, BFEdge> graph,
            BFNode node,
            BFEdge? incomingEdge,
            List<SumProperty<BFEdge>> props,
            Dictionary<BFNode, List<double>>? injectedSums)
        {
            int propCount = props.Count;
            var sums = new double[propCount];

            // Get child edges (all adjacent edges except the one we came from)
            var childEdges = graph.AdjacentEdges(node)
                .Where(e => e != incomingEdge)
                .ToList();

            // Recurse into children first (depth-first) and accumulate their sums
            foreach (var childEdge in childEdges)
            {
                var childNode = childEdge.GetOtherVertex(node);
                var childSums = CalculateSumsRecursively(graph, childNode, childEdge, props, injectedSums);

                for (int i = 0; i < propCount; i++)
                    sums[i] += childSums[i];
            }

            // Inject pre-calculated sums from connected child subgraphs (metagraph case)
            bool hasInjectedSums = false;
            if (injectedSums != null && injectedSums.TryGetValue(node, out var extraSums))
            {
                hasInjectedSums = true;
                for (int i = 0; i < propCount; i++)
                    sums[i] += extraSums[i];
            }

            // Process the incoming edge (skip for root node which has no incoming edge)
            if (incomingEdge != null)
            {
                // True leaf: no children AND no injected sums -> read initial values via Getter
                // This handles stikledning edges where we need to read KarFlow values etc.
                if (childEdges.Count == 0 && !hasInjectedSums)
                {
                    for (int i = 0; i < propCount; i++)
                        sums[i] = props[i].Getter(incomingEdge);
                }

                // Write accumulated sums to the edge
                for (int i = 0; i < propCount; i++)
                    props[i].Setter(incomingEdge, sums[i]);
            }

            return sums;
        }
    }
}
