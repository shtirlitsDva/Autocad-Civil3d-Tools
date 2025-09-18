using DimensioneringV2.Common;

using NetTopologySuite.LinearReferencing;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DimensioneringV2.Services.GDALClient
{
    internal sealed class ElevationDispatcher : DispatcherBase
    {
        private long _nextGeomId = 1;
        private readonly ConcurrentDictionary<ElevationProfileCache, long> _ids = new();

        private long GetOrAssignId(ElevationProfileCache cache)
            => _ids.GetOrAdd(cache, _ => Interlocked.Increment(ref _nextGeomId));

        private sealed record ReqPoint(long geomId, int seq, double s, double x, double y);
        public sealed record Row(long geomId, int seq, double s, double x, double y, double elev, string status);
        private sealed record SampleRowsDto(Row[] rows);

        public async Task<OpResult<int>> SampleAsync(
            IEnumerable<ElevationProfileCache> caches,
            double spacingMeters,
            int? maxThreads = null,
            IProgress<(int done, int total)>? progress = null,
            CancellationToken ct = default)
        {
            var ensure = await EnsureReadyAndProjectAsync(ct).ConfigureAwait(false);
            if (!ensure.Ok) return OpResult<int>.Fail(ensure.Error!);
            if (spacingMeters <= 0) return OpResult<int>.Fail("Sampling-afstand skal være > 0 m.");

            var reqs = new List<ReqPoint>(1 << 16);

            foreach (var cache in caches)
            {
                ct.ThrowIfCancellationRequested();
                long gid = GetOrAssignId(cache);

                var ls = cache.FullGeometry25832;
                var lil = new LengthIndexedLine(ls);
                double len = cache.LengthMeters;

                int seq = 0;
                for (double s = 0.0; s <= len; s += spacingMeters)
                {
                    double idx = (s < len) ? s : len;
                    var p = lil.ExtractPoint(idx);
                    reqs.Add(new ReqPoint(gid, seq++, idx, p.X, p.Y));
                }

                double remainder = len % spacingMeters;
                if (remainder > 0.001)
                    reqs.Add(new ReqPoint(gid, seq++, len, ls.EndPoint.X, ls.EndPoint.Y));
            }

            SampleRowsDto dto;
            try
            {
                dto = await Rpc.CallAsync<SampleRowsDto>(
                    "SAMPLE_POINTS",
                    new { threads = maxThreads ?? 0, points = reqs },
                    ct, progress).ConfigureAwait(false);
            }
            catch (RpcException ex)
            {
                return OpResult<int>.Fail($"SAMPLE_POINTS fejlede: {ex.Message}");
            }

            var inv = _ids.ToDictionary(kv => kv.Value, kv => kv.Key);
            var byCache = new Dictionary<ElevationProfileCache, List<ElevationSample>>();
            
            foreach (var r in dto.rows.OrderBy(t => t.geomId).ThenBy(t => t.seq))
            {
                if (!inv.TryGetValue(r.geomId, out var cache)) continue;

                if (!byCache.TryGetValue(cache, out var list))
                    byCache[cache] = list = new List<ElevationSample>();

                list.Add(new ElevationSample(r.s, r.elev, r.x, r.y));                
            }

            foreach (var (cache, forward) in byCache)
                cache.AcceptElevationProfile(forward);
            
            return OpResult<int>.Success(byCache.Count);
        }

        public async Task<OpResult<double?>> SamplePointAsync(double x, double y, CancellationToken ct = default)
        {
            var ensure = await EnsureReadyAndProjectAsync(ct).ConfigureAwait(false);
            if (!ensure.Ok) return OpResult<double?>.Fail(ensure.Error!);

            try
            {
                var dto = await Rpc.CallAsync<SampleRowsDto>(
                    "SAMPLE_POINTS",
                    new { threads = 1, points = new[] { new ReqPoint(0, 0, 0.0, x, y) } },
                    ct).ConfigureAwait(false);

                var r = dto.rows[0];
                return OpResult<double?>.Success(r.status == "OK" ? r.elev : (double?)null);
            }
            catch (Exception ex)
            {
                return OpResult<double?>.Fail($"Punkt-sampling fejlede: {ex.Message}");
            }
        }
    }
}
