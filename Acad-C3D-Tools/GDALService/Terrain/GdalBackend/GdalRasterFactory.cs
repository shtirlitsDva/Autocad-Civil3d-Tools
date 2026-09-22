using GDALService.Common;

namespace GDALService.Terrain.GdalBackend;

// Builds the mosaic in GDAL's in-memory file system, never in the tile folder:
// the terrain download writes its own <BASE>.vrt there, and several service
// processes may point at the same shared folder. Each build gets a name unique
// within the process (the in-memory file system is process-wide), because a
// thread-safe dataset reopens its file by name per thread.
internal sealed class GdalRasterFactory : IRasterFactory
{
    private static int s_builds;

    private readonly ServiceLog _log;

    public GdalRasterFactory(ServiceLog log) { _log = log; }

    public Option<Fault> Unavailable => None.Instance;

    public Result<IRaster> OpenMosaic(string name, IReadOnlyList<string> tilePaths)
    {
        var vrtPath = $"/vsimem/gdalservice/{name}-{Interlocked.Increment(ref s_builds)}.vrt";
        return GdalEdge.BuildVrt(vrtPath, tilePaths).Bind(built => GdalRaster.Open(built, _log));
    }
}
