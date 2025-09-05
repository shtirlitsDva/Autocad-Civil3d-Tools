using NetTopologySuite.LinearReferencing;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DimensioneringV2.Services.Elevations
{
    internal class ElevationDispatcher
    {
        private sealed record Request(int Index, ElevationProfileCache Owner, double Station, double X, double Y);

        public static async Task PreSampleAsync(
            IEnumerable<ElevationProfileCache> caches,
            int maxDegreeOfParallelism = 0,
            IProgress<(int done, int total)>? progress = null,
            CancellationToken ct = default)
        {
            // 1) Flatten all points to sample (forward direction, include endpoint)
            var reqs = new List<Request>(capacity: 1 << 16);
            foreach (var cache in caches)
            {
                ct.ThrowIfCancellationRequested();

                var ls = cache.FullGeometry25832;
                double len = cache.LengthMeters;

                var lil = new LengthIndexedLine(ls);
                int startIndex = reqs.Count;

                for (double s = 0.0; s <= len; s += 1.0)
                {
                    double idx = (s < len) ? s : len;
                    var p = lil.ExtractPoint(idx);
                    reqs.Add(new Request(Index: reqs.Count, Owner: cache, Station: s, X: p.X, Y: p.Y));
                }
            }

            // 2) Build the bare (x,y) list for the service
            var points = new PointXY[reqs.Count];
            for (int i = 0; i < reqs.Count; i++)
                points[i] = new PointXY(reqs[i].X, reqs[i].Y);

            // 3) Ask ElevationService to sample in bulk (controlled parallelism + progress)
            var es = ElevationService.Instance;
            var elevations = await es.SampleBulkAsync(points, maxDegreeOfParallelism, progress, ct).ConfigureAwait(false);

            // 4) Reassemble results per cache and push forward profiles
            // Group by owner while preserving order
            var byOwner = new Dictionary<ElevationProfileCache, List<ElevationSample>>();
            for (int i = 0; i < reqs.Count; i++)
            {
                ct.ThrowIfCancellationRequested();

                var r = reqs[i];
                var z = elevations[i] ?? double.NaN;

                if (!byOwner.TryGetValue(r.Owner, out var list))
                {
                    list = new List<ElevationSample>();
                    byOwner[r.Owner] = list;
                }
                list.Add(new ElevationSample(r.Station, z, r.X, r.Y));
            }

            foreach (var kv in byOwner)
            {
                var cache = kv.Key;
                var forward = kv.Value;
                cache.AcceptElevationProfile(forward, cache.LengthMeters);
            }
        }
    }
}
