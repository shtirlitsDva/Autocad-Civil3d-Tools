using DimensioneringV2.BruteForceOptimization;
using DimensioneringV2.Services;

using QuikGraph;

using System.Collections.Generic;
using System.Linq;

namespace DimensioneringV2.Genetic
{
    /// <summary>
    /// Generates a single minimal Steiner tree by greedily removing non-bridge edges
    /// one at a time until no non-bridges remain (all edges are bridges).
    ///
    /// This is equivalent to following the leftmost branch of the SteinerTreesEnumeratorV3
    /// recursion tree, without any backtracking. It always produces a valid tree
    /// connecting all terminal nodes.
    ///
    /// Time complexity: O(E * bridge_finding_time) where E is the number of edges.
    /// Much faster than greedy optimization (no cost evaluation per removal).
    /// </summary>
    internal static class FirstSteinerSeedGenerator
    {
        /// <summary>
        /// Generates a single minimal Steiner tree from the given subgraph.
        /// Returns a new graph containing only the edges of the minimal tree.
        /// </summary>
        internal static UndirectedGraph<BFNode, BFEdge> Generate(
            UndirectedGraph<BFNode, BFEdge> subGraph)
        {
            // Work on a copy of the edge list to avoid modifying the original
            var workingEdges = subGraph.Edges.ToList();

            while (true)
            {
                // Build temporary graph for bridge detection
                var tempGraph = new UndirectedGraph<BFNode, BFEdge>();
                foreach (var v in subGraph.Vertices) tempGraph.AddVertex(v);
                tempGraph.AddEdgeRange(workingEdges);

                // Find bridges (edges whose removal would disconnect the graph)
                var bridges = FindBridges.DoFindThem(tempGraph);

                // Non-bridges are edges we can safely remove
                var nonBridges = workingEdges.Where(e => !bridges.Contains(e)).ToList();

                if (nonBridges.Count == 0)
                    break; // All edges are bridges - we have a minimal tree

                // Remove the first non-bridge edge (no cost evaluation, just strip it)
                workingEdges.Remove(nonBridges[0]);
            }

            // Build the result graph with edge copies (same pattern as CopyWithNewEdges)
            var result = new UndirectedGraph<BFNode, BFEdge>();
            foreach (var edge in workingEdges)
            {
                var edgeCopy = new BFEdge(edge);
                edgeCopy.NonBridgeChromosomeIndex = edge.NonBridgeChromosomeIndex;
                result.AddVerticesAndEdge(edgeCopy);
            }

            return result;
        }
    }
}
