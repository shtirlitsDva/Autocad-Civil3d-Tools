using GDALService.Common;

namespace GDALService.Terrain.GdalBackend;

// gdal_contour: the raster's band 1, its NoData left out, traced every
// interval into a GeoJSON file of 3D lines.
internal sealed class GdalContourer : IContourer
{
    public Result<ContourLines> Contour(ContourRequest request) =>
        GdalEdge.Contour(request.Source, request.OutPath, request.Interval);
}
