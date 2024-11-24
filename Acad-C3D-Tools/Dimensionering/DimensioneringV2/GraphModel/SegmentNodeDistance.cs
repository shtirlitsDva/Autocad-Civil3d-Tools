using Dimensionering.DimensioneringV2.Geometry;
using Dimensionering.DimensioneringV2.GraphModelRoads;

using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Strtree;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dimensionering.DimensioneringV2.GraphModel
{
    internal class SegmentNodeDistance : IItemDistance<Envelope, SegmentNode>
    {
        private SpatialIndex noCrossIndex = null;
        public SegmentNodeDistance()
        {
        }
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