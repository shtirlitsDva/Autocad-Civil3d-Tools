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

namespace DimensioneringV2.Services
{
    internal class HydraulicCalculationsService
    {
        private static DataService _dataService = DataService.Instance;
        internal static void PerformCalculations()
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

                // Traverse from the root node to set levels for each edge
                var visited = new HashSet<JunctionNode>();
                //SetEdgeLevels(shortestPathTree, rootNode, visited, 0);

                // Traverse from downstream nodes and calculate NumberOfBuildingsSupplied
                // visited.Clear();
                Stopwatch sw = new Stopwatch();
                sw.Start();
                CalculateBuildingsSupplied(shortestPathTree, rootNode, visited);
                sw.Stop();
                File.AppendAllLines(@"C:\Temp\mapLog.txt", [$"Elapsed: {sw.ElapsedMilliseconds} ms."]);
                utils.prdDbg($"Elapsed: {sw.ElapsedMilliseconds} ms.");
            }

            _dataService.StoreCalculatedData(graphs.Select(g => g.Edges.Select(y => y.PipeSegment)));
        }

        private static void SetEdgeLevels(
        UndirectedGraph<JunctionNode, PipeSegmentEdge> graph,
        JunctionNode node, HashSet<JunctionNode> visited, int currentLevel)
        {
            if (visited.Contains(node))
                return;

            visited.Add(node);

            foreach (var edge in graph.AdjacentEdges(node))
            {
                var neighbor = edge.GetOtherVertex(node);
                if (!visited.Contains(neighbor))
                {
                    edge.Level = currentLevel + 1;
                    SetEdgeLevels(graph, neighbor, visited, currentLevel + 1);
                }
            }
        }

        private static int CalculateBuildingsSupplied(
        UndirectedGraph<JunctionNode, PipeSegmentEdge> graph,
        JunctionNode node, HashSet<JunctionNode> visited)
        {
            if (visited.Contains(node))
                return 0;

            visited.Add(node);

            int totalBuildings = 0;

            // Traverse downstream nodes recursively
            foreach (var edge in graph.AdjacentEdges(node))
            {
                var neighbor = edge.GetOtherVertex(node);
                //if (!visited.Contains(neighbor))
                {
                    // Recursively calculate buildings supplied for downstream nodes
                    int buildingsFromNeighbor = CalculateBuildingsSupplied(graph, neighbor, visited);
                    totalBuildings += buildingsFromNeighbor;
                    edge.PipeSegment.NumberOfBuildingsSupplied = buildingsFromNeighbor;
                }
            }

            //If this is a leaf node, set the number of buildings supplied to the connected value
            if (totalBuildings == 0)
            {
                if (graph.AdjacentEdges(node).Count() == 1)
                {
                    totalBuildings = graph.AdjacentEdges(node).First().PipeSegment.NumberOfBuildingsConnected;
                }
            }

            return totalBuildings;
        }
    }
}
