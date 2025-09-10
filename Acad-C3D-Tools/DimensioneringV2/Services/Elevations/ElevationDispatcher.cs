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

namespace DimensioneringV2.Services.Elevations
{
    internal sealed class ElevationDispatcher
    {
        private long _nextGeomId = 1;
        private readonly ConcurrentDictionary<ElevationProfileCache, long> _ids = new();

        private long GetOrAssignId(ElevationProfileCache cache)
            => _ids.GetOrAdd(cache, _ => Interlocked.Increment(ref _nextGeomId));

        private sealed record RequestRow(long GeomId, int Seq, double S, double X, double Y);

        /// <summary>Ensures GDAL service is ready and a project is set based on current DWG + ElevationSettings.</summary>
        public async Task<OpResult<(string projectId, string basePath)>> EnsureReadyAndProjectAsync(
            CancellationToken ct = default)
        {
            // Load settings (project id)
            var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            var settings = SettingsSerializer<ElevationSettings>.Load(doc);
            if (string.IsNullOrWhiteSpace(settings?.BaseFileName))
                return OpResult<(string, string)>.Fail(
                    "Ingen terræn-projekt-id for denne tegning. Hent først terrændata.");

            string projectId = settings!.BaseFileName!;
            // Resolve base path from drawing filename
            var dbFile = doc.Database.Filename;
            if (string.IsNullOrWhiteSpace(dbFile))
                return OpResult<(string, string)>.Fail("Kan ikke finde DWG-filen på disk.");

            string basePath = Path.GetDirectoryName(dbFile)!;

            try
            {
                await ElevationService.Instance.EnsureServerAsync(ct);
                await ElevationService.Instance.EnsureProjectAsync(
                    projectId, basePath, ct).ConfigureAwait(false);
                return OpResult<(string, string)>.Success((projectId, basePath));
            }
            catch (Exception ex)
            {
                return OpResult<(string, string)>.Fail(
                    $"GDALService kunne ikke initialiseres: {ex.Message}");
            }
        }

        /// <summary>Sample ALL caches at a given spacing (meters) and populate caches. Returns number of caches populated.</summary>
        public async Task<OpResult<int>> SampleAsync(
            IEnumerable<ElevationProfileCache> caches,
            double spacingMeters,
            int? maxThreads = null,
            IProgress<(int done, int total)>? progress = null,
            CancellationToken ct = default)
        {
            // Ensure project/service
            var ensure = await EnsureReadyAndProjectAsync(ct).ConfigureAwait(false);
            if (!ensure.Ok) return OpResult<int>.Fail(ensure.Error!);

            if (spacingMeters <= 0) return OpResult<int>.Fail("Sampling-afstand skal være > 0 m.");

            // 1) Flatten requests (forward order, include exact endpoint)
            var reqs = new List<RequestRow>(1 << 16);
            int cachesCount = 0;

            foreach (var cache in caches)
            {
                ct.ThrowIfCancellationRequested();
                cachesCount++;

                long gid = GetOrAssignId(cache);

                var ls = cache.FullGeometry25832;
                var lil = new LengthIndexedLine(ls);
                double len = cache.LengthMeters;

                int seq = 0;
                for (double s = 0.0; s <= len; s += spacingMeters)
                {
                    double idx = (s < len) ? s : len;
                    var p = lil.ExtractPoint(idx);
                    reqs.Add(new RequestRow(gid, seq++, idx, p.X, p.Y));
                }

                //Add end point as it is (almost) never added in the loop
                double remainder = len % spacingMeters;
                if (remainder > 0.001)
                    reqs.Add(new RequestRow(gid, seq++, len, ls.EndPoint.X, ls.EndPoint.Y));
            }

            // 2) Call GDALService (tagged points). Catch and report failures.
            IReadOnlyList<(long geomId, int seq, double s, double x, double y, double elev, string status)> tagged;
            try
            {
                tagged = await ElevationService.Instance
                    .SampleTaggedAsync(reqs.Select(r => (r.GeomId, r.Seq, r.S, r.X, r.Y)), threads: maxThreads, ct: ct)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return OpResult<int>.Fail($"SAMPLE_POINTS fejlede: {ex.Message}");
            }

            // 3) Reassemble per cache and push
            var inv = _ids.ToDictionary(kv => kv.Value, kv => kv.Key);
            var byCache = new Dictionary<ElevationProfileCache, List<ElevationSample>>();

            // If you want progress, cheaply count lines processed in batches (no UI yet)
            int done = 0, total = tagged.Count;
            foreach (var r in tagged.OrderBy(t => t.geomId).ThenBy(t => t.seq))
            {
                if (!inv.TryGetValue(r.geomId, out var cache)) continue;

                if (!byCache.TryGetValue(cache, out var list))
                {
                    list = new List<ElevationSample>();
                    byCache[cache] = list;
                }
                list.Add(new ElevationSample(r.s, r.elev, r.x, r.y));

                if (progress != null)
                {
                    done++;
                    if ((done & 0x03FF) == 0) progress.Report((done, total)); //1024 lines
                }
            }

            foreach (var (cache, forward) in byCache)
                cache.AcceptElevationProfile(forward);

            progress?.Report((total, total));
            return OpResult<int>.Success(byCache.Count);
        }

        /// <summary>Sample a single 25832 point via GDALService.</summary>
        public async Task<OpResult<double?>> SamplePointAsync(double x, double y, CancellationToken ct = default)
        {
            var ensure = await EnsureReadyAndProjectAsync(ct).ConfigureAwait(false);
            if (!ensure.Ok) return OpResult<double?>.Fail(ensure.Error!);

            try
            {
                var v = await ElevationService.Instance.SampleSingleAsync(x, y, ct).ConfigureAwait(false);
                return OpResult<double?>.Success(v);
            }
            catch (Exception ex)
            {
                return OpResult<double?>.Fail($"Punkt-sampling fejlede: {ex.Message}");
            }
        }
    }
}
