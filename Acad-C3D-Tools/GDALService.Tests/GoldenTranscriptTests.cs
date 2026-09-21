using System.Text.Json;
using System.Text.RegularExpressions;

using GDALService.Hosting;

namespace GDALService.Tests;

// The wire, pinned. One fixed session - every request type, every kind of bad
// line, every sample outcome - is run through the service, and its stdout and
// stderr are compared with the committed Golden/*.verified.txt. A refactor that
// changes a single byte a client can see fails here.
//
// Only what is not the service's to fix is normalised: the fixture's temporary
// folder and the per-process in-memory VRT name.
public sealed partial class GoldenTranscriptTests : IDisposable
{
    private readonly TileFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    private static string Line(string id, string type, object payload) =>
        JsonSerializer.Serialize(new { id, type, payload });

    private static object Point(int seq, (double X, double Y) at) => new { geomId = 7, seq, s = seq * 0.5, x = at.X, y = at.Y };

    private string[] Transcript() =>
    [
        Line("early", "SAMPLE_POINTS", new { points = new[] { Point(0, TileFolder.CentreOf(2, 1)) } }),
        """{"id":"h","type":"HELLO","payload":{}}""",
        "{ not json",
        "null",
        """{"id":"u","type":"NOPE","payload":{}}""",
        """{"id":"p0","type":"SET_PROJECT"}""",
        Line("p1", "SET_PROJECT", new { projectId = "../TST", basePath = _folder.BasePath }),
        Line("p", "SET_PROJECT", new { projectId = "TST", basePath = _folder.BasePath }),
        Line("pts", "SAMPLE_POINTS", new
        {
            threads = 0,
            points = new[]
            {
                Point(0, TileFolder.CentreOf(2, 1)),
                Point(1, TileFolder.CentreOf(3, 3)),     // declared NoData
                Point(2, TileFolder.CentreOf(4, 4)),     // undeclared NaN
                Point(3, TileFolder.CentreOf(15, 8)),    // second tile
                Point(4, (999.5, 2005)),                 // west
                Point(5, (1020.5, 2005)),                // east
                Point(6, (1010, 2010.5)),                // north
                Point(7, (1010, 1999.5)),                // south
            },
        }),
        """{"id":"bad","type":"SAMPLE_POINTS","payload":{"points":[{"geomId":1,"seq":0,"s":0,"x":"1","y":2}]}}""",
        """{"id":"g0","type":"SAMPLE_GRID","payload":{"gridDist":0}}""",
        """{"id":"g","type":"SAMPLE_GRID","payload":{"gridDist":0.5}}""",
        Line("miss", "SET_PROJECT", new { projectId = "TST", basePath = Path.Combine(_folder.BasePath, "nowhere") }),
        """{"id":"z","type":"SHUTDOWN","payload":{}}""",
        """{"id":"never","type":"HELLO"}""",
    ];

    [Fact]
    public Task The_wire_is_unchanged()
    {
        var (stdout, stderr) = Run(Transcript());
        var text = "== stdout ==\n" + stdout + "== stderr ==\n" + stderr;
        return Verify(Normalise(text)).UseDirectory("Golden");
    }

    // The same wiring the executable uses, with the console swapped for strings.
    private static (string Stdout, string Stderr) Run(string[] lines)
    {
        var output = new StringWriter { NewLine = "\n" };
        var error = new StringWriter { NewLine = "\n" };
        new ServiceLoop(new StringReader(string.Join("\n", lines)), output, error, GdalForTests.Loaded).Run();
        return (output.ToString(), error.ToString());
    }

    private string Normalise(string text) =>
        VrtName().Replace(
            text.Replace(JsonEncodedText.Encode(_folder.BasePath).ToString(), "{base}"),
            "/vsimem/gdalservice/{vrt}.vrt");

    [GeneratedRegex(@"/vsimem/gdalservice/[^""]+\.vrt")]
    private static partial Regex VrtName();
}
