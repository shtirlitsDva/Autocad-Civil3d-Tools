using GDALService.Common;
using GDALService.Infrastructure.IO;

using OSGeo.GDAL;

using static OSGeo.GDAL.GdalConst;

namespace GDALService.Infrastructure.Gdal
{
    internal sealed class VrtManager
    {
        internal readonly record struct OpenResult(
        string ProjectId, string BasePath, string ElevationsDir, string VrtPath,
        int RasterXSize, int RasterYSize, int RasterCount, string Projection);

        private readonly object _lock = new();

        private Dataset? _ds;
        internal string? CurrentProjectId { get; private set; }
        internal string? CurrentBasePath { get; private set; }
        internal string? ElevationsDir { get; private set; }
        internal string? VrtPath { get; private set; }
        internal int Width { get; private set; }
        internal int Height { get; private set; }
        internal int Bands { get; private set; }
        internal string Projection { get; private set; } = "";

        /// <summary>Called by SET_PROJECT every time. Reuses DS if same project/path.</summary>
        public Result<OpenResult> SetProject(string projectId, string basePath)
        {
            if (string.IsNullOrWhiteSpace(projectId) || string.IsNullOrWhiteSpace(basePath))
                return Result<OpenResult>.Fail(StatusCode.InvalidArgs, "projectId/basePath empty.");

            var requestedBase = Path.GetFullPath(basePath);
            var elevDir = PathHelpers.ElevationsDir(requestedBase);
            if (!Directory.Exists(elevDir))
                return Result<OpenResult>.Fail(StatusCode.NotFound, $"Elevations folder not found: {elevDir}");

            var vrt = PathHelpers.VrtPath(elevDir, projectId);
            var tiles = PathHelpers.FindTiles(elevDir, projectId);
            if (tiles.Length == 0)
                return Result<OpenResult>.Fail(StatusCode.NotFound, $"No GeoTIFF tiles for '{projectId}' in {elevDir}");

            // Build VRT if missing
            if (!File.Exists(vrt))
            {
                GDALService.Hosting.StdIo.WriteErr($"INFO building VRT: {vrt}");
                using var opts = new GDALBuildVRTOptions(new[] { "-resolution", "highest" });
                using var tmp = OSGeo.GDAL.Gdal.wrapper_GDALBuildVRT_names(vrt, tiles, opts, null, null);
                if (tmp is null)
                    return Result<OpenResult>.Fail(StatusCode.Error, "GDALBuildVRT failed.");
            }

            lock (_lock)
            {
                // Reuse if same project/path and DS still valid
                if (_ds is not null &&
                    string.Equals(CurrentProjectId, projectId, StringComparison.OrdinalIgnoreCase) &&
                    File.Exists(vrt))
                {
                    return Result<OpenResult>.Success(ToResult());
                }

                // Switch project: dispose old, open new thread-safe dataset
                try { _ds?.Dispose(); } catch { /* ignore */ }
                _ds = null;

                uint flags = (uint)(OF_RASTER | OF_THREAD_SAFE);
                var ds = OSGeo.GDAL.Gdal.OpenEx(vrt, flags, null, null, null);
                if (ds is null)
                    return Result<OpenResult>.Fail(StatusCode.Error, "OpenEx failed for VRT.");
                if (!ds.IsThreadSafe((int)OF_RASTER))
                {
                    ds.Dispose();
                    return Result<OpenResult>.Fail(StatusCode.Error, "Dataset not thread-safe (OF_THREAD_SAFE unavailable for this source).");
                }

                // Cache metadata & state
                _ds = ds;
                CurrentProjectId = projectId;
                CurrentBasePath = requestedBase;
                ElevationsDir = elevDir;
                VrtPath = vrt;
                Width = ds.RasterXSize;
                Height = ds.RasterYSize;
                Bands = ds.RasterCount;
                Projection = ds.GetProjectionRef() ?? "";

                return Result<OpenResult>.Success(ToResult());
            }

            OpenResult ToResult() => new(
                CurrentProjectId!, CurrentBasePath!, ElevationsDir!, VrtPath!,
                Width, Height, Bands, Projection);
        }

        /// <summary>Used by SAMPLE_POINTS. Returns the live thread-safe Dataset and VRT path.</summary>
        public Result<(Dataset Ds, string VrtPath)> TryGetDataset()
        {
            lock (_lock)
            {
                if (_ds is null || string.IsNullOrEmpty(VrtPath) || !File.Exists(VrtPath))
                    return Result<(Dataset, string)>.Fail(StatusCode.NotInitialized, "No project initialized. Call SET_PROJECT first always!");
                return Result<(Dataset, string)>.Success((_ds, VrtPath!));
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                try { _ds?.Dispose(); } catch { /* ignore */ }
                _ds = null;
            }
        }
    }
}
