using DimensioneringV2.Geometry;
using DimensioneringV2.GraphModelRoads;

using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Strtree;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.GraphModel
{
    /// <summary>
    /// Custom distance metric for R-tree nearest neighbor searches that supports filtering out
    /// candidates whose projection lines cross forbidden segments.
    /// </summary>
    /// <remarks>
    /// Implements <see cref="IItemDistance{Envelope, SegmentNode}"/> to customize how the R-tree
    /// evaluates distances between segment candidates. When a noCrossIndex is provided, the distance
    /// calculation will return double.MaxValue for any candidate whose projection line intersects
    /// a segment in the noCrossIndex, effectively disqualifying it from the search.
    /// </remarks>
    internal class SegmentNodeDistance : IItemDistance<Envelope, SegmentNode>
    {
        private SpatialIndex noCrossIndex = null;

        public SegmentNodeDistance()
        {
        }

        /// <summary>
        /// Creates a distance metric that filters out candidates crossing forbidden segments.
        /// </summary>
        /// <param name="noCrossIndex">
        /// Spatial index containing segments that projection lines must not cross.
        /// When evaluating a candidate, if the line from query point to closest point on
        /// the candidate intersects any segment in this index, the candidate is disqualified.
        /// </param>
        public SegmentNodeDistance(SpatialIndex noCrossIndex)
        {
            this.noCrossIndex = noCrossIndex;
        }
        double IItemDistance<Envelope, SegmentNode>.Distance(IBoundable<Envelope, SegmentNode> item1, IBoundable<Envelope, SegmentNode> item2)
        {
            SegmentNode node1 = item1.Item;
            SegmentNode node2 = item2.Item;

            return node1.DistanceToSegment(node2, noCrossIndex);
        }
    }
}