using GDALService.Common;

namespace GDALService.Terrain.GdalBackend;

// Stands in for GDAL when it could not be loaded: every clip is refused with
// the reason GDAL did not load.
internal sealed class UnavailableRasterClipper : IRasterClipper
{
    private readonly Fault _why;

    public UnavailableRasterClipper(Fault why) { _why = why; }

    public Result<ClippedRaster> Clip(ClipRequest request) => _why;
}
