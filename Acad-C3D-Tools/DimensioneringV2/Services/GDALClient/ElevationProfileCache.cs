using DimensioneringV2.GraphFeatures;

using NetTopologySuite.Geometries;
using NetTopologySuite.LinearReferencing;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.Services.GDALClient
{
    internal sealed class ElevationProfileCache
    {
        public AnalysisFeature Owner { get; init; }
        private readonly OriginalGeometry _og;   // FullGeometry in EPSG:25832
        private readonly object _lock = new();
        private List<ElevationSample>? _forward;
        public bool Sampled => _forward != null && _forward.Count != 0;

        public ElevationProfileCache(AnalysisFeature owner, OriginalGeometry og)
        {
            this.Owner = owner;
            _og = og ?? throw new ArgumentNullException(nameof(og));
        }

        public LineString FullGeometry25832 => _og.FullGeometry;
        public double LengthMeters => _og.Length;
        public void AcceptElevationProfile(IReadOnlyList<ElevationSample> forward)
        {
            lock (_lock) { _forward = [.. forward]; }
        }
        public IReadOnlyList<ElevationSample> GetProfile(Coordinate start, Coordinate end)
        {            
            var fwd = _forward!;
            var len = _og.Length;

            var gs = _og.FullGeometry.StartPoint.Coordinate;
            var ge = _og.FullGeometry.EndPoint.Coordinate;
            var (ex, ey) = Normalize(ge.X - gs.X, ge.Y - gs.Y);
            var (vx, vy) = Normalize(end.X - start.X, end.Y - start.Y);
            double dot = ex * vx + ey * vy;

            if (dot >= 0) return fwd;

            var rev = new List<ElevationSample>(fwd.Count);
            for (int i = fwd.Count - 1; i >= 0; i--)
            {
                var s = fwd[i];
                rev.Add(new ElevationSample(len - s.Station, s.Elevation, s.X, s.Y));
            }
            return rev;
        }
        public IReadOnlyList<ElevationSample> GetDefaultProfile() => _forward!;
        
        private static (double, double) Normalize(double x, double y)
        { var L = Math.Sqrt(x * x + y * y); return L > 0 ? (x / L, y / L) : (0, 0); }
    }
}
