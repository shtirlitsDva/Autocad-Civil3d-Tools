using Mapsui.Styles;
using Mapsui.Nts;

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
    internal class FeatureNode : IFeature, Mapsui.IFeature
    {
        public NetTopologySuite.Geometries.Geometry Geometry { get; set; }
        public Envelope BoundingBox { get => this.Geometry.EnvelopeInternal; set => throw new NotImplementedException(); }
        public IAttributesTable Attributes { get; set; }

        public ICollection<IStyle> Styles => throw new NotImplementedException();

        public IEnumerable<string> Fields => throw new NotImplementedException();

        public Mapsui.MRect? Extent => throw new NotImplementedException();

        public IDictionary<IStyle, object> RenderedGeometry => throw new NotImplementedException();

        public object? this[string key] { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public FeatureNode(NetTopologySuite.Geometries.Geometry geometry, IAttributesTable attributes)
        {
            this.Geometry = geometry;
            this.Attributes = attributes;
        }

        public void CoordinateVisitor(Action<double, double, Mapsui.CoordinateSetter> visit)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
