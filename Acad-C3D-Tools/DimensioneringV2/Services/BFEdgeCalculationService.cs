using DimensioneringV2.BruteForceOptimization;
using DimensioneringV2.ResultCache;

using NorsynHydraulicCalc;
using NorsynHydraulicCalc.Pipes;

using QuikGraph;

using System.Linq;

namespace DimensioneringV2.Services
{
    /// <summary>
    /// Centralized service for BFEdge hydraulic calculations.
    /// Provides methods for finding parent edges and recalculating SL with rules.
    /// </summary>
    internal static class BFEdgeCalculationService
    {
        /// <summary>
        /// Finds the parent FL (Fordelingsledning) edge for a given SL (Stikledning) edge.
        /// SL edges are always leaf edges - one end is a leaf node (degree 1).
        /// The parent FL is connected to the non-leaf node of the SL.
        /// </summary>
        /// <param name="slEdge">The SL edge to find parent for.</param>
        /// <param name="graph">The graph containing the edges.</param>
        /// <returns>The parent FL edge, or null if not found.</returns>
        public static BFEdge? FindParentFlEdge(BFEdge slEdge, UndirectedGraph<BFNode, BFEdge> graph)
        {
            // Find which node is the non-leaf node (degree > 1)
            var sourceNode = slEdge.Source;
            var targetNode = slEdge.Target;

            BFNode nonLeafNode;
            if (graph.AdjacentDegree(sourceNode) > 1)
            {
                nonLeafNode = sourceNode;
            }
            else if (graph.AdjacentDegree(targetNode) > 1)
            {
                nonLeafNode = targetNode;
            }
            else
            {
                // Both nodes have degree 1 - isolated SL, no parent
                return null;
            }

            // Find FL edges connected to the non-leaf node
            var adjacentEdges = graph.AdjacentEdges(nonLeafNode);
            var parentFlEdge = adjacentEdges
                .Where(e => e != slEdge && e.SegmentType == SegmentType.Fordelingsledning)
                .FirstOrDefault();

            return parentFlEdge;
        }

        /// <summary>
        /// Recalculates SL segments with rule-based dimension selection using parent FL pipe type.
        /// This should be called after FL hydraulics are calculated.
        /// </summary>
        /// <param name="graph">The graph containing edges to recalculate.</param>
        public static void RecalculateSlWithRules(UndirectedGraph<BFNode, BFEdge> graph)
            => RecalculateSlWithRules(graph, null);

        /// <summary>
        /// Recalculates SL segments with rule-based dimension selection using parent FL pipe type.
        /// Uses cache for performance when provided.
        /// This should be called after FL hydraulics are calculated.
        /// </summary>
        /// <param name="graph">The graph containing edges to recalculate.</param>
        /// <param name="cache">Optional cache for SL calculations.</param>
        public static void RecalculateSlWithRules(
            UndirectedGraph<BFNode, BFEdge> graph,
            ClientCalculationCache<BFEdge>? cache)
        {
            foreach (var slEdge in graph.Edges.Where(e => e.SegmentType == SegmentType.Stikledning))
            {
                // Skip manually dimensioned edges
                if (slEdge.ManualDim) continue;

                // Find the parent FL edge
                var parentFlEdge = FindParentFlEdge(slEdge, graph);

                // Get parent pipe type (null if no parent found)
                PipeType? parentPipeType = parentFlEdge?.Dim.PipeType;

                // Recalculate using cache or direct calculation
                CalculationResultClient result;
                if (cache != null)
                {
                    result = cache.GetOrCalculate(slEdge, parentPipeType);
                }
                else
                {
                    result = HydraulicCalculationService.Calc.CalculateClientSegment(slEdge, parentPipeType);
                }
                slEdge.ApplyResult(result);
            }
        }
    }
}
