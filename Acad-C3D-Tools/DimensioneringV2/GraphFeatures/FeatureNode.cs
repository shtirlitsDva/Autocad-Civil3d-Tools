using Mapsui.Styles;
using Mapsui.Nts;

using NetTopologySuite.Features;
using NetTopologySuite.Geometries;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Geometry = NetTopologySuite.Geometries.Geometry;

namespace DimensioneringV2.GraphFeatures
{
    internal class FeatureNode : IFeature, Mapsui.IFeature
    {
        public NetTopologySuite.Geometries.Geometry Geometry { get; set; }
        public Envelope BoundingBox { get => this.Geometry.EnvelopeInternal; set => throw new NotImplementedException(); }
        public IAttributesTable Attributes { get; set; }

        public ICollection<IStyle> Styles => new List<IStyle>();

        public IEnumerable<string> Fields => Attributes.GetNames();

        public Mapsui.MRect? Extent
        {
            get
            {
                if (Geometry == null) return null;
                var envelope = Geometry.EnvelopeInternal;
                return new Mapsui.MRect(envelope.MinX, envelope.MinY, envelope.MaxX, envelope.MaxY);
            }
        }

        public IDictionary<IStyle, object> RenderedGeometry => new Dictionary<IStyle, object>();

        public object? this[string key] 
        { 
            get => Attributes[key];
            set => Attributes[key] = value;
        }

        public FeatureNode(NetTopologySuite.Geometries.Geometry geometry, IAttributesTable attributes)
        {
            this.Geometry = geometry;
            this.Attributes = attributes;
        }

        public void CoordinateVisitor(Action<double, double, Mapsui.CoordinateSetter> visit)
        {
            if (Geometry == null)
                return;

            var filter = new CoordinateVisitorFilter(visit);
            Geometry.Apply(filter);
        }

        public void Dispose()
        {
            
        }

        private class CoordinateVisitorFilter : NetTopologySuite.Geometries.ICoordinateFilter
        {
            private readonly Action<double, double, Mapsui.CoordinateSetter> _visit;

            public CoordinateVisitorFilter(Action<double, double, Mapsui.CoordinateSetter> visit)
            {
                _visit = visit;
            }

            public void Filter(NetTopologySuite.Geometries.Coordinate coord)
            {
                double x = coord.X;
                double y = coord.Y;

                void setter(double newX, double newY)
                {
                    coord.X = newX;
                    coord.Y = newY;
                }

                _visit(x, y, setter);
            }
        }
    }
}
