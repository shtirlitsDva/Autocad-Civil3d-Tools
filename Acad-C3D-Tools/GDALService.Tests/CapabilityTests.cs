using System.Text.Json;

using GDALService.Capabilities;
using GDALService.Common;
using GDALService.Project;
using GDALService.Protocol;
using GDALService.Terrain;
using GDALService.Tests.Fakes;

namespace GDALService.Tests;

// Each capability through Handle, the way the loop calls it: its payload rules,
// word for word as the client sees them, and the shape of its reply. The raster
// is a fake laid out like TileFolder's TST mosaic, so every sample outcome can
// be produced on purpose.
internal static class CapabilityTesting
{
    public static Envelope Request(string line) => Expect.Ok(RequestReader.Read(line).Envelope);

    public static JsonElement Result(Result<Reply> reply) =>
        JsonElement.Parse(ReplyWriter.Serialize(new RequestId("t"), reply.Map(r => r.Body))).GetProperty("result");

    // Pixel (2, 1) cannot be read; (3, 3) is the declared NoData; (4, 4) is NaN.
    public static FakeRaster Tst() =>
        new FakeRaster(20, 10, (c, r) => c == 3 && r == 3 ? -9999 : c == 4 && r == 4 ? double.NaN : TileFolder.Expected(c, r),
                       new Some<double>(-9999), TileFolder.OriginX, TileFolder.TopY, "/vsimem/gdalservice/TST-1.vrt")
            .FailAt(2, 1);

    // A store with the TST project already open.
    public static ProjectStore OpenStore(FakeRaster raster)
    {
        var store = new ProjectStore(FakeTileCatalog.Serving(FakeTileCatalog.Tiles(@"C:\P", ("TST_1.tif", 1))),
                                     FakeRasterFactory.Always(raster));
        Expect.Ok(store.Open("TST", @"C:\P"));
        return store;
    }

    public static ProgressFactory Progress(TextWriter stderr) => new(new ServiceLog(stderr), new SamplingOptions());
}

public class RequestReaderTests
{
    [Theory]
    [InlineData("null")]
    [InlineData("not json at all")]
    [InlineData("[1,2]")]
    [InlineData("{}")]
    [InlineData("""{"id":42,"type":"HELLO"}""")]
    [InlineData("""{"id":null,"type":"HELLO"}""")]
    public void A_line_without_a_readable_id_is_answered_unaddressed(string line)
    {
        var (to, envelope) = RequestReader.Read(line);
        Assert.IsType<Unaddressed>(to.Value);
        Assert.Equal(FaultKind.InvalidArgs, Expect.Fault(envelope).Kind);
    }

    [Fact]
    public void A_line_without_a_type_keeps_its_id()
    {
        var (to, envelope) = RequestReader.Read("""{"id":"a"}""");
        Assert.Equal(new RequestId("a"), to.Value);
        Assert.Equal("'type' is missing", Expect.Fault(envelope).Message);
    }

    [Fact]
    public void The_payload_is_carried_as_sent_and_outlives_the_line()
    {
        var envelope = CapabilityTesting.Request("""{"id":"a","type":"X","payload":{"k":[1,2]}}""");
        Assert.Equal(("a", "X"), (envelope.Id.Value, envelope.Type));
        Assert.Equal(2, Expect.Some(envelope.Payload).GetProperty("k").GetArrayLength());
        Assert.IsType<None>(CapabilityTesting.Request("""{"id":"a","type":"X"}""").Payload.Value);
    }
}

public class CapabilityPayloadTests
{
    private static Fault FaultOf(ICapability capability, string line) =>
        Expect.Fault(capability.Handle(CapabilityTesting.Request(line)));

    private static ICapability ByType(string line)
    {
        var store = CapabilityTesting.OpenStore(CapabilityTesting.Tst());
        var sampler = new Sampler(new SamplingOptions());
        var progress = CapabilityTesting.Progress(TextWriter.Null);
        ICapability[] all = [new SetProject(store), new SamplePoints(store, sampler, progress), new SampleGrid(store, sampler, progress)];
        return all.Single(c => c.Type == CapabilityTesting.Request(line).Type);
    }

    [Theory]
    [InlineData("""{"id":"a","type":"SAMPLE_POINTS"}""", "'payload' is missing")]
    [InlineData("""{"id":"a","type":"SAMPLE_POINTS","payload":[]}""", "'payload' must be an object")]
    [InlineData("""{"id":"a","type":"SAMPLE_POINTS","payload":{}}""", "'points' is missing")]
    [InlineData("""{"id":"a","type":"SAMPLE_POINTS","payload":{"points":null}}""", "'points' must be an array")]
    [InlineData("""{"id":"a","type":"SAMPLE_POINTS","payload":{"points":[{"geomId":1,"seq":0,"s":0,"x":"NaN","y":1}]}}""", "'points[0].x' must be a finite number")]
    [InlineData("""{"id":"a","type":"SAMPLE_POINTS","payload":{"points":[{"geomId":1,"seq":0,"s":0,"x":1,"y":1},{"geomId":1,"seq":1,"s":0,"x":1,"y":1e400}]}}""", "'points[1].y' must be a finite number")]
    [InlineData("""{"id":"a","type":"SAMPLE_POINTS","payload":{"points":[{"seq":0,"s":0,"x":1,"y":1}]}}""", "'points[0].geomId' is missing")]
    [InlineData("""{"id":"a","type":"SAMPLE_POINTS","payload":{"points":[{"geomId":1,"seq":1.5,"s":0,"x":1,"y":1}]}}""", "'points[0].seq' must be an integer")]
    [InlineData("""{"id":"a","type":"SAMPLE_GRID","payload":{"gridDist":0}}""", "Grid distance must be > 0 m.")]
    [InlineData("""{"id":"a","type":"SAMPLE_GRID","payload":{"gridDist":-1}}""", "Grid distance must be > 0 m.")]
    [InlineData("""{"id":"a","type":"SAMPLE_GRID","payload":{}}""", "'gridDist' is missing")]
    [InlineData("""{"id":"a","type":"SET_PROJECT","payload":{"projectId":"","basePath":"C:\\x"}}""", "'projectId' must be letters, digits, '_' or '-' (it names files): ''")]
    [InlineData("""{"id":"a","type":"SET_PROJECT","payload":{"projectId":"..\\evil","basePath":"C:\\x"}}""", "'projectId' must be letters, digits, '_' or '-' (it names files): '..\\evil'")]
    [InlineData("""{"id":"a","type":"SET_PROJECT","payload":{"projectId":"A","basePath":"  "}}""", "'basePath' is empty")]
    [InlineData("""{"id":"a","type":"SET_PROJECT","payload":{"projectId":"A"}}""", "'basePath' is missing")]
    public void An_invalid_payload_is_refused_and_names_what_is_wrong(string line, string message)
    {
        var fault = FaultOf(ByType(line), line);
        Assert.Equal(FaultKind.InvalidArgs, fault.Kind);
        Assert.Equal(message, fault.Message);
    }

    [Fact]
    public void Sampling_before_a_project_is_open_is_not_initialized()
    {
        var empty = new ProjectStore(FakeTileCatalog.Serving(FakeTileCatalog.Tiles(@"C:\P", ("TST_1.tif", 1))),
                                     FakeRasterFactory.Always(CapabilityTesting.Tst()));
        var capability = new SampleGrid(empty, new Sampler(new SamplingOptions()), CapabilityTesting.Progress(TextWriter.Null));

        var fault = FaultOf(capability, """{"id":"a","type":"SAMPLE_GRID","payload":{"gridDist":1}}""");

        Assert.Equal(FaultKind.NotInitialized, fault.Kind);
    }
}

public class CapabilityReplyTests
{
    private static readonly Sampler Sampler = new(new SamplingOptions());
    private static readonly string[] ListedStatuses = ["OK", "NODATA"];

    private static string Line(string id, string type, object payload) => JsonSerializer.Serialize(new { id, type, payload });

    [Fact]
    public void Hello_and_shutdown_answer_with_their_message_and_only_shutdown_stops()
    {
        var hello = Expect.Ok(new Hello().Handle(CapabilityTesting.Request("""{"id":"h","type":"HELLO"}""")));
        var bye = Expect.Ok(new Shutdown().Handle(CapabilityTesting.Request("""{"id":"z","type":"SHUTDOWN"}""")));

        Assert.Equal(("HELLO_ACK", AfterReply.Continue),
            (CapabilityTesting.Result(new Ok<Reply>(hello)).GetProperty("msg").GetString(), hello.Then));
        Assert.Equal(("BYE", AfterReply.Stop),
            (CapabilityTesting.Result(new Ok<Reply>(bye)).GetProperty("msg").GetString(), bye.Then));
    }

    [Fact]
    public void Set_project_describes_the_raster_and_leaves_out_a_projection_it_does_not_have()
    {
        var store = new ProjectStore(FakeTileCatalog.Serving(FakeTileCatalog.Tiles(@"C:\P", ("TST_1.tif", 1))),
                                     FakeRasterFactory.Always(CapabilityTesting.Tst()));

        var result = CapabilityTesting.Result(new SetProject(store).Handle(
            CapabilityTesting.Request(Line("p", "SET_PROJECT", new { projectId = "TST", basePath = @"C:\P" }))));

        Assert.Equal("TST", result.GetProperty("projectId").GetString());
        Assert.Equal(@"C:\P\Elevations", result.GetProperty("elevationsDir").GetString());
        Assert.Equal("/vsimem/gdalservice/TST-1.vrt", result.GetProperty("vrtPath").GetString());
        Assert.Equal((20, 10, 1), (result.GetProperty("width").GetInt32(), result.GetProperty("height").GetInt32(),
                                   result.GetProperty("bands").GetInt32()));
        Assert.False(result.TryGetProperty("projection", out _));
    }

    [Fact]
    public void Only_an_elevation_row_carries_elev_and_every_outcome_is_counted()
    {
        using var store = CapabilityTesting.OpenStore(CapabilityTesting.Tst());
        var capability = new SamplePoints(store, Sampler, CapabilityTesting.Progress(TextWriter.Null));
        (double X, double Y)[] at = [TileFolder.CentreOf(5, 5), TileFolder.CentreOf(3, 3), (0, 0), TileFolder.CentreOf(2, 1)];
        var points = at.Select((p, i) => new { geomId = 9, seq = i, s = i * 1.5, x = p.X, y = p.Y }).ToArray();

        var result = CapabilityTesting.Result(capability.Handle(CapabilityTesting.Request(Line("s", "SAMPLE_POINTS", new { points }))));

        Assert.Equal((4, 1, 1, 1, 1), (result.GetProperty("total").GetInt32(), result.GetProperty("ok").GetInt32(),
            result.GetProperty("outside").GetInt32(), result.GetProperty("noData").GetInt32(), result.GetProperty("err").GetInt32()));
        var rows = result.GetProperty("rows").EnumerateArray().ToList();
        Assert.Equal(["OK", "NODATA", "OUTSIDE", "ERR"], rows.Select(r => r.GetProperty("status").GetString()));
        Assert.Equal(TileFolder.Expected(5, 5), rows[0].GetProperty("elev").GetDouble());
        Assert.All(rows.Skip(1), r => Assert.False(r.TryGetProperty("elev", out _)));
        Assert.Equal((9L, 3, 4.5), (rows[3].GetProperty("geomId").GetInt64(), rows[3].GetProperty("seq").GetInt32(),
                                    rows[3].GetProperty("s").GetDouble()));
    }

    [Fact]
    public void A_grid_lists_only_cells_on_the_raster_and_only_elevations_carry_z()
    {
        // At 1 m the grid lines fall on pixel corners: cell (3, 3) of the grid is
        // the NoData pixel's corner, and the x = 1020 / y = 2010 lines are outside.
        using var store = CapabilityTesting.OpenStore(CapabilityTesting.Tst());
        var capability = new SampleGrid(store, Sampler, CapabilityTesting.Progress(TextWriter.Null));

        var result = CapabilityTesting.Result(capability.Handle(
            CapabilityTesting.Request("""{"id":"g","type":"SAMPLE_GRID","payload":{"gridDist":1}}""")));

        var rows = result.GetProperty("rows").EnumerateArray().ToList();
        int total = result.GetProperty("total").GetInt32();
        int listed = result.GetProperty("ok").GetInt32() + result.GetProperty("noData").GetInt32();
        Assert.Equal(21 * 11, total);
        Assert.Equal(listed, rows.Count);
        Assert.True(result.GetProperty("outside").GetInt32() > 0);
        Assert.All(rows, r => Assert.Contains(r.GetProperty("status").GetString(), ListedStatuses));
        Assert.All(rows, r => Assert.Equal(r.GetProperty("status").GetString() == "OK", r.TryGetProperty("z", out _)));
    }

    [Fact]
    public void Sampling_reports_progress_on_stderr_under_the_requests_id()
    {
        using var store = CapabilityTesting.OpenStore(CapabilityTesting.Tst());
        var stderr = new StringWriter();
        var capability = new SampleGrid(store, Sampler, CapabilityTesting.Progress(stderr));

        Expect.Ok(capability.Handle(CapabilityTesting.Request("""{"id":"g7","type":"SAMPLE_GRID","payload":{"gridDist":0.25}}""")));

        // 81 x 41 = 3321 cells, reported every 500.
        var lines = stderr.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(6, lines.Length);
        var first = JsonElement.Parse(lines[0]);
        Assert.Equal(("g7", "PROGRESS", 3321), (first.GetProperty("id").GetString(), first.GetProperty("type").GetString(),
                                                first.GetProperty("total").GetInt32()));
    }
}

public class ReplyWriterTests
{
    private sealed class Body : IReplyBody
    {
        public void WriteTo(Utf8JsonWriter json) => json.WriteString("msg", "hi");
    }

    private static JsonElement Write(ReplyTo to, Result<IReplyBody> reply) =>
        JsonElement.Parse(ReplyWriter.Serialize(to, reply));

    [Fact]
    public void A_success_has_a_result_object_and_no_error()
    {
        var json = Write(new RequestId("7"), new Ok<IReplyBody>(new Body()));
        Assert.Equal(("7", 0, "hi"), (json.GetProperty("id").GetString(), json.GetProperty("status").GetInt32(),
                                      json.GetProperty("result").GetProperty("msg").GetString()));
        Assert.False(json.TryGetProperty("error", out _));
    }

    [Fact]
    public void A_failure_has_an_error_and_no_result()
    {
        var json = Write(new RequestId("7"), new Fault(FaultKind.NotInitialized, "no project"));
        Assert.Equal((4, "no project"), (json.GetProperty("status").GetInt32(), json.GetProperty("error").GetString()));
        Assert.False(json.TryGetProperty("result", out _));
    }

    [Fact]
    public void An_unaddressed_reply_has_no_id() =>
        Assert.False(Write(Unaddressed.Instance, new Fault(FaultKind.InvalidArgs, "Bad JSON")).TryGetProperty("id", out _));

    [Theory]
    [InlineData(nameof(FaultKind.Gdal), 1)]
    [InlineData(nameof(FaultKind.Internal), 1)]
    [InlineData(nameof(FaultKind.InvalidArgs), 2)]
    [InlineData(nameof(FaultKind.NotFound), 3)]
    [InlineData(nameof(FaultKind.NotInitialized), 4)]
    public void Fault_kinds_keep_the_old_wire_status_codes(string kind, int status) =>
        Assert.Equal(status, ReplyWriter.StatusOf(Enum.Parse<FaultKind>(kind)));
}
