using System.Text.Json;

using GDALService.Common;
using GDALService.Domain;
using GDALService.Project;
using GDALService.Protocol;
using GDALService.Terrain;

namespace GDALService.Capabilities;

// SAMPLE_POINTS {points: [{geomId, seq, s, x, y}]}: the ground height under each
// point of the open project. A point off the raster is an OUTSIDE row, not a
// failure; rows come back in request order. Fields the service does not use,
// such as the client's `threads`, are ignored.
internal sealed class SamplePoints : ICapability
{
    private readonly ProjectStore _projects;
    private readonly Sampler _sampler;
    private readonly ProgressFactory _progress;

    public SamplePoints(ProjectStore projects, Sampler sampler, ProgressFactory progress)
    {
        _projects = projects;
        _sampler = sampler;
        _progress = progress;
    }

    public string Type => "SAMPLE_POINTS";

    public Result<Reply> Handle(Envelope request) =>
        JsonRead.Payload(request).Bind(ParsePoints).Bind(points =>
        _projects.Current.Bind(open =>
        {
            var rows = _sampler.Points(open.Raster, points, _progress.For(request.Id, points.Count).Advance);
            return Reply.Continue(new Sampled(rows, SampleSummary.Of(rows.Select(r => r.Sample))));
        }));

    private static Result<IReadOnlyList<PointQuery>> ParsePoints(JsonElement payload) =>
        JsonEdge.Property(payload, "points") switch
        {
            Some<JsonElement> points when points.Value.ValueKind == JsonValueKind.Array => ParseEach(points.Value),
            Some<JsonElement> => JsonRead.Invalid("'points' must be an array"),
            None => JsonRead.Invalid("'points' is missing"),
        };

    // The first invalid point fails the request and names itself; the points
    // after it are not parsed.
    private static Result<IReadOnlyList<PointQuery>> ParseEach(JsonElement array) =>
        array.EnumerateArray()
            .Select((element, index) => (element, index))
            .Aggregate(
                (Result<List<PointQuery>>)new Ok<List<PointQuery>>([]),
                (sofar, item) => sofar.Bind(list =>
                    ParsePoint(item.element, $"points[{item.index}]").Map(point =>
                    {
                        list.Add(point);
                        return list;
                    })))
            .Map(list => (IReadOnlyList<PointQuery>)list);

    private static Result<PointQuery> ParsePoint(JsonElement point, string at) =>
        JsonRead.Required(point, "geomId", JsonEdge.Int64, "an integer", at).Bind(geomId =>
        JsonRead.Required(point, "seq", JsonEdge.Int32, "an integer", at).Bind(seq =>
        JsonRead.Required(point, "s", JsonEdge.FiniteDouble, "a finite number", at).Bind(s =>
        JsonRead.Required(point, "x", JsonEdge.FiniteDouble, "a finite number", at).Bind(x =>
        JsonRead.Required(point, "y", JsonEdge.FiniteDouble, "a finite number", at).Map(y =>
            new PointQuery(geomId, seq, s, x, y))))));

    private sealed record Sampled(IReadOnlyList<SampledPoint> Rows, SampleSummary Summary) : IReplyBody
    {
        public void WriteTo(Utf8JsonWriter json)
        {
            WireFormat.Summary(json, Summary);
            json.WriteStartArray("rows");
            foreach (var row in Rows)
            {
                json.WriteStartObject();
                json.WriteNumber("geomId", row.Query.GeomId);
                json.WriteNumber("seq", row.Query.Seq);
                json.WriteNumber("s", row.Query.S);
                json.WriteNumber("x", row.Query.X);
                json.WriteNumber("y", row.Query.Y);
                WireFormat.Height(json, "elev", row.Sample);
                json.WriteString("status", row.Sample.WireStatus);
                json.WriteEndObject();
            }
            json.WriteEndArray();
        }
    }
}
