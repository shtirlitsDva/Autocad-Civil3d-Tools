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
    internal class HydraulicCalculationsService
    {
        private static DataService _dataService = DataService.Instance;
        internal static void CalculateDependentSums(
            List<(
                Func<AnalysisFeature, dynamic> Getter, 
                Action<AnalysisFeature, dynamic> Setter)> props)
        {
            var graphs = _dataService.Graphs;

            foreach (var graph in graphs)
            {
                // Find the root node
                var rootNode = graph.Vertices.FirstOrDefault(v => v.IsRootNode);
                if (rootNode == null)
                    throw new System.Exception("Root node not found.");

                var shortestPathTree = new UndirectedGraph<JunctionNode, PipeSegmentEdge>();
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
                var visited = new HashSet<JunctionNode>();
                CalculateBaseSums(shortestPathTree, rootNode, visited, props);

                CalculateHydraulics(shortestPathTree);

            }

            //debug dims
            var segs = graphs.Select(g => g.Edges.Select(y => y.PipeSegment));
            HashSet<string> strings = new();
            foreach (var edge in segs.SelectMany(x => x))
            {
                strings.Add(edge.PipeDim.DimName);
            }

            File.WriteAllText("C:\\Temp\\dims.txt", string.Join("\n", strings.OrderBy(x => x)));

            _dataService.StoreCalculatedData(graphs.Select(g => g.Edges.Select(y => y.PipeSegment)));
        }

        private static void CalculateHydraulics(
            UndirectedGraph<JunctionNode, PipeSegmentEdge> graph)
        {
            HydraulicCalc hc = new(HydraulicSettingsService.Instance.Settings);

            Parallel.ForEach(graph.Edges, edge =>
            {
                var result = hc.CalculateHydraulicSegment(edge.PipeSegment);
                edge.PipeSegment.PipeDim = result.Dim;
                edge.PipeSegment.ReynoldsSupply = result.ReynoldsSupply;
                edge.PipeSegment.ReynoldsReturn = result.ReynoldsReturn;
                edge.PipeSegment.FlowSupply = result.FlowSupply;
                edge.PipeSegment.FlowReturn = result.FlowReturn;
                edge.PipeSegment.PressureGradientSupply = result.PressureGradientSupply;
                edge.PipeSegment.PressureGradientReturn = result.PressureGradientReturn;
                edge.PipeSegment.VelocitySupply = result.VelocitySupply;
                edge.PipeSegment.VelocityReturn = result.VelocityReturn;
                edge.PipeSegment.UtilizationRate = result.UtilizationRate;
            });
        }

        private static List<dynamic> CalculateBaseSums(
        UndirectedGraph<JunctionNode, PipeSegmentEdge> graph,
        JunctionNode node, HashSet<JunctionNode> visited,
        List<(Func<AnalysisFeature, dynamic> Getter, Action<AnalysisFeature, dynamic> Setter)> props)
        {
            if (visited.Contains(node)) return props.Select(_ => (dynamic)0).ToList();

            visited.Add(node);

            List<dynamic> totalSums = props.Select(_ => (dynamic)0).ToList();

            // Traverse downstream nodes recursively
            foreach (var edge in graph.AdjacentEdges(node))
            {
                var neighbor = edge.GetOtherVertex(node);
                var downstreamSums = CalculateBaseSums(graph, neighbor, visited, props);
                for (int i = 0; i < props.Count; i++)
                {
                    totalSums[i] += downstreamSums[i];
                }
                for (int i = 0; i < props.Count; i++)
                {
                    var (getter, setter) = props[i];
                    setter(edge.PipeSegment, downstreamSums[i]);
                }

                //totalBuildings += buildingsFromNeighbor;
                //edge.PipeSegment.NumberOfBuildingsSupplied = buildingsFromNeighbor;
            }

            //If this is a leaf node, set the number of buildings supplied to the connected value

            if (totalSums.All(sum => sum == 0) && graph.AdjacentEdges(node).Count() == 1)
            {
                for (int i = 0; i < props.Count; i++)
                {
                    var (getter, setter) = props[i];
                    var value = getter(graph.AdjacentEdges(node).First().PipeSegment);
                    totalSums[i] = value;
                    setter(graph.AdjacentEdges(node).First().PipeSegment, value);
                }
            }

            return totalSums;
        }
    }
}
