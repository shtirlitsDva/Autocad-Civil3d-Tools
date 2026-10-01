using System.Globalization;

using GDALService.Common;

namespace GDALService.Terrain.GdalBackend;

// gdalwarp over an in-memory mosaic of the tiles. Bilinear, because the tiles
// are heights: where the target grid falls on the source grid it gives the
// source values unchanged, and between them it does not invent steps. Pixels
// with no tile under them are -9999, the NoData of the written file.
internal sealed class GdalClipper : IRasterClipper
{
    private const string NoData = "-9999";

    private static int s_clips;

    public Result<ClippedRaster> Clip(ClipRequest request)
    {
        var vrtPath = $"/vsimem/gdalservice/clip-{Interlocked.Increment(ref s_clips)}.vrt";
        return GdalEdge.BuildVrt(vrtPath, request.Sources).Bind(vrt =>
        {
            var warped = GdalEdge.Warp(vrt, request.OutPath, Arguments(request));
            _ = GdalEdge.Unlink(vrt);
            return warped;
        });
    }

    private static string[] Arguments(ClipRequest request) =>
    [
        "-of", "GTiff", "-overwrite",
        "-t_srs", request.TargetWkt,
        "-te", Number(request.MinX), Number(request.MinY), Number(request.MaxX), Number(request.MaxY),
        "-tr", Number(request.Resolution), Number(request.Resolution),
        "-r", "bilinear",
        "-dstnodata", NoData,
        "-co", "COMPRESS=DEFLATE", "-co", "TILED=YES",
    ];

    private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);
}
