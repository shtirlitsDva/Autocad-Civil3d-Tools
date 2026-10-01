using System.Text.Json;

using GDALService.Capabilities;
using GDALService.Common;
using GDALService.Terrain;
using GDALService.Terrain.GdalBackend;
using GDALService.Tests.Fakes;

using OSGeo.GDAL;
using OSGeo.OSR;

namespace GDALService.Tests;

// CONTOUR through Handle: its payload rules word for word, and the request the
// contourer gets.
public class ContourPayloadTests
{
    private const string Valid = "\"source\":\"C:\\\\t\\\\cut.tif\",\"interval\":0.5,\"outPath\":\"C:\\\\o\\\\c.geojson\"";

    private static (Contour Capability, FakeContourer Contourer) Capability()
    {
        var contourer = new FakeContourer(r => new Ok<ContourLines>(new ContourLines(r.OutPath, 12)));
        return (new Contour(contourer), contourer);
    }

    private static string Line(string payload) => """{"id":"c","type":"CONTOUR","payload":{""" + payload + "}}";

    [Fact]
    public void A_valid_request_reaches_the_contourer_and_the_reply_names_the_file()
    {
        var (capability, contourer) = Capability();

        var result = CapabilityTesting.Result(capability.Handle(CapabilityTesting.Request(Line(Valid))));

        var asked = Assert.Single(contourer.Requests);
        Assert.Equal(("C:\\t\\cut.tif", 0.5, "C:\\o\\c.geojson"), (asked.Source, asked.Interval, asked.OutPath));
        Assert.Equal("C:\\o\\c.geojson", result.GetProperty("outPath").GetString());
        Assert.Equal(12, result.GetProperty("count").GetInt64());
    }

    [Theory]
    [InlineData("\"source\":\"C:\\\\t\\\\cut.tif\",\"outPath\":\"o.geojson\"", "'interval' is missing")]
    [InlineData("\"source\":\" \",\"interval\":1,\"outPath\":\"o.geojson\"", "'source' is empty")]
    [InlineData("\"source\":\"a.tif\",\"interval\":0,\"outPath\":\"o.geojson\"", "'interval' must be greater than 0")]
    [InlineData("\"source\":\"a.tif\",\"interval\":1,\"outPath\":\"\"", "'outPath' is empty")]
    [InlineData("\"source\":7,\"interval\":1,\"outPath\":\"o.geojson\"", "'source' must be a string")]
    public void A_bad_field_is_refused_by_name_and_the_contourer_is_not_asked(string payload, string message)
    {
        var (capability, contourer) = Capability();

        var fault = Expect.Fault(capability.Handle(CapabilityTesting.Request(Line(payload))));

        Assert.Equal((FaultKind.InvalidArgs, message), (fault.Kind, fault.Message));
        Assert.Empty(contourer.Requests);
    }
}

// The GDAL contourer on a real GeoTIFF in EPSG:25832: 10 × 10 m at 1 m, the
// height rising 1 m per metre eastwards, with a NoData strip on the north edge.
public sealed class GdalContourerTests : IDisposable
{
    private const double Left = 566000, Top = 5934010;

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "gdalservice-tests", Guid.NewGuid().ToString("N"));
    private readonly string _tile;

    public GdalContourerTests()
    {
        GdalForTests.Ensure();
        Directory.CreateDirectory(_dir);
        _tile = Path.Combine(_dir, "slope.tif");
        TileFolder.WriteTile(_tile, Left, Top, 10, 10, -9999f, (c, r) => r == 0 ? -9999f : c + 0.5f);
        using var ds = Gdal.Open(_tile, Access.GA_Update);
        using var srs = new SpatialReference("");
        srs.ImportFromEPSG(25832);
        srs.ExportToWkt(out string wkt, null);
        ds.SetProjection(wkt);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); }
        catch (IOException) { /* a GDAL handle may still be closing; the temp folder is disposable */ }
    }

    [Fact]
    public void Lines_are_3d_at_every_level_and_skip_nodata()
    {
        string outPath = Path.Combine(_dir, "c.geojson");

        var lines = Expect.Ok(new GdalContourer().Contour(new ContourRequest(_tile, 2, outPath)));

        Assert.Equal(outPath, lines.OutPath);
        using var json = JsonDocument.Parse(File.ReadAllText(outPath));
        var features = json.RootElement.GetProperty("features").EnumerateArray().ToList();
        Assert.Equal(lines.Count, features.Count);
        var levels = features.Select(f => f.GetProperty("properties").GetProperty("elev").GetDouble()).Distinct().Order().ToList();
        Assert.Equal([2.0, 4.0, 6.0, 8.0], levels);
        foreach (var f in features)
        {
            double elev = f.GetProperty("properties").GetProperty("elev").GetDouble();
            foreach (var p in f.GetProperty("geometry").GetProperty("coordinates").EnumerateArray())
            {
                Assert.Equal(3, p.GetArrayLength());
                Assert.Equal(elev, p[2].GetDouble());
                Assert.InRange(p[0].GetDouble(), Left, Left + 10);
                Assert.InRange(p[1].GetDouble(), Top - 10, Top - 1 + 0.5); // not into the NoData row
            }
        }
    }

    [Fact]
    public void An_existing_file_is_replaced()
    {
        string outPath = Path.Combine(_dir, "again.geojson");
        Expect.Ok(new GdalContourer().Contour(new ContourRequest(_tile, 2, outPath)));

        var second = Expect.Ok(new GdalContourer().Contour(new ContourRequest(_tile, 4, outPath)));

        Assert.Equal(2, second.Count); // levels 4 and 8 only
    }

    [Fact]
    public void A_missing_source_is_a_gdal_fault()
    {
        var fault = Expect.Fault(new GdalContourer().Contour(new ContourRequest(Path.Combine(_dir, "none.tif"), 1, Path.Combine(_dir, "x.geojson"))));
        Assert.Equal(FaultKind.Gdal, fault.Kind);
    }
}
