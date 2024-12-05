using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using QuikGraph;
using QuikGraph.Algorithms;
using QuikGraph.Algorithms.ShortestPath;

using DimensioneringV2.GraphFeatures;

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
                foreach (var vertex in graph.Vertices)
                {
                    if (vertex != rootNode && tryGetPaths(vertex, out var path))
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
                SetEdgeLevels(shortestPathTree, rootNode, visited, 0);

                // Traverse from downstream nodes and calculate NumberOfBuildingsSupplied
                visited.Clear();
                CalculateBuildingsSupplied(shortestPathTree, rootNode, visited);
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
                if (!visited.Contains(neighbor))
                {
                    // Recursively calculate buildings supplied for downstream nodes
                    int buildingsFromNeighbor = CalculateBuildingsSupplied(graph, neighbor, visited);
                    totalBuildings += buildingsFromNeighbor;
                    edge.PipeSegment.NumberOfBuildingsSupplied = buildingsFromNeighbor + edge.PipeSegment.NumberOfBuildingsConnected;
                }
            }

            // If this is a leaf node, set the number of buildings supplied to the connected value
            if (totalBuildings == 0)
            {
                totalBuildings = graph.AdjacentEdges(node).Sum(edge => edge.PipeSegment.NumberOfBuildingsConnected);
                foreach (var edge in graph.AdjacentEdges(node))
                {
                    edge.PipeSegment.NumberOfBuildingsSupplied = edge.PipeSegment.NumberOfBuildingsConnected;
                }
            }

            return totalBuildings;
        }
    }
}
