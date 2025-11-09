using Autodesk.AutoCAD.Geometry;

using NTRExport.TopologyModel;

namespace NTRExport.Elevation
{
    internal sealed class FlatElevationProvider : IElevationProvider
    {
        public double GetZ(ElementBase element, Point3d a, Point3d b, double t)
        {
            // Default: keep existing plan Z (usually 0) if present on endpoints, interpolate.
            var z0 = a.Z;
            var z1 = b.Z;
            return z0 + t * (z1 - z0);
        }
    }
}


