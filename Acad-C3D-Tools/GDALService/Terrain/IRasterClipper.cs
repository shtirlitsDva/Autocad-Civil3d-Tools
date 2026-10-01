using GDALService.Common;

namespace GDALService.Terrain;

// Cuts a set of tiles to one box and writes it as a single GeoTIFF: the tiles
// are mosaicked, warped into the target CRS (given as WKT, so a CRS newer than
// the bundled PROJ database still works) and resampled to the resolution.
internal interface IRasterClipper
{
    Result<ClippedRaster> Clip(ClipRequest request);
}

// The box is in the target CRS; resolution is in its units per pixel.
internal sealed record ClipRequest(IReadOnlyList<string> Sources, double MinX, double MinY, double MaxX, double MaxY,
                                   string TargetWkt, double Resolution, string OutPath);

internal sealed record ClippedRaster(string OutPath, int Width, int Height);
