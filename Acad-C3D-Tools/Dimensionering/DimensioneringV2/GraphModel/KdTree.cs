using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dimensionering.DimensioneringV2.GraphModel
{
    internal class KdTree
    {
        private KdTreeNode root;

        // Build the KD-tree from a list of segments
        public void BuildTree(List<SegmentNode> segments)
        {
            root = BuildTreeRecursive(segments, depth: 0);
        }

        private KdTreeNode BuildTreeRecursive(List<SegmentNode> segments, int depth)
        {
            if (segments.Count == 0)
                return null;

            int axis = depth % 2; // 0 for X-axis, 1 for Y-axis

            // Sort segments based on the midpoint coordinate on the current axis
            segments.Sort((a, b) =>
            {
                var aMid = a.GetMidpoint();
                var bMid = b.GetMidpoint();
                return axis == 0 ? aMid.X.CompareTo(bMid.X) : aMid.Y.CompareTo(bMid.Y);
            });

            int medianIndex = segments.Count / 2;
            var node = new KdTreeNode
            {
                Segment = segments[medianIndex],
                Depth = depth,
                Left = BuildTreeRecursive(segments.GetRange(0, medianIndex), depth + 1),
                Right = BuildTreeRecursive(segments.GetRange(medianIndex + 1, segments.Count - (medianIndex + 1)), depth + 1)
            };

            return node;
        }

        // Find the nearest segment to a given point
        public SegmentNode FindNearest(Point2D target)
        {
            return FindNearestRecursive(root, target, best: null, bestDistanceSquared: double.MaxValue);
        }

        private SegmentNode FindNearestRecursive(KdTreeNode node, Point2D target, SegmentNode best, double bestDistanceSquared)
        {
            if (node == null)
                return best;

            double distSq = node.Segment.DistanceSquaredToPoint(target);
            if (distSq < bestDistanceSquared)
            {
                bestDistanceSquared = distSq;
                best = node.Segment;
            }

            int axis = node.Depth % 2;
            double pointCoord = axis == 0 ? target.X : target.Y;
            double nodeCoord = axis == 0 ? node.Segment.GetMidpoint().X : node.Segment.GetMidpoint().Y;

            KdTreeNode nextNode = pointCoord < nodeCoord ? node.Left : node.Right;
            KdTreeNode otherNode = pointCoord < nodeCoord ? node.Right : node.Left;

            best = FindNearestRecursive(nextNode, target, best, bestDistanceSquared);

            // Check if we need to explore the other side of the tree
            double planeDistSq = (pointCoord - nodeCoord) * (pointCoord - nodeCoord);
            if (planeDistSq < bestDistanceSquared)
            {
                best = FindNearestRecursive(otherNode, target, best, bestDistanceSquared);
            }

            return best;
        }
    }
}
