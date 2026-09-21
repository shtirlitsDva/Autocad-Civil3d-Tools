using System.Text.Json;

using GDALService.Common;

namespace GDALService.Protocol;

// The readers every capability shares for its payload. A missing or mistyped
// field is an InvalidArgs Fault that names the field by its path, such as
// 'points[3].x', so the client can tell which one.
internal static class JsonRead
{
    public static Result<JsonElement> Payload(Envelope request) =>
        request.Payload switch
        {
            Some<JsonElement> payload when payload.Value.ValueKind == JsonValueKind.Object =>
                new Ok<JsonElement>(payload.Value),
            Some<JsonElement> => Invalid("'payload' must be an object"),
            None => Invalid("'payload' is missing"),
        };

    public static Result<T> Required<T>(JsonElement owner, string name, Func<JsonElement, Option<T>> read,
                                        string expected, string at = "") =>
        JsonEdge.Property(owner, name) switch
        {
            Some<JsonElement> property => read(property.Value) switch
            {
                Some<T> value => new Ok<T>(value.Value),
                None => Invalid($"'{Path(at, name)}' must be {expected}"),
            },
            None => Invalid($"'{Path(at, name)}' is missing"),
        };

    public static Fault Invalid(string message) => new(FaultKind.InvalidArgs, message);

    private static string Path(string at, string name) => at.Length == 0 ? name : at + "." + name;
}
