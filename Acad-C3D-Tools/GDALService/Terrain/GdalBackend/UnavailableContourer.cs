using GDALService.Common;

namespace GDALService.Terrain.GdalBackend;

// Stands in for GDAL when it could not be loaded: every contour request is
// refused with the reason GDAL did not load.
internal sealed class UnavailableContourer : IContourer
{
    private readonly Fault _why;

    public UnavailableContourer(Fault why) { _why = why; }

    public Result<ContourLines> Contour(ContourRequest request) => _why;
}
