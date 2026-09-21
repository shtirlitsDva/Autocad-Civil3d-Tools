using System.Text.Json;
using System.Text.RegularExpressions;

using GDALService.Common;
using GDALService.Domain;

namespace GDALService.Protocol;

// One stdin line to one Request. Parsing never throws: a line that is not JSON,
// lacks a field or carries a value of the wrong kind comes back as an
// InvalidArgs Fault. The reply address is separate from the result, because a
// request can be addressable (its id was read) and still be invalid.
//
// Fields the service does not use, such as the client's `threads`, are ignored.
internal static partial class RequestParser
{
    public static (ReplyTo To, Result<Request> Request) Parse(string line) =>
        JsonEdge.Parse(line) switch
        {
            Ok<JsonDocument> document => Addressed(document.Value),
            Fault fault => (Unaddressed.Instance, fault),
        };

    private static (ReplyTo To, Result<Request> Request) Addressed(JsonDocument document)
    {
        using (document)
        {
            var root = document.RootElement;
            return Required(root, "id", JsonEdge.String, "a string") switch
            {
                Ok<string> id => (new RequestId(id.Value), Typed(root, new RequestId(id.Value))),
                Fault fault => (Unaddressed.Instance, fault),
            };
        }
    }

    private static Result<Request> Typed(JsonElement root, RequestId id) =>
        Required(root, "type", JsonEdge.String, "a string").Bind(type => type switch
        {
            "HELLO" => Accept(new Hello(id)),
            "SET_PROJECT" => Payload(root).Bind(p => ParseSetProject(p, id)),
            "SAMPLE_POINTS" => Payload(root).Bind(p => ParseSamplePoints(p, id)),
            "SAMPLE_GRID" => Payload(root).Bind(p => ParseSampleGrid(p, id)),
            "SHUTDOWN" => Accept(new Shutdown(id)),
            _ => Invalid($"Unknown type '{type}'"),
        });

    private static Result<Request> ParseSetProject(JsonElement payload, RequestId id) =>
        Required(payload, "projectId", JsonEdge.String, "a string").Bind(projectId =>
        Required(payload, "basePath", JsonEdge.String, "a string").Bind(basePath =>
            !ProjectIdShape().IsMatch(projectId)
                ? Invalid($"'projectId' must be letters, digits, '_' or '-' (it names files): '{projectId}'")
                : basePath.Trim().Length == 0
                    ? Invalid("'basePath' is empty")
                    : Accept(new SetProject(id, projectId, basePath))));

    private static Result<Request> ParseSampleGrid(JsonElement payload, RequestId id) =>
        Required(payload, "gridDist", JsonEdge.FiniteDouble, "a finite number").Bind(gridDist =>
            gridDist > 0
                ? Accept(new SampleGrid(id, gridDist))
                : Invalid("Grid distance must be > 0 m."));

    private static Result<Request> ParseSamplePoints(JsonElement payload, RequestId id) =>
        JsonEdge.Property(payload, "points") switch
        {
            Some<JsonElement> points when points.Value.ValueKind == JsonValueKind.Array =>
                ParsePoints(points.Value).Map(list => (Request)new SamplePoints(id, list)),
            Some<JsonElement> => Invalid("'points' must be an array"),
            None => Invalid("'points' is missing"),
        };

    // The first invalid point fails the request and names itself; the points
    // after it are not parsed.
    private static Result<IReadOnlyList<PointQuery>> ParsePoints(JsonElement array) =>
        array.EnumerateArray()
            .Select((element, index) => (element, index))
            .Aggregate(
                (Result<List<PointQuery>>)new Ok<List<PointQuery>>(new List<PointQuery>(array.GetArrayLength())),
                (sofar, item) => sofar.Bind(list =>
                    ParsePoint(item.element, $"points[{item.index}]").Map(point =>
                    {
                        list.Add(point);
                        return list;
                    })))
            .Map(list => (IReadOnlyList<PointQuery>)list);

    private static Result<PointQuery> ParsePoint(JsonElement point, string at) =>
        Required(point, "geomId", JsonEdge.Int64, "an integer", at).Bind(geomId =>
        Required(point, "seq", JsonEdge.Int32, "an integer", at).Bind(seq =>
        Required(point, "s", JsonEdge.FiniteDouble, "a finite number", at).Bind(s =>
        Required(point, "x", JsonEdge.FiniteDouble, "a finite number", at).Bind(x =>
        Required(point, "y", JsonEdge.FiniteDouble, "a finite number", at).Map(y =>
            new PointQuery(geomId, seq, s, x, y))))));

    private static Result<JsonElement> Payload(JsonElement root) =>
        JsonEdge.Property(root, "payload") switch
        {
            Some<JsonElement> payload when payload.Value.ValueKind == JsonValueKind.Object =>
                new Ok<JsonElement>(payload.Value),
            Some<JsonElement> => new Fault(FaultKind.InvalidArgs, "'payload' must be an object"),
            None => new Fault(FaultKind.InvalidArgs, "'payload' is missing"),
        };

    private static Result<T> Required<T>(JsonElement owner, string name, Func<JsonElement, Option<T>> read,
                                         string expected, string at = "") =>
        JsonEdge.Property(owner, name) switch
        {
            Some<JsonElement> property => read(property.Value) switch
            {
                Some<T> value => new Ok<T>(value.Value),
                None => new Fault(FaultKind.InvalidArgs, $"'{Path(at, name)}' must be {expected}"),
            },
            None => new Fault(FaultKind.InvalidArgs, $"'{Path(at, name)}' is missing"),
        };

    private static string Path(string at, string name) => at.Length == 0 ? name : at + "." + name;

    private static Result<Request> Accept(Request request) => new Ok<Request>(request);

    private static Result<Request> Invalid(string message) => new Fault(FaultKind.InvalidArgs, message);

    [GeneratedRegex("^[A-Za-z0-9_-]+$")]
    private static partial Regex ProjectIdShape();
}
