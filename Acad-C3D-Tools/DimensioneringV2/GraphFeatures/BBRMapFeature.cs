using Mapsui.Nts;

using NetTopologySuite.Geometries;

namespace DimensioneringV2.GraphFeatures
{
    public sealed class BBRMapFeature : GeometryFeature
    {
        public string HeatingType { get; }
        public string Address { get; }
        public double OriginalX { get; }
        public double OriginalY { get; }

        public BBRMapFeature() : base()
        {
            HeatingType = string.Empty;
            Address = string.Empty;
        }

        public BBRMapFeature(BBRMapFeature source) : base(source)
        {
            HeatingType = source.HeatingType;
            Address = source.Address;
            OriginalX = source.OriginalX;
            OriginalY = source.OriginalY;
        }

        public BBRMapFeature(
            Point geometry,
            string heatingType,
            string address,
            double originalX,
            double originalY) : base(geometry)
        {
            HeatingType = heatingType;
            Address = address;
            OriginalX = originalX;
            OriginalY = originalY;
        }
    }
}
