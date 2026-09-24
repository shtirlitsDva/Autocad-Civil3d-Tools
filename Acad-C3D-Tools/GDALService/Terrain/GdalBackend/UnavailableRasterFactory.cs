using GDALService.Common;

namespace GDALService.Terrain.GdalBackend;

// Stands in for GDAL when it could not be loaded: every raster it is asked for
// is refused with the reason GDAL did not load. The service still starts and
// answers what needs no raster.
internal sealed class UnavailableRasterFactory : IRasterFactory
{
    private readonly Fault _why;

    public UnavailableRasterFactory(Fault why) { _why = why; }

    public Option<Fault> Unavailable => new Some<Fault>(_why);

    public Result<IRaster> OpenMosaic(string name, IReadOnlyList<string> tilePaths) => _why;
}
