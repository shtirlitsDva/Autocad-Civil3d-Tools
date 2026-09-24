using GDALService.Common;
using GDALService.Terrain.GdalBackend;

using OSGeo.GDAL;

namespace GDALService.Tests;

// Unwrapping for assertions. A test that expects Ok and gets a Fault fails with
// the Fault's message, which is the whole point of the helper.
internal static class Expect
{
    public static T Ok<T>(Result<T> result) => result switch
    {
        Ok<T> ok => ok.Value,
        Fault fault => throw new Xunit.Sdk.XunitException($"expected Ok, got {fault.Kind}: {fault.Message}"),
    };

    public static Fault Fault<T>(Result<T> result) => result switch
    {
        Ok<T> ok => throw new Xunit.Sdk.XunitException($"expected a Fault, got Ok: {ok.Value}"),
        Common.Fault fault => fault,
    };

    public static T Some<T>(Option<T> option) => option switch
    {
        Some<T> some => some.Value,
        None => throw new Xunit.Sdk.XunitException("expected Some, got None"),
    };
}

internal static class GdalForTests
{
    private static readonly Lazy<string> Release = new(() => Expect.Ok(GdalBootstrap.Initialise()));

    public static void Ensure() => _ = Release.Value;

    // What Program hands the loop when GDAL loaded.
    public static Result<string> Loaded => new Ok<string>(Release.Value);
}

// A real tile folder on disk, written with GDAL, so the tests need nothing from
// any user's machine.
//
// Tile set "TST": two 10 x 10 Float32 tiles of 1 m pixels side by side, together
// covering x 1000..1020, y 2000..2010. Pixel (col, row) of the mosaic holds
// 100 + col + row / 2, except mosaic pixel (3, 3), which holds the declared
// NoData -9999, and mosaic pixel (4, 4), which holds an undeclared NaN.
//
// Tile set "NAN": one 5 x 5 tile at x 3000..3005, y 4000..4005 whose declared
// NoData is NaN; pixel (2, 2) holds NaN, every other pixel 7.
//
// A decoy "TST_old.tif" sits beside them and must never join the mosaic.
internal sealed class TileFolder : IDisposable
{
    public const double OriginX = 1000, TopY = 2010;
    public const int TileSize = 10;

    public string BasePath { get; }
    public string ElevationsDir => Path.Combine(BasePath, "Elevations");

    public TileFolder(string? name = null)
    {
        GdalForTests.Ensure();
        BasePath = Path.Combine(Path.GetTempPath(), "gdalservice-tests", Guid.NewGuid().ToString("N"), name ?? "Base");
        Directory.CreateDirectory(ElevationsDir);

        WriteTstTile(1, 0);
        WriteTstTile(2, 1);
        WriteTile(Path.Combine(ElevationsDir, "NAN_1.tif"), 3000, 4005, 5, 5, float.NaN,
                  (c, r) => c == 2 && r == 2 ? float.NaN : 7f);
        // Same georeference as TST_1, different values: if it were picked up the
        // mosaic would change, and it is not a <BASE>_<digits>.tif name.
        WriteTile(Path.Combine(ElevationsDir, "TST_old.tif"), OriginX, TopY, TileSize, TileSize, -9999f,
                  (_, _) => -1f);
    }

    public static double Expected(int col, int row) => 100 + col + row / 2.0;

    // The plan coordinate at the centre of mosaic pixel (col, row).
    public static (double X, double Y) CentreOf(int col, int row) => (OriginX + col + 0.5, TopY - row - 0.5);

    public void WriteTstTile(int number, int tileColumn, Func<int, int, float>? values = null)
    {
        int colOffset = tileColumn * TileSize;
        WriteTile(Path.Combine(ElevationsDir, $"TST_{number}.tif"), OriginX + colOffset, TopY, TileSize, TileSize, -9999f,
                  values ?? ((c, r) =>
                  {
                      int col = c + colOffset;
                      return (col, r) switch
                      {
                          (3, 3) => -9999f,
                          (4, 4) => float.NaN,
                          _ => (float)Expected(col, r),
                      };
                  }));
    }

    public static void WriteTile(string path, double left, double top, int width, int height, float noData,
                                 Func<int, int, float> value)
    {
        using var driver = Gdal.GetDriverByName("GTiff");
        using var ds = driver.Create(path, width, height, 1, DataType.GDT_Float32, null);
        ds.SetGeoTransform([left, 1, 0, top, 0, -1]);
        using var band = ds.GetRasterBand(1);
        band.SetNoDataValue(noData);
        var buffer = new float[width * height];
        for (int r = 0; r < height; r++)
        {
            for (int c = 0; c < width; c++) { buffer[r * width + c] = value(c, r); }
        }
        band.WriteRaster(0, 0, width, height, buffer, width, height, 0, 0);
        ds.FlushCache();
    }

    public void Dispose()
    {
        try { Directory.Delete(Path.GetDirectoryName(BasePath)!, recursive: true); }
        catch (IOException) { /* a GDAL handle may still be closing; the temp folder is disposable */ }
    }
}
