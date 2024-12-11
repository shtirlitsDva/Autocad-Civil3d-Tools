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
        public static List<UndirectedGraph<NodeJunction, EdgePipeSegment>> CreateGraphsFromFeatures(
        IEnumerable<IEnumerable<AnalysisFeature>> allFeatures)
        {
            List<UndirectedGraph<NodeJunction, EdgePipeSegment>> graphs = new();
            Dictionary<Point2D, NodeJunction> pointToJunctionNodes = new(new Point2DEqualityComparer());

            // Step 1: Identify all junction points and create junction nodes
            foreach (var featureList in allFeatures)
            {
                foreach (var featureNode in featureList)
                {
                    // Find the start and end points of each linestring in the feature geometry
                    LineString lineString = (LineString)featureNode.Geometry!;
                    Point2D startPoint = new Point2D(lineString.StartPoint.X, lineString.StartPoint.Y);
                    Point2D endPoint = new Point2D(lineString.EndPoint.X, lineString.EndPoint.Y);

                    // Create or retrieve the junction nodes for both start and end points
                    if (!pointToJunctionNodes.ContainsKey(startPoint))
                    {
                        pointToJunctionNodes[startPoint] = new NodeJunction(startPoint);
                    }
                    pointToJunctionNodes[startPoint].Degree++;

                    if (!pointToJunctionNodes.ContainsKey(endPoint))
                    {
                        pointToJunctionNodes[endPoint] = new NodeJunction(endPoint);
                    }
                    pointToJunctionNodes[endPoint].Degree++;
                }
            }

            var rootNodes = allFeatures.SelectMany(x => x).Where(x => x.IsRootNode);
            foreach (var node in rootNodes)
            {
                // Find the start and end points of each linestring in the feature geometry
                LineString lineString = (LineString)node.Geometry!;
                Point2D startPoint = new Point2D(lineString.StartPoint.X, lineString.StartPoint.Y);
                Point2D endPoint = new Point2D(lineString.EndPoint.X, lineString.EndPoint.Y);

                // Assign root node property to the corresponding junction node if it has a degree of 1
                if (pointToJunctionNodes[startPoint].Degree == 1)
                {
                    pointToJunctionNodes[startPoint].IsRootNode = true;
                }
                else if (pointToJunctionNodes[endPoint].Degree == 1)
                {
                    pointToJunctionNodes[endPoint].IsRootNode = true;
                }
            }

            // Step 2: Create edges (pipe segments) and build graphs for each disjoint network
            foreach (var featureList in allFeatures)
            {
                var graph = new UndirectedGraph<NodeJunction, EdgePipeSegment>(false);
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

                        // Find the start and end points of the current segment
                        LineString lineString = (LineString)currentNode.Geometry!;
                        Point2D startPoint = new Point2D(lineString.StartPoint.X, lineString.StartPoint.Y);
                        Point2D endPoint = new Point2D(lineString.EndPoint.X, lineString.EndPoint.Y);

                        // Retrieve the junction nodes for both start and end points
                        var startJunction = pointToJunctionNodes[startPoint];
                        var endJunction = pointToJunctionNodes[endPoint];

                        // Add junction nodes to the graph
                        graph.AddVertex(startJunction);
                        graph.AddVertex(endJunction);

                        // Create and add the edge representing the pipe segment
                        var pipeSegmentEdge = new EdgePipeSegment(startJunction, endJunction, currentNode);
                        if (!graph.ContainsEdge(pipeSegmentEdge))
                        {
                            graph.AddEdge(pipeSegmentEdge);
                        }

                        // Add the connected segment to the stack if it has not been visited
                        if (!visited.Contains(currentNode))
                        {
                            stack.Push(currentNode);
                        }
                    }
                }

                graphs.Add(graph);
            }

            return graphs;
        }
    }
}