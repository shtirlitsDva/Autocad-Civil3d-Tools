using Autodesk.AutoCAD.Ribbon;

using DimensioneringV2.AutoCAD;
using DimensioneringV2.Geometry;
using DimensioneringV2.GraphFeatures;
using DimensioneringV2.UI;

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

            // === DIAGNOSTIC: Detect near-miss junction points ===
            {
                var allPoints = pointToJunctionNodes.Keys.ToList();
                Utils.prtDbg($"Total unique junction points: {allPoints.Count}");

                double searchTolerance = 0.01;  // 1 cm
                double equalityTolerance = CoordinateTolerance.Current; // The Point2D equality tolerance
                var nearMisses = new List<(Point2D a, Point2D b, double dist)>();

                for (int i = 0; i < allPoints.Count; i++)
                {
                    for (int j = i + 1; j < allPoints.Count; j++)
                    {
                        double dx = allPoints[i].X - allPoints[j].X;
                        double dy = allPoints[i].Y - allPoints[j].Y;
                        double dist = Math.Sqrt(dx * dx + dy * dy);

                        // Points that are close but NOT equal (would have been caught by ContainsKey)
                        if (dist > equalityTolerance && dist < searchTolerance)
                        {
                            nearMisses.Add((allPoints[i], allPoints[j], dist));
                        }
                    }
                }

                Utils.prtDbg($"Near-miss pairs (within {searchTolerance}m but > {equalityTolerance}m): {nearMisses.Count}");
                foreach (var (a, b, dist) in nearMisses.OrderBy(x => x.dist).Take(20))
                {
                    Utils.prtDbg($"  Distance: {dist:E6}m");
                    Utils.prtDbg($"    Point A: ({a.X:F10}, {a.Y:F10})");
                    Utils.prtDbg($"    Point B: ({b.X:F10}, {b.Y:F10})");
                    Utils.prtDbg($"    Delta: dx={a.X - b.X:E6}, dy={a.Y - b.Y:E6}");
                }
            }
            // === END DIAGNOSTIC ===

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
                //Allow parallel edges here to be able to detect them, error and mark them
                var graph = new UndirectedGraph<NodeJunction, EdgePipeSegment>(allowParallelEdges: true);
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

            // Step 3: Mark all building nodes
            foreach (var graph in graphs)
            {
                var leafs = graph.Vertices.Where(x => graph.AdjacentEdges(x).Count() == 1);
                foreach (var leaf in leafs)
                {
                    var edge = graph.AdjacentEdges(leaf).First();
                    leaf.IsBuildingNode = edge.PipeSegment.NumberOfBuildingsConnected == 1;
                }
            }

            // Step 4: Detect parallel edges, error and mark them
            foreach (var graph in graphs)
            {
                // Dictionary to track edge occurrences
                Dictionary<(NodeJunction, NodeJunction), List<EdgePipeSegment>> edgeMap = new();

                foreach (var edge in graph.Edges)
                {
                    // Ensure undirected uniqueness: always store the smaller NodeJunction first
                    var key = edge.Source.GetHashCode() <= edge.Target.GetHashCode()
                        ? (edge.Source, edge.Target)
                        : (edge.Target, edge.Source);

                    if (!edgeMap.ContainsKey(key))
                    {
                        edgeMap[key] = new List<EdgePipeSegment>();
                    }
                    edgeMap[key].Add(edge);
                }

                // Identify parallel edges
                foreach (var kvp in edgeMap)
                {
                    if (kvp.Value.Count > 1)
                    {
                        IEnumerable<AnalysisFeature> reprojected = 
                            ProjectionService.ReProjectFeatures(
                                kvp.Value.Select(x => x.PipeSegment), "EPSG:3857", "EPSG:25832");

                        MarkParallelEdges.Mark(reprojected);

                        throw new Exception("DBG: Parallel edges detected! This is not allowed!");
                    }
                }
            }

            return graphs;
        }
    }
}