using GDALService.Common;
using GDALService.Infrastructure.IO;

using OSGeo.GDAL;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GDALService.Infrastructure.Gdal
{
    internal sealed class VrtManager
    {
        internal readonly struct OpenResult
        {
            public OpenResult(Dataset ds, string vrtPath, string elevationsDir)
            { Dataset = ds; VrtPath = vrtPath; ElevationsDir = elevationsDir; }

            public Dataset Dataset { get; }
            public string VrtPath { get; }
            public string ElevationsDir { get; }
        }

        public Result<OpenResult> EnsureOpen(string projectId, string basePath)
        {
            if (string.IsNullOrWhiteSpace(projectId) || string.IsNullOrWhiteSpace(basePath))
                return Result<OpenResult>.Fail(StatusCode.InvalidArgs, "projectId/basePath empty.");

            string elevDir = PathHelpers.ElevationsDir(basePath);
            if (!Directory.Exists(elevDir))
                return Result<OpenResult>.Fail(StatusCode.NotFound, $"Elevations folder not found: {elevDir}");

            string vrt = PathHelpers.VrtPath(elevDir, projectId);
            var tiles = PathHelpers.FindTiles(elevDir, projectId);
            if (tiles.Length == 0)
                return Result<OpenResult>.Fail(StatusCode.NotFound, $"No GeoTIFF tiles for '{projectId}' in {elevDir}");

            if (!File.Exists(vrt))
            {
                GDALService.Hosting.StdIo.WriteErr($"INFO building VRT: {vrt}");
                using var opts = new GDALBuildVRTOptions(new[] { "-resolution", "highest" });
                var tmp = OSGeo.GDAL.Gdal.wrapper_GDALBuildVRT_names(vrt, tiles, opts, null, null);
                if (tmp == null)
                    return Result<OpenResult>.Fail(StatusCode.Error, "GDALBuildVRT failed.");
                tmp.Dispose();
            }

            var ds = OSGeo.GDAL.Gdal.Open(vrt, Access.GA_ReadOnly);
            if (ds == null)
                return Result<OpenResult>.Fail(StatusCode.Error, "Failed to open VRT.");

            return Result<OpenResult>.Success(new OpenResult(ds, vrt, elevDir));
        }
    }
}
