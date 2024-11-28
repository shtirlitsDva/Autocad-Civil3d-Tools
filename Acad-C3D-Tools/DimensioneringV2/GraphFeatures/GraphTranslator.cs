using DimensioneringV2.Geometry;
using DimensioneringV2.GraphModelRoads;
using dbg = IntersectUtilities.UtilsCommon.Utils.DebugHelper;
using utils = IntersectUtilities.UtilsCommon.Utils;

using QuikGraph;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;

namespace DimensioneringV2.GraphFeatures
{
    internal class GraphTranslator
    {
        //public static List<UndirectedGraph<FeatureNode, Edge<FeatureNode>>> TranslateGraph(Graph originalGraph)        
        public static List<List<FeatureNode>> TranslateGraph(Graph originalGraph)
        {
            List<List<FeatureNode>> allFeatures = new();

            HashSet<SegmentNode> visited = new();

            foreach (var subgraph in originalGraph.ConnectedComponents)
            {
                List<FeatureNode> nodes = new List<FeatureNode>();

                SegmentNode root = subgraph.RootNode;
                Point2D ep;
                var degree = originalGraph.GetPointDegree(root.StartPoint);
                if (degree == 1) ep = root.StartPoint;
                else ep = root.EndPoint;

                //dbg.CreateDebugLine(ep.To3d(), utils.ColorByName("green"));

                Stack <(SegmentNode seg, Point2D entry)> stack = new();
                stack.Push((root, ep)); // seed the stack with the root node

                List<SegmentNode> originalNodes = new();
                bool startNew = false;

                while (stack.Count > 0)
                {
                    var node = stack.Pop();

                    if (visited.Contains(node.seg)) continue;
                    visited.Add(node.seg);

                    if (startNew)
                    {
                        originalNodes = new();
                        startNew = false;
                    }
                    originalNodes.Add(node.seg);

                    node.seg.MakePointStart(node.entry);
                    Point2D exitPt = node.seg.GetOtherEnd(node.entry);
                    degree = originalGraph.GetPointDegree(exitPt);

                    switch (degree)
                    {
                        case 1: //Reached a leafnode
                            {
                                startNew = true;
                                break;
                            }
                        case 2: //Intermediate node
                            {
                                foreach (SegmentNode neighbor in node.seg.Neighbors)
                                {
                                    if (!visited.Contains(neighbor) && neighbor.HasPoint(exitPt))
                                    {
                                        stack.Push((neighbor, exitPt));
                                    }
                                }
                                break;
                            }
                        case > 2:
                            {
                                foreach (SegmentNode neighbor in node.seg.Neighbors)
                                {
                                    if (!visited.Contains(neighbor) && neighbor.HasPoint(exitPt))
                                    {
                                        stack.Push((neighbor, exitPt));
                                    }
                                }
                                startNew = true;
                                break;
                            }
                        case 0:
                            {
                                dbg.CreateDebugLine(
                                    node.seg.StartPoint.To3d(), utils.ColorByName("red"));
                                dbg.CreateDebugLine(
                                    node.seg.EndPoint.To3d(), utils.ColorByName("red"));
                                utils.prdDbg("Point has degree 0!\n" +
                                    $"{node.ToString()}");
                                return null;
                            }
                    }

                    if (startNew)
                    {
                        var lines = originalNodes.Select(n => n.ToLineString()).ToList();
                        NetTopologySuite.Geometries.Geometry geometry;
                        if (lines.Count > 1)
                        {
                            var merger = new NetTopologySuite.Operation.Linemerge.LineMerger();
                            merger.Add(lines);
                            var merged = merger.GetMergedLineStrings();

                            if (merged.Count > 1)
                            {

                                foreach (var item in originalNodes)
                                {
                                    dbg.CreateDebugLine(
                                        item.StartPoint.To3d(), utils.ColorByName("red"));
                                    dbg.CreateDebugLine(
                                        item.EndPoint.To3d(), utils.ColorByName("cyan"));
                                }
                                utils.prdDbg("Merging returned multiple linestrings!");
                                return new List<List<FeatureNode>>() { nodes };
                            }
                            geometry = merged[0];
                            //geometry = sequenced;
                        }
                        else geometry = lines[0];


                        FeatureNode fn = new FeatureNode(
                                geometry, new AttributesTable());
                        nodes.Add(fn);
                    }
                }

                allFeatures.Add(nodes);
            }

            return allFeatures;
        }

        public static List<UndirectedGraph<FeatureNode, Edge<FeatureNode>>> CreateGraphsFromFeatures(
            List<List<FeatureNode>> allFeatures)
        {

            #region Mark the root node before making graphs
            foreach (var list in allFeatures)
            {
                foreach (var fn in list)
                    fn.Attributes.Add("RootNode", false);
                if (list.Count > 0)
                    list[0].Attributes["RootNode"] = true;
            }
            #endregion

            List<UndirectedGraph<FeatureNode, Edge<FeatureNode>>> graphs = new();
            Dictionary<Point2D, List<FeatureNode>> pointToFeatureNodes = new(new Point2DEqualityComparer());

            // Initialize feature nodes dictionary for easy lookup
            foreach (var featureList in allFeatures)
            {
                foreach (var featureNode in featureList)
                {
                    // Find the start and end points of each linestring in the feature geometry
                    LineString lineString = (LineString)featureNode.Geometry;
                    Point2D startPoint = new Point2D(lineString.StartPoint.X, lineString.StartPoint.Y);
                    Point2D endPoint = new Point2D(lineString.EndPoint.X, lineString.EndPoint.Y);

                    // Add the feature node to the dictionary for both start and end points
                    if (!pointToFeatureNodes.ContainsKey(startPoint))
                    {
                        pointToFeatureNodes[startPoint] = new List<FeatureNode>();
                    }
                    pointToFeatureNodes[startPoint].Add(featureNode);

                    if (!pointToFeatureNodes.ContainsKey(endPoint))
                    {
                        pointToFeatureNodes[endPoint] = new List<FeatureNode>();
                    }
                    pointToFeatureNodes[endPoint].Add(featureNode);
                }
            }

            // Create edges and build graphs for each disjoint network
            foreach (var featureList in allFeatures)
            {
                var graph = new UndirectedGraph<FeatureNode, Edge<FeatureNode>>(false);
                HashSet<FeatureNode> visited = new();

                foreach (var featureNode in featureList)
                {
                    if (visited.Contains(featureNode))
                        continue;

                    Stack<FeatureNode> stack = new();
                    stack.Push(featureNode);

                    while (stack.Count > 0)
                    {
                        var currentNode = stack.Pop();
                        if (visited.Contains(currentNode))
                            continue;

                        visited.Add(currentNode);
                        graph.AddVertex(currentNode);

                        // Find connected nodes and add edges
                        LineString lineString = (LineString)currentNode.Geometry;
                        Point2D startPoint = new Point2D(lineString.StartPoint.X, lineString.StartPoint.Y);
                        Point2D endPoint = new Point2D(lineString.EndPoint.X, lineString.EndPoint.Y);

                        foreach (var point in new[] { startPoint, endPoint })
                        {
                            if (pointToFeatureNodes.TryGetValue(point, out var neighborNodes))
                            {
                                foreach (var neighborNode in neighborNodes)
                                {
                                    if (neighborNode != currentNode && !graph.ContainsEdge(new Edge<FeatureNode>(currentNode, neighborNode)))
                                    {
                                        graph.AddVertex(neighborNode);
                                        graph.AddEdge(new Edge<FeatureNode>(currentNode, neighborNode));

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