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
    /// Now includes cycle protection for use with non-tree graphs.
    /// </summary>
    internal static class GraphSumCalculator
    {
        /// <summary>
        /// Calculates accumulated sums from leaves to root for a standalone graph.
        /// </summary>
        public static double[] CalculateSums(
            UndirectedGraph<BFNode, BFEdge> graph,
            BFNode rootNode,
            List<SumProperty<BFEdge>> props)
        {
            var visited = new HashSet<BFNode>();
            return CalculateSumsRecursively(graph, rootNode, null, props, null, visited);
        }

        /// <summary>
        /// Calculates accumulated sums from leaves to root for a metagraph subgraph,
        /// with support for injecting pre-calculated sums from connected child subgraphs.
        /// </summary>
        public static double[] CalculateSums(
            UndirectedGraph<BFNode, BFEdge> graph,
            BFNode rootNode,
            List<SumProperty<BFEdge>> props,
            Dictionary<BFNode, List<double>> injectedSums)
        {
            var visited = new HashSet<BFNode>();
            return CalculateSumsRecursively(graph, rootNode, null, props, injectedSums, visited);
        }

        /// <summary>
        /// Recursively calculates accumulated sums from leaves to root.
        /// Traverses depth-first to leaves, then accumulates values on the way back up.
        /// </summary>
        /// <remarks>
        /// <b>ASSUMPTIONS:</b>
        /// <list type="number">
        ///   <item>
        ///     <b>ROOT NODE:</b> rootNode must be a vertex in the graph.
        ///   </item>
        ///   <item>
        ///     <b>CONNECTED GRAPH:</b> All nodes must be reachable from rootNode.
        ///     Disconnected components will not be processed.
        ///   </item>
        ///   <item>
        ///     <b>LEAF VALUES:</b> At leaf nodes (degree 1 in traversal), the Getter is called 
        ///     on the incoming edge to read initial values. For non-leaf nodes, values are
        ///     accumulated from children (Getter is NOT called).
        ///   </item>
        ///   <item>
        ///     <b>INJECTED SUMS:</b> For metagraph case, injectedSums contains pre-calculated
        ///     sums at "virtual leaf" nodes (connection points to child subgraphs).
        ///     These are added to the accumulator at those nodes.
        ///   </item>
        ///   <item>
        ///     <b>CYCLES:</b> Cycles are handled via visited set. If a node is already visited,
        ///     it returns zero sums (doesn't contribute twice). This effectively treats the
        ///     graph as if it were a spanning tree rooted at rootNode.
        ///   </item>
        /// </list>
        /// </remarks>
        private static double[] CalculateSumsRecursively(
            UndirectedGraph<BFNode, BFEdge> graph,
            BFNode node,
            BFEdge? incomingEdge,
            List<SumProperty<BFEdge>> props,
            Dictionary<BFNode, List<double>>? injectedSums,
            HashSet<BFNode> visited)
        {
            int propCount = props.Count;
            var sums = new double[propCount];

            // Cycle protection: if already visited, return zeros
            if (!visited.Add(node))
                return sums;

            // Get child edges (all adjacent edges except the one we came from)
            var childEdges = graph.AdjacentEdges(node)
                .Where(e => e != incomingEdge)
                .ToList();

            // Recurse into children first (depth-first) and accumulate their sums
            foreach (var childEdge in childEdges)
            {
                var childNode = childEdge.GetOtherVertex(node);
                
                // Skip if child already visited (handles cycles)
                if (visited.Contains(childNode))
                    continue;
                    
                var childSums = CalculateSumsRecursively(graph, childNode, childEdge, props, injectedSums, visited);

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
                // True leaf: no unvisited children AND no injected sums -> read initial values via Getter
                // This handles stikledning edges where we need to read KarFlow values etc.
                bool hasUnvisitedChildren = childEdges.Any(e => !visited.Contains(e.GetOtherVertex(node)));
                if (!hasUnvisitedChildren && !hasInjectedSums && childEdges.Count == 0)
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
