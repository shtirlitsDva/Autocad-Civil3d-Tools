using Dimensionering.DimensioneringV2.Geometry;
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

        public void BuildIndex(List<SegmentNode> segments)
        {
            rtree = new STRtree<SegmentNode>();

            foreach (var segment in segments)
            {
                var line = segment.ToLineString();
                rtree.Insert(line.EnvelopeInternal, segment);
            }

            rtree.Build();
        }

        public SegmentNode FindNearest(Point2D point)
        {
            var queryPoint = new Point(point.X, point.Y);

            // Define a small initial search envelope
            double searchRadius = 1.0; // Adjust as needed based on your coordinate system

            while (true)
            {
                var envelope = new Envelope(
                    point.X - searchRadius, point.X + searchRadius,
                    point.Y - searchRadius, point.Y + searchRadius);

                var candidates = rtree.Query(envelope);

                if (candidates.Count > 0)
                {
                    // Find the nearest segment among the candidates
                    SegmentNode nearestSegment = null;
                    double minDistance = double.MaxValue;

                    foreach (var segment in candidates)
                    {
                        double dist = segment.DistanceToPoint(point);
                        if (dist < minDistance)
                        {
                            minDistance = dist;
                            nearestSegment = segment;
                        }
                    }

                    return nearestSegment;
                }

                // Increase the search radius
                searchRadius *= 2;

                // Optional: Set a maximum search radius to prevent infinite loops
                if (searchRadius > 10000) // Adjust based on your data extent
                {
                    throw new Exception("No segment found within the maximum search distance.");
                }
            }
        }
    }
}
