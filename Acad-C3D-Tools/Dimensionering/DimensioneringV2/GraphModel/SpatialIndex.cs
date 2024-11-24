using Dimensionering.DimensioneringV2.Geometry;
using Dimensionering.DimensioneringV2.GraphModel;

using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Strtree;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dimensionering.DimensioneringV2.GraphModelRoads
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
