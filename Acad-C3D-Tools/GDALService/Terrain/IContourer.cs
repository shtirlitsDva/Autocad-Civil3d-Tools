using GDALService.Common;

namespace GDALService.Terrain;

// Traces contour lines through a height raster and writes them as GeoJSON:
// 3D lines (z is the height) with the height also in the field "elev", in the
// raster's own CRS. Levels are every `interval` units, counted from 0.
internal interface IContourer
{
    Result<ContourLines> Contour(ContourRequest request);
}

internal sealed record ContourRequest(string Source, double Interval, string OutPath);

internal sealed record ContourLines(string OutPath, long Count);
