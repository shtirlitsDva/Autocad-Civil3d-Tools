using Autodesk.AutoCAD.DatabaseServices;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dimensionering.DimensioneringV2.GraphModel
{
    internal class Graph
    {
        public List<SegmentNode> Segments { get; }
        public KdTree SpatialIndex { get; private set; }
        public List<SegmentNode> RootNodes { get; private set; }
        public List<List<SegmentNode>> ConnectedComponents { get; private set; }

        public Graph()
        {
            Segments = new List<SegmentNode>();
            SpatialIndex = new KdTree();
            RootNodes = new List<SegmentNode>();
            ConnectedComponents = new List<List<SegmentNode>>();
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

            SpatialIndex.BuildTree(Segments);

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
                    var component = new List<SegmentNode>();
                    TraverseComponent(segment, visited, component);
                    ConnectedComponents.Add(component);
                }
            }
        }
        private void TraverseComponent(SegmentNode segment, HashSet<SegmentNode> visited, List<SegmentNode> component)
        {
            var stack = new Stack<SegmentNode>();
            stack.Push(segment);

            while (stack.Count > 0)
            {
                var node = stack.Pop();
                if (visited.Contains(node)) continue;

                visited.Add(node);
                component.Add(node);

                foreach (var neighbor in node.Neighbors)
                    if (!visited.Contains(neighbor)) stack.Push(neighbor);
            }
        }
        private void MapRootPointsToComponents(List<Point2D> rootPoints)
        {
            var componentRootMap = new Dictionary<List<SegmentNode>, Point2D>();
            var assignedComponents = new HashSet<List<SegmentNode>>();

            foreach (var rootPoint in rootPoints)
            {
                var nearestSegment = FindNearestSegment(rootPoint);

                // Find the connected component containing the nearest segment
                List<SegmentNode> component = null;
                foreach (var comp in ConnectedComponents)
                {
                    if (comp.Contains(nearestSegment))
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
                        componentRootMap[component] = rootPoint;
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
                assignedSegments.UnionWith(component);
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
    }
}
