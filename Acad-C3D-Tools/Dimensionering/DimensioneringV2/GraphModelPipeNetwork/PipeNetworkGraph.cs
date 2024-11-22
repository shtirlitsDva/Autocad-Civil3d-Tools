using Dimensionering.DimensioneringV2.Geometry;
using Dimensionering.DimensioneringV2.GraphModelRoads;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dimensionering.DimensioneringV2.GraphModelPipeNetwork
{
    internal class PipeNetworkGraph
    {
        public List<PipeNode> Nodes { get; }
        public List<PipeSegment> Segments { get; }

        private Graph roadNetwork;

        public PipeNetworkGraph()
        {
            Nodes = new List<PipeNode>();
            Segments = new List<PipeSegment>();
        }

        public void BuildGraph(Graph roadNetwork, List<Building> buildings)
        {
            this.roadNetwork = roadNetwork;

            // Step 1: Project buildings onto the road network
            var projectedPoints = ProjectBuildingsOntoRoadNetwork(buildings);

            // Step 2: Create nodes and service lines from buildings to projected points
            CreateBuildingConnections(buildings, projectedPoints);

            // Step 3: Build the pipe network between projected points along the road network
            BuildPipeNetworkBetweenProjectedPoints(projectedPoints);
        }

        private Dictionary<Building, (PipeNode buildingNode, PipeNode projectedNode)> ProjectBuildingsOntoRoadNetwork(List<Building> buildings)
        {
            var result = new Dictionary<Building, (PipeNode, PipeNode)>();

            foreach (var building in buildings)
            {
                // Find the nearest segment in the road network
                var nearestSegment = roadNetwork.FindNearestSegment(building.Location);

                // Get the closest point on the segment to the building
                var projectedPoint = nearestSegment.GetClosestPoint(building.Location);

                // Create or retrieve the pipe node at the projected point
                var projectedNode = GetOrCreatePipeNode(projectedPoint);

                // Create the building node
                var buildingNode = new PipeNode(building.Location, isBuildingConnection: true);
                Nodes.Add(buildingNode);

                // Add to result
                result.Add(building, (buildingNode, projectedNode));
            }

            return result;
        }

        private void CreateBuildingConnections(List<Building> buildings, Dictionary<Building, (PipeNode buildingNode, PipeNode projectedNode)> projectedPoints)
        {
            foreach (var building in buildings)
            {
                var (buildingNode, projectedNode) = projectedPoints[building];

                // Create a service line from the building to the projected point
                var geometry = new List<Point2D> { buildingNode.Location, projectedNode.Location };
                var serviceSegment = new PipeSegment(buildingNode, projectedNode, geometry);
                Segments.Add(serviceSegment);
            }
        }

        private void BuildPipeNetworkBetweenProjectedPoints(Dictionary<Building, (PipeNode buildingNode, PipeNode projectedNode)> projectedPoints)
        {
            // Collect all projected nodes
            var projectedNodes = new HashSet<PipeNode>(projectedPoints.Values.Select(t => t.projectedNode));

            // Build a list of projected points
            var projectedPointsList = projectedNodes.Select(node => node.Location).ToList();

            // Build the minimal spanning tree (MST) connecting all projected points along the road network
            BuildMinimalSpanningTree(projectedNodes);
        }

        private void BuildMinimalSpanningTree(HashSet<PipeNode> projectedNodes)
        {
            // Implement Prim's algorithm or Kruskal's algorithm to build the MST
            // For simplicity, let's assume we have a method to get the distance between two points along the road network

            // Build a graph where nodes are projected points and edges are shortest paths along the road network

            var mstEdges = new List<(PipeNode nodeA, PipeNode nodeB, List<Point2D> path, double length)>();

            // For demonstration purposes, we'll connect each projected node to its nearest neighbor
            // In practice, you should use an algorithm like Prim's or Kruskal's to build the MST

            var nodesList = projectedNodes.ToList();
            var visited = new HashSet<PipeNode> { nodesList[0] };
            var remainingNodes = new HashSet<PipeNode>(nodesList.Skip(1));

            while (remainingNodes.Count > 0)
            {
                double minDistance = double.MaxValue;
                PipeNode minNodeA = null;
                PipeNode minNodeB = null;
                List<Point2D> minPath = null;

                foreach (var nodeA in visited)
                {
                    foreach (var nodeB in remainingNodes)
                    {
                        // Find shortest path along the road network
                        var path = roadNetwork.FindShortestPath(nodeA.Location, nodeB.Location, out double pathLength);

                        if (path != null && pathLength < minDistance)
                        {
                            minDistance = pathLength;
                            minNodeA = nodeA;
                            minNodeB = nodeB;
                            minPath = path;
                        }
                    }
                }

                if (minNodeA != null && minNodeB != null && minPath != null)
                {
                    // Create pipe segments along the path
                    CreatePipeSegmentsFromPath(minNodeA, minNodeB, minPath);

                    visited.Add(minNodeB);
                    remainingNodes.Remove(minNodeB);
                }
                else
                {
                    // No path found (should not happen if the road network is connected)
                    break;
                }
            }
        }

        private void CreatePipeSegmentsFromPath(PipeNode startNode, PipeNode endNode, List<Point2D> path)
        {
            // Create nodes along the path if they don't already exist
            var pathNodes = new List<PipeNode> { startNode };

            for (int i = 1; i < path.Count - 1; i++)
            {
                var point = path[i];
                var node = GetOrCreatePipeNode(point);
                pathNodes.Add(node);
            }

            pathNodes.Add(endNode);

            // Create pipe segments between consecutive nodes
            for (int i = 0; i < pathNodes.Count - 1; i++)
            {
                var nodeA = pathNodes[i];
                var nodeB = pathNodes[i + 1];

                // Check if segment already exists
                if (!nodeA.ConnectedSegments.Any(seg => seg.EndNode == nodeB))
                {
                    var segmentGeometry = new List<Point2D> { nodeA.Location, nodeB.Location };
                    var segment = new PipeSegment(nodeA, nodeB, segmentGeometry);
                    Segments.Add(segment);
                }
            }
        }

        private PipeNode GetOrCreatePipeNode(Point2D location)
        {
            var node = Nodes.FirstOrDefault(n => n.Location.Equals(location));
            if (node == null)
            {
                node = new PipeNode(location);
                Nodes.Add(node);
            }
            return node;
        }
    }

}
