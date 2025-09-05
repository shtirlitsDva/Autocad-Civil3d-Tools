using DimensioneringV2.GraphFeatures;

using NetTopologySuite.Geometries;
using NetTopologySuite.LinearReferencing;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.Services.Elevations
{
    internal sealed class ElevationProfileCache
    {
        private readonly OriginalGeometry _og;   // FullGeometry in EPSG:25832
        private readonly object _lock = new();
        private List<ElevationSample>? _forward;
        private double _length;

        public ElevationProfileCache(OriginalGeometry og)
        {
            _og = og ?? throw new ArgumentNullException(nameof(og));
            _length = og.FullGeometry.Length;
            if (_length <= 0) throw new ArgumentException("FullGeometry must have positive length.");
        }

        public LineString FullGeometry25832 => _og.FullGeometry;
        public double LengthMeters => _length;
        public void AcceptElevationProfile(IReadOnlyList<ElevationSample> forward, double lengthMeters)
        {
            lock (_lock)
            {
                _forward = [.. forward];
                _length = lengthMeters;
            }
        }
        public IReadOnlyList<ElevationSample> GetProfile(Coordinate start3857, Coordinate end3857)
        {
            EnsureSampledForwardOnce(); // fallback if not prewarmed
            var fwd = _forward!;
            var len = _length;

            var gs = _og.FullGeometry.StartPoint.Coordinate;
            var ge = _og.FullGeometry.EndPoint.Coordinate;
            var (ex, ey) = Normalize(ge.X - gs.X, ge.Y - gs.Y);
            var (vx, vy) = Normalize(end3857.X - start3857.X, end3857.Y - start3857.Y);
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
        public IReadOnlyList<ElevationSample> GetDefaultProfile()
        {
            EnsureSampledForwardOnce(); // fallback if not prewarmed
            return _forward!;
        }
        private void EnsureSampledForwardOnce()
        {
            if (_forward != null) return;
            lock (_lock)
            {
                if (_forward != null) return;

                // local (non-parallel) fallback
                var es = ElevationService.Instance;
                es.PublishElevationData();

                var ls = _og.FullGeometry;
                var lil = new LengthIndexedLine(ls);
                var outList = new List<ElevationSample>((int)Math.Ceiling(_length) + 1);
                for (double s = 0.0; s <= _length; s += 1.0)
                {
                    double idx = (s < _length) ? s : _length;
                    var p = lil.ExtractPoint(idx);
                    double elev = es.SampleElevation25832(p.X, p.Y) ?? double.NaN;
                    outList.Add(new ElevationSample(s, elev, p.X, p.Y));
                }
                _forward = outList;
            }
        }
        private static (double, double) Normalize(double x, double y)
        { var L = Math.Sqrt(x * x + y * y); return L > 0 ? (x / L, y / L) : (0, 0); }
    }
}
