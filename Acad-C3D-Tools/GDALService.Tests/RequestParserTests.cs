using GDALService.Common;
using GDALService.Protocol;

namespace GDALService.Tests;

public class RequestParserTests
{
    private static Request ParsedOk(string line)
    {
        var (to, request) = RequestParser.Parse(line);
        Assert.IsType<RequestId>(to.Value);
        return Expect.Ok(request);
    }

    private static (ReplyTo To, Fault Fault) ParsedFault(string line)
    {
        var (to, request) = RequestParser.Parse(line);
        return (to, Expect.Fault(request));
    }

    [Fact]
    public void Every_request_type_parses()
    {
        Assert.IsType<Hello>(ParsedOk("""{"id":"1","type":"HELLO","payload":{}}""").Value);
        Assert.IsType<Hello>(ParsedOk("""{"id":"1","type":"HELLO"}""").Value);
        Assert.IsType<Shutdown>(ParsedOk("""{"id":"1","type":"SHUTDOWN"}""").Value);

        var set = Assert.IsType<SetProject>(ParsedOk(
            """{"id":"2","type":"SET_PROJECT","payload":{"projectId":"K1LJKYA7","basePath":"C:\\Temp\\GDALTest"}}""").Value);
        Assert.Equal(("K1LJKYA7", @"C:\Temp\GDALTest"), (set.ProjectId, set.BasePath));

        var grid = Assert.IsType<SampleGrid>(ParsedOk("""{"id":"3","type":"SAMPLE_GRID","payload":{"gridDist":2.5}}""").Value);
        Assert.Equal(2.5, grid.GridDist);
    }

    [Fact]
    public void Sample_points_parse_as_the_client_sends_them_and_threads_is_ignored()
    {
        var points = Assert.IsType<SamplePoints>(ParsedOk(
            """{"id":"4","type":"SAMPLE_POINTS","payload":{"threads":0,"points":[{"geomId":1,"seq":0,"s":0.0,"x":686000.5,"y":6170500.25},{"geomId":1,"seq":1,"s":0,"x":1,"y":2}]}}""").Value);

        Assert.Equal(new RequestId("4"), points.Id);
        Assert.Equal(2, points.Points.Count);
        Assert.Equal((1L, 0, 686000.5, 6170500.25), (points.Points[0].GeomId, points.Points[0].Seq, points.Points[0].X, points.Points[0].Y));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("not json at all")]
    [InlineData("[1,2]")]
    [InlineData("{}")]
    [InlineData("""{"id":42,"type":"HELLO"}""")]
    [InlineData("""{"id":null,"type":"HELLO"}""")]
    public void A_line_without_a_readable_id_is_answered_unaddressed(string line)
    {
        var (to, fault) = ParsedFault(line);
        Assert.IsType<Unaddressed>(to.Value);
        Assert.Equal(FaultKind.InvalidArgs, fault.Kind);
    }

    [Theory]
    [InlineData("""{"id":"a","type":"NOPE"}""", "Unknown type 'NOPE'")]
    [InlineData("""{"id":"a"}""", "'type' is missing")]
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
    public void An_invalid_request_keeps_its_id_and_names_what_is_wrong(string line, string message)
    {
        var (to, fault) = ParsedFault(line);
        Assert.Equal(new RequestId("a"), Assert.IsType<RequestId>(to.Value));
        Assert.Equal(FaultKind.InvalidArgs, fault.Kind);
        Assert.Equal(message, fault.Message);
    }
}
