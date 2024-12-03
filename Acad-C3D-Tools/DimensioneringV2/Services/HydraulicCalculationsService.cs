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
            List<UndirectedGraph<AnalysisFeature, Edge<AnalysisFeature>>> graphs = 
                GraphCreationService.CreateGraphsFromFeatures(_dataService.Features);

            foreach (var graph in graphs)
            {
                // Find the root node
                var rootNode = graph.Vertices.FirstOrDefault(v => v.IsRootNode);
                if (rootNode == null)
                    throw new System.Exception("Root node not found.");

                // Initialize dictionary to store shortest distances from root
                var shortestPaths = new Dictionary<AnalysisFeature, double>();

                // Dijkstra's algorithm for shortest paths from the root node
                var tryGetPaths = graph.ShortestPathsDijkstra(edge => edge.Source.Length, rootNode);

                foreach (var vertex in graph.Vertices)
                {
                    if (tryGetPaths(vertex, out var path))
                    {
                        shortestPaths[vertex] = path.Sum(edge => edge.Source.Length);
                    }
                    else
                    {
                        shortestPaths[vertex] = double.PositiveInfinity;
                    }
                }

                // Traverse from downstream nodes and calculate NumberOfBuildingsSupplied
                var visited = new HashSet<AnalysisFeature>();
                CalculateBuildingsSupplied(graph, rootNode, visited);
            }

            _dataService.LoadData(graphs.Select(g => g.Vertices));
        }

        private static int CalculateBuildingsSupplied(
            UndirectedGraph<AnalysisFeature, Edge<AnalysisFeature>> graph,
            AnalysisFeature node, HashSet<AnalysisFeature> visited)
        {
            if (visited.Contains(node))
                return 0;

            visited.Add(node);

            // Start with the number of buildings directly connected to this node
            int totalBuildings = node.NumberOfBuildingsConnected;

            // Traverse downstream nodes
            foreach (var edge in graph.AdjacentEdges(node))
            {
                var neighbor = edge.GetOtherVertex(node);
                if (!visited.Contains(neighbor))
                {
                    totalBuildings += CalculateBuildingsSupplied(graph, neighbor, visited);
                }
            }

            node.NumberOfBuildingsSupplied = totalBuildings;
            return totalBuildings;
        }
    }
}
