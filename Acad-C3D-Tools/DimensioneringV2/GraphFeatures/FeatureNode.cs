using NetTopologySuite.Features;
using NetTopologySuite.Geometries;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Geometry =  NetTopologySuite.Geometries.Geometry;

namespace DimensioneringV2.GraphFeatures
{
    internal class FeatureNode : IFeature
    {
        public NetTopologySuite.Geometries.Geometry Geometry { get; set; }
        public Envelope BoundingBox { get => this.Geometry.EnvelopeInternal; set => throw new NotImplementedException(); }
        public IAttributesTable Attributes { get; set; }
        public FeatureNode(NetTopologySuite.Geometries.Geometry geometry, IAttributesTable attributes)
        {
            this.Geometry = geometry;
            this.Attributes = attributes;
        }
    }
}
