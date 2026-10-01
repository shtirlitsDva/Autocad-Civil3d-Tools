using System.Text.Json;

using GDALService.Capabilities;
using GDALService.Common;
using GDALService.Terrain;
using GDALService.Terrain.GdalBackend;
using GDALService.Tests.Fakes;

using OSGeo.GDAL;
using OSGeo.OSR;

namespace GDALService.Tests;

// CLIP_RASTER through Handle: its payload rules word for word, and the request
// the clipper gets.
public class ClipRasterPayloadTests
{
    // The members of a valid payload (Line adds the braces).
    private const string Valid =
        "\"sources\":[\"C:\\\\t\\\\a.tif\",\"C:\\\\t\\\\b.tif\"],\"bounds\":{\"minX\":1,\"minY\":2,\"maxX\":3,\"maxY\":4}," +
        "\"targetWkt\":\"WKT\",\"resolution\":0.5,\"outPath\":\"C:\\\\o\\\\x.tif\"";

    private static (ClipRaster Capability, FakeRasterClipper Clipper) Capability()
    {
        var clipper = new FakeRasterClipper(r => new Ok<ClippedRaster>(new ClippedRaster(r.OutPath, 4, 4)));
        return (new ClipRaster(clipper), clipper);
    }

    private static string Line(string payload) => """{"id":"c","type":"CLIP_RASTER","payload":{""" + payload + "}}";

    [Fact]
    public void A_valid_request_reaches_the_clipper_and_the_reply_names_the_file()
    {
        var (capability, clipper) = Capability();

        var result = CapabilityTesting.Result(capability.Handle(CapabilityTesting.Request(Line(Valid))));

        var asked = Assert.Single(clipper.Requests);
        Assert.Equal(["C:\\t\\a.tif", "C:\\t\\b.tif"], asked.Sources);
        Assert.Equal((1.0, 2.0, 3.0, 4.0, "WKT", 0.5, "C:\\o\\x.tif"),
                     (asked.MinX, asked.MinY, asked.MaxX, asked.MaxY, asked.TargetWkt, asked.Resolution, asked.OutPath));
        Assert.Equal("C:\\o\\x.tif", result.GetProperty("outPath").GetString());
        Assert.Equal((4, 4), (result.GetProperty("width").GetInt32(), result.GetProperty("height").GetInt32()));
    }

    [Theory]
    [InlineData("\"sources\":[]", "'sources' is empty")]
    [InlineData("\"sources\":\"a.tif\"", "'sources' must be an array")]
    [InlineData("\"sources\":[\"a.tif\",7]", "'sources[1]' must be a string")]
    [InlineData("\"sources\":[\" \"]", "'sources[0]' is empty")]
    [InlineData("\"bounds\":{\"minX\":3,\"minY\":2,\"maxX\":3,\"maxY\":4}", "'bounds' must have minX < maxX and minY < maxY")]
    [InlineData("\"bounds\":{\"minX\":1,\"minY\":2,\"maxX\":3}", "'bounds.maxY' is missing")]
    [InlineData("\"bounds\":[1,2,3,4]", "'bounds' must be an object")]
    [InlineData("\"resolution\":0", "'resolution' must be greater than 0")]
    [InlineData("\"targetWkt\":\"  \"", "'targetWkt' is empty")]
    [InlineData("\"outPath\":5", "'outPath' must be a string")]
    public void A_bad_field_is_refused_by_name_and_the_clipper_is_not_asked(string replace, string message)
    {
        var (capability, clipper) = Capability();

        var fault = Expect.Fault(capability.Handle(CapabilityTesting.Request(Line(Replaced(replace)))));

        Assert.Equal((FaultKind.InvalidArgs, message), (fault.Kind, fault.Message));
        Assert.Empty(clipper.Requests);
    }

    // The valid payload's members with one of them replaced by `replace` ("name":value).
    private static string Replaced(string replace)
    {
        var valid = JsonElement.Parse("{" + Valid + "}");
        var replacement = JsonElement.Parse("{" + replace + "}").EnumerateObject().Single();
        var name = replacement.Name;
        var replaced = replacement.Value;
        var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            foreach (var property in valid.EnumerateObject())
            {
                json.WritePropertyName(property.Name);
                (property.Name == name ? replaced : property.Value).WriteTo(json);
            }
            json.WriteEndObject();
        }
        string text = System.Text.Encoding.UTF8.GetString(buffer.ToArray());
        return text[1..^1]; // Line() adds the braces

    }

    [Fact]
    public void A_clipper_fault_is_the_reply()
    {
        var capability = new ClipRaster(new FakeRasterClipper(_ => new Fault(FaultKind.Gdal, "no tiles")));
        var fault = Expect.Fault(capability.Handle(CapabilityTesting.Request(Line(Valid))));
        Assert.Equal((FaultKind.Gdal, "no tiles"), (fault.Kind, fault.Message));
    }
}

// The GDAL clipper against real GeoTIFFs in EPSG:25832: two 10 × 10 m tiles at
// 1 m side by side, each pixel holding x + y / 100 of its centre.
public sealed class GdalClipperTests : IDisposable
{
    private const double Left = 566000, Top = 5934010;

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "gdalservice-tests", Guid.NewGuid().ToString("N"));
    private readonly string[] _tiles;

    public GdalClipperTests()
    {
        GdalForTests.Ensure();
        Directory.CreateDirectory(_dir);
        _tiles = [Tile("a.tif", Left), Tile("b.tif", Left + 10)];
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); }
        catch (IOException) { /* a GDAL handle may still be closing; the temp folder is disposable */ }
    }

    private static string Wkt(int epsg)
    {
        using var srs = new SpatialReference("");
        srs.ImportFromEPSG(epsg);
        srs.ExportToWkt(out string wkt, null);
        return wkt;
    }

    private static float Value(double x, double y) => (float)(x - Left + (y - (Top - 10)) / 100);

    private string Tile(string name, double left)
    {
        string path = Path.Combine(_dir, name);
        TileFolder.WriteTile(path, left, Top, 10, 10, -9999f, (c, r) => Value(left + c + 0.5, Top - r - 0.5));
        using var ds = Gdal.Open(path, Access.GA_Update);
        ds.SetProjection(Wkt(25832));
        return path;
    }

    private static (int Width, int Height, double[] GeoTransform, float[] Values, double NoData) Read(string path)
    {
        using var ds = Gdal.Open(path, Access.GA_ReadOnly);
        var gt = new double[6];
        ds.GetGeoTransform(gt);
        using var band = ds.GetRasterBand(1);
        band.GetNoDataValue(out double noData, out _);
        var values = new float[ds.RasterXSize * ds.RasterYSize];
        band.ReadRaster(0, 0, ds.RasterXSize, ds.RasterYSize, values, ds.RasterXSize, ds.RasterYSize, 0, 0);
        return (ds.RasterXSize, ds.RasterYSize, gt, values, noData);
    }

    [Fact]
    public void A_box_across_both_tiles_in_their_own_crs_keeps_the_values()
    {
        string outPath = Path.Combine(_dir, "out.tif");
        var clipped = Expect.Ok(new GdalClipper().Clip(new ClipRequest(_tiles, Left + 5, Top - 8, Left + 15, Top - 2, Wkt(25832), 1, outPath)));

        Assert.Equal((outPath, 10, 6), (clipped.OutPath, clipped.Width, clipped.Height));
        var (width, height, gt, values, noData) = Read(outPath);
        Assert.Equal((10, 6), (width, height));
        Assert.Equal([Left + 5, 1, 0, Top - 2, 0, -1], gt);
        Assert.Equal(-9999, noData);
        for (int r = 0; r < height; r++)
        {
            for (int c = 0; c < width; c++)
            {
                Assert.Equal(Value(Left + 5 + c + 0.5, Top - 2 - r - 0.5), values[r * width + c], 3);
            }
        }
    }

    [Fact]
    public void Outside_the_tiles_is_nodata()
    {
        string outPath = Path.Combine(_dir, "edge.tif");
        Expect.Ok(new GdalClipper().Clip(new ClipRequest(_tiles, Left + 15, Top - 5, Left + 25, Top, Wkt(25832), 1, outPath)));

        var (width, _, _, values, _) = Read(outPath);
        Assert.Equal(Value(Left + 15.5, Top - 0.5), values[0], 3);
        Assert.Equal(-9999, values[width - 1]);
    }

    [Fact]
    public void Another_target_crs_is_warped_into()
    {
        // The same ground in UTM zone 33: the tiles lie near x 165000 there.
        string outPath = Path.Combine(_dir, "utm33.tif");
        using var utm32 = new SpatialReference(Wkt(25832));
        using var utm33 = new SpatialReference(Wkt(25833));
        utm32.SetAxisMappingStrategy(AxisMappingStrategy.OAMS_TRADITIONAL_GIS_ORDER);
        utm33.SetAxisMappingStrategy(AxisMappingStrategy.OAMS_TRADITIONAL_GIS_ORDER);
        using var transform = new CoordinateTransformation(utm32, utm33);
        var centre = new double[3];
        transform.TransformPoint(centre, Left + 10, Top - 5, 0);

        var clipped = Expect.Ok(new GdalClipper().Clip(new ClipRequest(_tiles, centre[0] - 2, centre[1] - 2, centre[0] + 2, centre[1] + 2,
                                                                       Wkt(25833), 0.5, outPath)));

        Assert.Equal((8, 8), (clipped.Width, clipped.Height));
        var (_, _, _, values, _) = Read(outPath);
        Assert.All(values, v => Assert.InRange(v, 7f, 13f)); // around x - Left = 10, all on data
    }

    [Fact]
    public void A_missing_source_is_a_gdal_fault()
    {
        var fault = Expect.Fault(new GdalClipper().Clip(new ClipRequest([Path.Combine(_dir, "none.tif")], 0, 0, 1, 1, Wkt(25832), 1,
                                                                        Path.Combine(_dir, "x.tif"))));
        Assert.Equal(FaultKind.Gdal, fault.Kind);
    }
}
