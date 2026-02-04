using DimensioneringV2.Geometry;
using DimensioneringV2.GraphModel;

using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Strtree;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.GraphModelRoads
{
    internal class SpatialIndex
    {
        private STRtree<SegmentNode> rtree;

        public SpatialIndex()
        {
            rtree = new STRtree<SegmentNode>();
        }

        public void Insert(List<SegmentNode> segments)
        {
            foreach (var segment in segments)
            {
                Insert(segment);
            }
        }

        public SegmentNode FindNearest(Point2D point)
        {
            // Create a zero-length SegmentNode at the query point
            var querySegment = new SegmentNode(point, point);

            var itemDistance = new SegmentNodeDistance();

            var nearestSegment = rtree.NearestNeighbour(querySegment.Bounds, querySegment, itemDistance);

            return nearestSegment;
        }

        public SegmentNode FindNearest(SegmentNode segment)
        {
            var itemDistance = new SegmentNodeDistance();

            var nearestSegment = rtree.NearestNeighbour(segment.Bounds, segment, itemDistance);

            return nearestSegment;
        }

        /// <summary>
        /// Finds the nearest segment to a point while avoiding projection lines that cross forbidden segments.
        /// </summary>
        /// <param name="point">The query point to find the nearest segment to.</param>
        /// <param name="noCrossIndex">
        /// A spatial index containing "forbidden" segments (e.g., roads, railways) that the projection
        /// line must not cross. The projection line is the line from the query point to the closest
        /// point on a candidate segment.
        /// </param>
        /// <returns>
        /// The nearest segment whose projection line does not intersect any segment in noCrossIndex.
        /// If a candidate's projection line crosses a forbidden segment, it is disqualified by returning
        /// double.MaxValue from the distance calculation, causing the R-tree to skip it and find the
        /// next nearest valid candidate.
        /// </returns>
        /// <remarks>
        /// This is useful for connecting buildings to pipe networks where service lines should not
        /// cross obstacles like roads. The noCrossIndex contains obstacle geometries, and this index
        /// contains the pipe segments to connect to.
        /// </remarks>
        public SegmentNode FindNearest(Point2D point, SpatialIndex noCrossIndex)
        {
            // Create a zero-length SegmentNode at the query point
            var querySegment = new SegmentNode(point, point);

            var itemDistance = new SegmentNodeDistance(noCrossIndex);

            var nearestSegment = rtree.NearestNeighbour(querySegment.Bounds, querySegment, itemDistance);

            return nearestSegment;
        }

        public void Insert(SegmentNode segment)
        {
            var line = segment.ToLineString();
            rtree.Insert(line.EnvelopeInternal, segment);
        }
    }
}
