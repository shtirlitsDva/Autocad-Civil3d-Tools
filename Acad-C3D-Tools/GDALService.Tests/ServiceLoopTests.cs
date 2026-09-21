using System.Text.Json;

using GDALService.Common;

namespace GDALService.Tests;

// The whole protocol, driven in-process through the same loop the executable
// runs, with the request lines as input and the reply lines as output.
public sealed class ServiceLoopTests : IDisposable
{
    private readonly TileFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    // Request lines built by the serializer: correct escaping of Windows paths
    // and invariant numbers without hand-written JSON.
    private static string Line(string id, string type, object payload) =>
        JsonSerializer.Serialize(new { id, type, payload });

    private static string SetProjectLine(string basePath) =>
        Line("p", "SET_PROJECT", new { projectId = "TST", basePath });

    private string SetProject() => SetProjectLine(_folder.BasePath);

    private static object Point(int seq, double x, double y) => new { geomId = 1, seq, s = 0, x, y };

    private static List<JsonElement> Run(params string[] lines) => Run(GdalForTests.Loaded, lines);

    private static List<JsonElement> Run(Result<string> gdal, params string[] lines) =>
        ServiceHarness.Replies(ServiceHarness.Run(gdal, _ => { }, lines).Stdout);

    private static int Status(JsonElement reply) => ServiceHarness.Status(reply);

    [Fact]
    public void A_scripted_session_answers_every_request_in_order()
    {
        var (x, y) = TileFolder.CentreOf(2, 1);
        var replies = Run(
            """{"id":"h","type":"HELLO","payload":{}}""",
            SetProject(),
            Line("s", "SAMPLE_POINTS", new { threads = 0, points = new[] { Point(0, x, y), Point(1, 0, 0) } }),
            """{"id":"g","type":"SAMPLE_GRID","payload":{"gridDist":5}}""",
            """{"id":"z","type":"SHUTDOWN"}""");

        Assert.Equal(["h", "p", "s", "g", "z"], replies.Select(r => r.GetProperty("id").GetString()));
        Assert.All(replies, r => Assert.Equal(0, Status(r)));

        var rows = replies[2].GetProperty("result").GetProperty("rows").EnumerateArray().ToList();
        Assert.Equal(TileFolder.Expected(2, 1), rows[0].GetProperty("elev").GetDouble(), 4);
        Assert.Equal("OUTSIDE", rows[1].GetProperty("status").GetString());
        Assert.Equal("BYE", replies[4].GetProperty("result").GetProperty("msg").GetString());
    }

    [Fact]
    public void Issue_175_a_point_off_the_raster_is_answered_and_the_service_stays_up()
    {
        var replies = Run(
            SetProject(),
            """{"id":"far","type":"SAMPLE_POINTS","payload":{"points":[{"geomId":1,"seq":0,"s":0,"x":700000,"y":6000000}]}}""",
            """{"id":"next","type":"HELLO"}""");

        Assert.Equal(0, Status(replies[1]));
        var row = replies[1].GetProperty("result").GetProperty("rows")[0];
        Assert.Equal("OUTSIDE", row.GetProperty("status").GetString());
        Assert.False(row.TryGetProperty("elev", out _));
        Assert.Equal("next", replies[2].GetProperty("id").GetString());
    }

    [Fact]
    public void Bad_lines_between_good_ones_never_stop_the_loop()
    {
        var replies = Run(
            "null",
            "{ not json",
            """{"id":"a","type":"SAMPLE_POINTS"}""",
            """{"id":"b","type":"SAMPLE_POINTS","payload":{"points":[]}}""",
            """{"id":"c","type":"HELLO"}""");

        Assert.Equal(5, replies.Count);
        Assert.False(replies[0].TryGetProperty("id", out _));
        Assert.False(replies[1].TryGetProperty("id", out _));
        Assert.Equal((2, 2), (Status(replies[0]), Status(replies[1])));
        Assert.Equal(("a", 2), (replies[2].GetProperty("id").GetString(), Status(replies[2])));
        Assert.Equal(("b", 4), (replies[3].GetProperty("id").GetString(), Status(replies[3])));   // no project yet
        Assert.Equal(0, Status(replies[4]));
    }

    [Fact]
    public void Shutdown_ends_the_loop()
    {
        var replies = Run("""{"id":"z","type":"SHUTDOWN"}""", """{"id":"never","type":"HELLO"}""");
        Assert.Single(replies);
    }

    [Fact]
    public void A_base_path_with_danish_letters_arrives_intact()
    {
        using var danish = new TileFolder("Grønnegård æøå");
        var replies = Run(SetProjectLine(danish.BasePath));

        Assert.Equal(0, Status(replies[0]));
        Assert.Equal(danish.ElevationsDir, replies[0].GetProperty("result").GetProperty("elevationsDir").GetString());
    }

    [Fact]
    public void Without_gdal_the_service_refuses_gdal_work_but_still_answers()
    {
        var noGdal = new Fault(FaultKind.Gdal, "GDAL native libraries are not usable");
        var replies = Run(noGdal, """{"id":"h","type":"HELLO"}""", SetProject());

        Assert.Equal(0, Status(replies[0]));
        Assert.Equal(1, Status(replies[1]));
        Assert.Equal("GDAL native libraries are not usable", replies[1].GetProperty("error").GetString());
    }

    // Spec decision D6: the cause, not a symptom - a folder that is missing too
    // would not open a project without GDAL either.
    [Fact]
    public void Without_gdal_set_project_says_so_even_when_the_folder_is_missing()
    {
        var noGdal = new Fault(FaultKind.Gdal, "GDAL native libraries are not usable");
        var replies = Run(noGdal, SetProjectLine(@"C:\definitely\not\here"));

        Assert.Equal((1, "GDAL native libraries are not usable"), (Status(replies[0]), replies[0].GetProperty("error").GetString()));
    }

    // Spec decision D6: without GDAL no project can open, so a sample request is
    // told there is no project (4) rather than, as before, that GDAL is missing (1).
    [Fact]
    public void Without_gdal_a_sample_request_is_told_there_is_no_project()
    {
        var noGdal = new Fault(FaultKind.Gdal, "GDAL native libraries are not usable");
        var replies = Run(noGdal, """{"id":"s","type":"SAMPLE_GRID","payload":{"gridDist":1}}""");
        Assert.Equal(4, Status(replies[0]));
    }

    [Fact]
    public void The_ready_line_names_the_gdal_release_or_why_there_is_none()
    {
        Assert.StartsWith("READY gdal=", ServiceHarness.Run().Stderr);
        var noGdal = new Fault(FaultKind.Gdal, "not usable");
        Assert.StartsWith("READY without GDAL: not usable", ServiceHarness.Run(noGdal, _ => { }).Stderr);
    }
}
