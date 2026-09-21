using System.Text;
using System.Text.Json;

using GDALService.Common;
using GDALService.Domain;

namespace GDALService.Protocol;

internal sealed record HelloAck
{
    public static readonly HelloAck Instance = new();
    private HelloAck() { }
}

internal sealed record Bye
{
    public static readonly Bye Instance = new();
    private Bye() { }
}

internal sealed record ProjectOpened(string ProjectId, string ElevationsDir, string VrtPath,
                                     int Width, int Height, int Bands, Option<string> Projection);

internal sealed record PointsSampled(IReadOnlyList<SampledPoint> Rows, SampleSummary Summary);

internal sealed record GridSampled(IReadOnlyList<GridSample> Cells, SampleSummary Summary);

internal union ReplyBody(HelloAck, ProjectOpened, PointsSampled, GridSampled, Bye);

// Writes one reply line: {"id","status","result"} on success, {"id","status",
// "error"} on failure. A field with nothing to say is left out rather than
// written as null - an unaddressed reply has no "id", a non-OK row has no
// "elev". Every number written comes from a type that only holds finite values,
// so the writer has no NaN to refuse.
internal static class ReplyWriter
{
    public static string Serialize(ReplyTo to, Result<ReplyBody> reply)
    {
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            WriteId(json, to);
            WriteOutcome(json, reply);
            json.WriteEndObject();
        }
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    // The writers below pick their action with switch expressions rather than
    // switch statements, because only the expression form is checked for
    // exhaustiveness: a new case of any of these unions fails the build here.
    private static void WriteId(Utf8JsonWriter json, ReplyTo to) =>
        to.Id.Switch(id => json.WriteString("id", id.Value), () => { });

    private static void WriteOutcome(Utf8JsonWriter json, Result<ReplyBody> reply) =>
        reply.Switch(
            body =>
            {
                json.WriteNumber("status", 0);
                json.WritePropertyName("result");
                WriteBody(json, body);
            },
            fault =>
            {
                json.WriteNumber("status", StatusOf(fault.Kind));
                json.WriteString("error", fault.Message);
            });

    // The wire's numeric status codes, unchanged from the old protocol.
    public static int StatusOf(FaultKind kind) => kind switch
    {
        FaultKind.Gdal => 1,
        FaultKind.Internal => 1,
        FaultKind.InvalidArgs => 2,
        FaultKind.NotFound => 3,
        FaultKind.NotInitialized => 4,
    };

    private static void WriteBody(Utf8JsonWriter json, ReplyBody body)
    {
        json.WriteStartObject();
        (body switch
        {
            HelloAck => (Action)(() => json.WriteString("msg", "HELLO_ACK")),
            Bye => () => json.WriteString("msg", "BYE"),
            ProjectOpened p => () =>
            {
                json.WriteString("projectId", p.ProjectId);
                json.WriteString("elevationsDir", p.ElevationsDir);
                json.WriteString("vrtPath", p.VrtPath);
                json.WriteNumber("width", p.Width);
                json.WriteNumber("height", p.Height);
                json.WriteNumber("bands", p.Bands);
                WriteProjection(json, p.Projection);
            },
            PointsSampled points => () =>
            {
                WriteSummary(json, points.Summary);
                json.WriteStartArray("rows");
                foreach (var row in points.Rows) { WritePointRow(json, row); }
                json.WriteEndArray();
            },
            GridSampled grid => () =>
            {
                WriteSummary(json, grid.Summary);
                json.WriteStartArray("rows");
                foreach (var cell in grid.Cells) { WriteGridRow(json, cell); }
                json.WriteEndArray();
            },
        })();
        json.WriteEndObject();
    }

    private static void WriteProjection(Utf8JsonWriter json, Option<string> projection) =>
        projection.Switch(wkt => json.WriteString("projection", wkt), () => { });

    private static void WriteHeight(Utf8JsonWriter json, string name, Sample sample) =>
        sample.Height.Switch(metres => json.WriteNumber(name, metres), () => { });

    private static void WriteSummary(Utf8JsonWriter json, SampleSummary sum)
    {
        json.WriteNumber("total", sum.Total);
        json.WriteNumber("ok", sum.OkCount);
        json.WriteNumber("outside", sum.OutsideCount);
        json.WriteNumber("noData", sum.NoDataCount);
        json.WriteNumber("err", sum.ErrCount);
    }

    private static void WritePointRow(Utf8JsonWriter json, SampledPoint row)
    {
        json.WriteStartObject();
        json.WriteNumber("geomId", row.Query.GeomId);
        json.WriteNumber("seq", row.Query.Seq);
        json.WriteNumber("s", row.Query.S);
        json.WriteNumber("x", row.Query.X);
        json.WriteNumber("y", row.Query.Y);
        WriteHeight(json, "elev", row.Sample);
        json.WriteString("status", row.Sample.WireStatus);
        json.WriteEndObject();
    }

    // The grid reply has always listed only cells that are on the raster: ground
    // with a height, and ground marked NoData. Only the former carries "z".
    private static void WriteGridRow(Utf8JsonWriter json, GridSample cell)
    {
        bool listed = cell.Sample switch
        {
            Elevation => true,
            NoData => true,
            Outside => false,
            ReadFailed => false,
        };
        if (!listed) { return; }

        json.WriteStartObject();
        json.WriteNumber("x", cell.X);
        json.WriteNumber("y", cell.Y);
        WriteHeight(json, "z", cell.Sample);
        json.WriteString("status", cell.Sample.WireStatus);
        json.WriteEndObject();
    }
}
