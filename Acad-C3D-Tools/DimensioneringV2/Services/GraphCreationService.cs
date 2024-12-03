using DimensioneringV2.Geometry;
using DimensioneringV2.GraphFeatures;
using NetTopologySuite.Geometries;
using QuikGraph;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.Services
{
    internal class GraphCreationService
    {
        public static List<UndirectedGraph<AnalysisFeature, Edge<AnalysisFeature>>> CreateGraphsFromFeatures(
            IEnumerable<IEnumerable<AnalysisFeature>> allFeatures)
        {
            List<UndirectedGraph<AnalysisFeature, Edge<AnalysisFeature>>> graphs = new();
            Dictionary<Point2D, List<AnalysisFeature>> pointToFeatureNodes = new(new Point2DEqualityComparer());

            // Initialize feature nodes dictionary for easy lookup
            foreach (var featureList in allFeatures)
            {
                foreach (var featureNode in featureList)
                {
                    // Find the start and end points of each linestring in the feature geometry
                    LineString lineString = (LineString)featureNode.Geometry!;
                    Point2D startPoint = new Point2D(lineString.StartPoint.X, lineString.StartPoint.Y);
                    Point2D endPoint = new Point2D(lineString.EndPoint.X, lineString.EndPoint.Y);

                    // Add the feature node to the dictionary for both start and end points
                    if (!pointToFeatureNodes.ContainsKey(startPoint))
                    {
                        pointToFeatureNodes[startPoint] = new List<AnalysisFeature>();
                    }
                    pointToFeatureNodes[startPoint].Add(featureNode);

                    if (!pointToFeatureNodes.ContainsKey(endPoint))
                    {
                        pointToFeatureNodes[endPoint] = new List<AnalysisFeature>();
                    }
                    pointToFeatureNodes[endPoint].Add(featureNode);
                }
            }

            // Create edges and build graphs for each disjoint network
            foreach (var featureList in allFeatures)
            {
                var graph = new UndirectedGraph<AnalysisFeature, Edge<AnalysisFeature>>(false);
                HashSet<AnalysisFeature> visited = new();

                foreach (var featureNode in featureList)
                {
                    if (visited.Contains(featureNode))
                        continue;

                    Stack<AnalysisFeature> stack = new();
                    stack.Push(featureNode);

                    while (stack.Count > 0)
                    {
                        var currentNode = stack.Pop();
                        if (visited.Contains(currentNode))
                            continue;

                        visited.Add(currentNode);
                        graph.AddVertex(currentNode);

                        // Find connected nodes and add edges
                        LineString lineString = (LineString)currentNode.Geometry!;
                        Point2D startPoint = new Point2D(lineString.StartPoint.X, lineString.StartPoint.Y);
                        Point2D endPoint = new Point2D(lineString.EndPoint.X, lineString.EndPoint.Y);

                        foreach (var point in new[] { startPoint, endPoint })
                        {
                            if (pointToFeatureNodes.TryGetValue(point, out var neighborNodes))
                            {
                                foreach (var neighborNode in neighborNodes)
                                {
                                    if (neighborNode != currentNode && !graph.ContainsEdge(
                                        new Edge<AnalysisFeature>(currentNode, neighborNode)))
                                    {
                                        graph.AddVertex(neighborNode);
                                        graph.AddEdge(new Edge<AnalysisFeature>(currentNode, neighborNode));

                                        if (!visited.Contains(neighborNode))
                                        {
                                            stack.Push(neighborNode);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                graphs.Add(graph);
            }

            return graphs;
        }
    }
}
