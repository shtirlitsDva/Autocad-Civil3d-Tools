using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using NTRExport.Spines;
using NTRExport.TopologyModel;

namespace NTRExport.Elevation
{
    internal sealed class SpineElevationProvider : IElevationProvider
    {
        private readonly List<SpinePath> _spines;

        public SpineElevationProvider(IEnumerable<SpinePath> spines)
        {
            _spines = spines.ToList();
        }

        public double GetZ(ElementBase element, Point3d a, Point3d b, double t)
        {
            // Try to find a spine segment with the same source handle.
            var seg = FindSegmentBySource(element.Source);
            if (seg != null)
            {
                var z0 = seg.A.Z;
                var z1 = seg.B.Z;
                return z0 + t * (z1 - z0);
            }

            // Fallback: use plan endpoints
            var zStart = a.Z;
            var zEnd = b.Z;
            return zStart + t * (zEnd - zStart);
        }

        private SpineSegment? FindSegmentBySource(Handle source)
        {
            foreach (var p in _spines)
            {
                foreach (var s in p.Segments)
                {
                    if (s.Source == source) return s;
                }
            }
            return null;
        }
    }
}


