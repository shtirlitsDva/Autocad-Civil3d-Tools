using DimensioneringV2.BruteForceOptimization;
using DimensioneringV2.Common;
using DimensioneringV2.Services;
using DimensioneringV2.Services.SubGraphs;

using NorsynHydraulicCalc;

using QuikGraph;
using QuikGraph.Algorithms;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DimensioneringV2.MapCommands
{
    internal class CalculateSPD
    {
        internal async Task Execute()
        {
            try
            {
                List<SumProperty<BFEdge>> props =
                    [
                        new(f => f.NumberOfBuildingsConnected, (f, v) => f.NumberOfBuildingsSupplied = (int)v),
                        new(f => f.NumberOfUnitsConnected, (f, v) => f.NumberOfUnitsSupplied = (int)v),
                        new(f => f.HeatingDemandConnected, (f, v) => f.HeatingDemandSupplied = v),
                        new(f => f.KarFlowHeatSupply, (f, v) => f.KarFlowHeatSupply = v),
                        new(f => f.KarFlowBVSupply, (f, v) => f.KarFlowBVSupply = v),
                        new(f => f.KarFlowHeatReturn, (f, v) => f.KarFlowHeatReturn = v),
                        new(f => f.KarFlowBVReturn, (f, v) => f.KarFlowBVReturn = v),
                    ];

                var graphs = DataService.Instance.Graphs;

                HydraulicCalculationService.Initialize();

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
                    CalculateSumsRecursively(shortestPathTree, rootNode, null, props);

                    new HydraulicCalculationsService().CalculateGraphsFordelingsledninger(
                        shortestPathTree, HydraulicCalculationService.Calc);

                    foreach (var edge in shortestPathTree.Edges)
                    {
                        edge.PushAllResults();
                    }
                }

                //Perform post processing
                foreach (var graph in graphs)
                {
                    PressureAnalysisService.CalculateDifferentialLossAtClient(graph);
                }
            }
            catch (System.Exception ex)
            {
                Utils.prtDbg($"An error occurred during calculations: {ex.Message}");
                Utils.prtDbg(ex);
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
        private static double[] CalculateSumsRecursively(
            UndirectedGraph<BFNode, BFEdge> graph,
            BFNode node,
            BFEdge? incomingEdge,
            List<SumProperty<BFEdge>> props)
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
                var childSums = CalculateSumsRecursively(graph, childNode, childEdge, props);

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
                    for (int i = 0; i < propCount; i++)
                        sums[i] = props[i].Getter(incomingEdge);

                // Write accumulated sums to the edge
                for (int i = 0; i < propCount; i++)
                    props[i].Setter(incomingEdge, sums[i]);
            }

            return sums;
        }
    }
}