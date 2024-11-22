using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Dimensionering.DimensioneringV2.Geometry;
using IntersectUtilities;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dimensionering.DimensioneringV2.GraphModelRoads
{
    internal class Graph
    {
        public List<SegmentNode> Segments { get; }
        public SpatialIndex SpatialIndex { get; private set; }
        public List<SegmentNode> RootNodes { get; private set; }
        public List<ConnectedComponent> ConnectedComponents { get; private set; }

        public Graph()
        {
            Segments = new List<SegmentNode>();
            SpatialIndex = new SpatialIndex();
            RootNodes = new List<SegmentNode>();
            ConnectedComponents = new List<ConnectedComponent>();
        }

        // Method to build the graph from polylines
        public void BuildGraph(List<Polyline> polylines, List<Point2D> rootPoints)
        {
            // Step 1: Convert polylines to segments
            foreach (var polyline in polylines)
            {
                var segments = ConvertPolylineToSegments(polyline);
                Segments.AddRange(segments);
            }

            BuildNeighbors();

            SpatialIndex.BuildIndex(Segments);

            // Identify connected components in the graph
            IdentifyConnectedComponents();

            // Map root points to connected components
            MapRootPointsToComponents(rootPoints);

            // Check for leftover segments
            CheckForLeftoverSegments();
        }

        // Convert a polyline to a list of segments
        private List<SegmentNode> ConvertPolylineToSegments(Polyline polyline)
        {
            var segments = new List<SegmentNode>();
            for (int i = 0; i < polyline.NumberOfVertices - 1; i++)
            {
                var startPoint = polyline.GetPoint2dAt(i);
                var endPoint = polyline.GetPoint2dAt(i + 1);
                if (startPoint.GetDistanceTo(endPoint) < Tolerance.Default)
                    continue; // Skip zero-length segments (duplicate points)
                var segment = new SegmentNode(startPoint, endPoint);
                segments.Add(segment);
            }
            return segments;
        }

        // Build neighbor relationships between segments
        private void BuildNeighbors()
        {
            // Create a dictionary to map points to segments for quick lookup
            var pointToSegments = new Dictionary<Point2D, List<SegmentNode>>(new Point2DEqualityComparer());

            foreach (var segment in Segments)
            {
                if (!pointToSegments.ContainsKey(segment.StartPoint))
                    pointToSegments[segment.StartPoint] = new List<SegmentNode>();
                pointToSegments[segment.StartPoint].Add(segment);

                if (!pointToSegments.ContainsKey(segment.EndPoint))
                    pointToSegments[segment.EndPoint] = new List<SegmentNode>();
                pointToSegments[segment.EndPoint].Add(segment);
            }

            // Establish neighbors based on shared points
            foreach (var segment in Segments)
            {
                var startNeighbors = pointToSegments[segment.StartPoint];
                var endNeighbors = pointToSegments[segment.EndPoint];

                foreach (var neighbor in startNeighbors)
                {
                    if (neighbor != segment)
                        segment.AddNeighbor(neighbor);
                }

                foreach (var neighbor in endNeighbors)
                {
                    if (neighbor != segment)
                        segment.AddNeighbor(neighbor);
                }
            }
        }
        private void IdentifyConnectedComponents()
        {
            var visited = new HashSet<SegmentNode>();
            foreach (var segment in Segments)
            {
                if (!visited.Contains(segment))
                {
                    var component = new ConnectedComponent();
                    TraverseComponent(segment, visited, component);
                    ConnectedComponents.Add(component);
                }
            }
        }
        private void TraverseComponent(SegmentNode segment, HashSet<SegmentNode> visited, ConnectedComponent component)
        {
            var stack = new Stack<SegmentNode>();
            stack.Push(segment);

            while (stack.Count > 0)
            {
                var node = stack.Pop();
                if (visited.Contains(node)) continue;

                visited.Add(node);
                component.Segments.Add(node);

                foreach (var neighbor in node.Neighbors)
                    if (!visited.Contains(neighbor)) stack.Push(neighbor);
            }
        }
        private void MapRootPointsToComponents(List<Point2D> rootPoints)
        {
            var assignedComponents = new HashSet<ConnectedComponent>();

            foreach (var rootPoint in rootPoints)
            {
                var nearestSegment = FindNearestSegment(rootPoint);
                IntersectUtilities.UtilsCommon.Utils.DebugHelper.CreateDebugLine(
                    nearestSegment.GetMidpoint().To3d(), 
                    IntersectUtilities.UtilsCommon.Utils.ColorByName("red"));

                // Find the connected component containing the nearest segment
                ConnectedComponent component = null;
                foreach (var comp in ConnectedComponents)
                {
                    if (comp.Segments.Contains(nearestSegment))
                    {
                        component = comp;
                        break;
                    }
                }

                if (component != null)
                {
                    if (!assignedComponents.Contains(component))
                    {
                        assignedComponents.Add(component);
                        component.RootNode = nearestSegment;
                        RootNodes.Add(nearestSegment);
                    }
                    else
                    {
                        throw new Exception("Multiple root points assigned to the same connected component.");
                    }
                }
                else
                {
                    throw new Exception("Root point does not correspond to any connected component.");
                }
            }

            // Check if all components have been assigned a root point
            if (assignedComponents.Count != ConnectedComponents.Count)
            {
                throw new Exception("Number of root points does not correspond to the number of identified networks.");
            }
        }
        private void CheckForLeftoverSegments()
        {
            // All segments should be part of a connected component that has a root point assigned
            // If not, throw an exception indicating leftover segments
            var assignedSegments = new HashSet<SegmentNode>();
            foreach (var component in ConnectedComponents)
            {
                if (component.RootNode != null)
                {
                    assignedSegments.UnionWith(component.Segments);
                }
            }

            if (assignedSegments.Count != Segments.Count)
            {
                throw new Exception("There are leftover segments not connected to any root point.");
            }
        }
        // Find the nearest segment to a given point
        public SegmentNode FindNearestSegment(Point2D point)
        {
            return SpatialIndex.FindNearest(point);
        }
        public IEnumerable<Point2D> GetLeafNodePoints()
        {
            var pointToSegments = new Dictionary<Point2D, List<SegmentNode>>();
            foreach (var segment in Segments)
            {
                if (!pointToSegments.ContainsKey(segment.StartPoint))
                    pointToSegments[segment.StartPoint] = new List<SegmentNode>();
                pointToSegments[segment.StartPoint].Add(segment);

                if (!pointToSegments.ContainsKey(segment.EndPoint))
                    pointToSegments[segment.EndPoint] = new List<SegmentNode>();
                pointToSegments[segment.EndPoint].Add(segment);
            }

            foreach (var kvp in pointToSegments)
            {
                if (kvp.Value.Count == 1)
                {
                    yield return kvp.Key;
                }
            }
        }
        public IEnumerable<(Point3d Point, string Text)> GetSegmentsNumbering()
        {
            int groupNumber = 1;
            foreach (var component in ConnectedComponents)
            {
                int segmentCounter = 1;
                var visited = new HashSet<SegmentNode>();
                var stack = new Stack<SegmentNode>();

                // Get the root node for this component
                var rootNode = component.RootNode;
                if (rootNode == null)
                {
                    throw new Exception($"No root node assigned to connected component {groupNumber}");
                }

                stack.Push(rootNode);

                while (stack.Count > 0)
                {
                    var node = stack.Pop();
                    if (visited.Contains(node))
                        continue;

                    visited.Add(node);

                    string text = $"{groupNumber}.{segmentCounter:000}";
                    Point2D midpoint = node.GetMidpoint();
                    yield return (midpoint.To3d(), text);
                    segmentCounter++;

                    foreach (var neighbor in node.Neighbors)
                    {
                        if (!visited.Contains(neighbor))
                        {
                            stack.Push(neighbor);
                        }
                    }
                }

                groupNumber++;
            }
        }
    }
}
