using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using QuikGraph;
using QuikGraph.Algorithms;
using QuikGraph.Algorithms.ShortestPath;

using utils = IntersectUtilities.UtilsCommon.Utils;
using NorsynHydraulicCalc;
using DimensioneringV2.BruteForceOptimization;

namespace DimensioneringV2.Services
{
    internal partial class HydraulicCalculationsService
    {
        private static DataService _dataService = DataService.Instance;

        /// <summary>
        /// Property definition for recursive sum calculation.
        /// Supports two usage patterns:
        /// <list type="number">
        /// <item><description>Connected → Supplied: Getter reads source property (e.g., NumberOfBuildingsConnected), 
        /// Setter writes to different target property (e.g., NumberOfBuildingsSupplied)</description></item>
        /// <item><description>Same property: Getter/Setter both operate on same property (e.g., KarFlowHeatSupply).
        /// Value must be pre-calculated at leaf edges before the recursive sum runs.</description></item>
        /// </list>
        /// </summary>
        /// <param name="Getter">Reads the initial value (only called at leaf edges)</param>
        /// <param name="Setter">Writes the accumulated sum (called on all edges)</param>
        internal readonly record struct SumProperty(
            Func<BFEdge, double> Getter,
            Action<BFEdge, double> Setter);

        internal static void CalculateSPDijkstra(List<SumProperty> props)
        {
            HydraulicCalculationService.Initialize();
            var graphs = _dataService.Graphs;

            //Reset the results
            foreach (var f in graphs.SelectMany(g => g.Edges.Select(e => e.PipeSegment))) f.ResetHydraulicResults();            

            foreach (var ograph in graphs)
            {
                var graph = ograph.CopyToBF();

                //Mark bridges
                FindBridges.DoMarkThem(graph);

                // Find the root node
                var rootNode = graph.Vertices.FirstOrDefault(v => v.IsRootNode);
                if (rootNode == null)
                    throw new System.Exception("Root node not found.");

                var shortestPathTree = new UndirectedGraph<BFNode, BFEdge>();
                shortestPathTree.AddVertexRange(graph.Vertices);

                // Dijkstra's algorithm for shortest paths from the root node
                var tryGetPaths = graph.ShortestPathsDijkstra(
                    edge => edge.Length, rootNode);
                
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

                //Calculate client connections
                foreach (var edge in shortestPathTree.Edges.Where(
                    x => x.SegmentType == SegmentType.Stikledning))
                {
                    var res = HydraulicCalculationService
                        .Calc.CalculateClientSegment(edge);
                    edge.ApplyResult(res);
                }

                // Traverse the network and calculate
                // the sums of all properties as given in the props list
                // These sums lays the foundation for the hydraulic calculations
                CalculateRecursivelyBaseSumsForSPD(shortestPathTree, rootNode, null, props);

                new HydraulicCalculationsService().CalculateGraphsFordelingsledninger(
                    shortestPathTree, HydraulicCalculationService.Calc);

                foreach (var edge in shortestPathTree.Edges)
                {
                    edge.PushAllResults();
                }
            }            
        }

        /// <summary>
        /// Recursively calculates accumulated sums from leaves to root.
        /// Traverses depth-first to leaves, then accumulates values on the way back up.
        /// </summary>
        /// <param name="graph">The tree structure (undirected graph with tree topology)</param>
        /// <param name="node">Current node being processed</param>
        /// <param name="incomingEdge">The edge we traversed to reach this node (null for root)</param>
        /// <param name="props">Properties to accumulate (see <see cref="SumProperty"/> for usage patterns)</param>
        /// <returns>Array of accumulated sums for this subtree</returns>
        /// <remarks>
        /// <para>
        /// Algorithm: At leaf edges, reads initial values via Getter. At intermediate edges,
        /// sums are accumulated from children's return values (Getter is NOT called).
        /// Setter is called on all edges to write the final accumulated value.
        /// </para>
        /// <para>
        /// ASSUMPTION: Under normal operation, no property value at a leaf should be zero.
        /// If any leaf has zero values, this may indicate upstream calculation errors.
        /// </para>
        /// </remarks>
        private static double[] CalculateRecursivelyBaseSumsForSPD(
            UndirectedGraph<BFNode, BFEdge> graph,
            BFNode node,
            BFEdge? incomingEdge,
            List<SumProperty> props)
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
                var childSums = CalculateRecursivelyBaseSumsForSPD(graph, childNode, childEdge, props);

                for (int i = 0; i < propCount; i++)
                {
                    sums[i] += childSums[i];
                }
            }

            // Process the incoming edge (skip for root node which has no incoming edge)
            if (incomingEdge != null)
            {
                // Leaf node: no children, so read the "Connected" values via Getter
                // Intermediate node: sums already contain accumulated children values
                if (childEdges.Count == 0)
                {
                    for (int i = 0; i < propCount; i++)
                    {
                        sums[i] = props[i].Getter(incomingEdge);
                    }
                }

                // Write accumulated sums to the edge
                for (int i = 0; i < propCount; i++)
                {
                    props[i].Setter(incomingEdge, sums[i]);
                }
            }

            return sums;
        }
    }
}