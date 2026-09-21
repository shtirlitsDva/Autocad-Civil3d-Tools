using System.Text.Json;

using GDALService.Common;

namespace GDALService.Protocol;

// The boundary to System.Text.Json. JsonDocument.Parse reports malformed input
// only by throwing and has no Try form, so this is where that exception becomes
// a Fault. Every other read goes through the Try* members below, which cannot
// throw, and none of them hands back a null.
internal static class JsonEdge
{
    public static Result<JsonDocument> Parse(string line)
    {
        try
        {
            return new Ok<JsonDocument>(JsonDocument.Parse(line));
        }
        catch (JsonException ex)
        {
            return new Fault(FaultKind.InvalidArgs, "Bad JSON: " + ex.Message);
        }
    }

    // A property of an object. Anything that is not an object has no properties.
    public static Option<JsonElement> Property(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value)
            ? new Some<JsonElement>(value)
            : None.Instance;

    public static Option<string> String(JsonElement element) =>
        element.ValueKind == JsonValueKind.String && element.GetString() is string text
            ? new Some<string>(text)
            : None.Instance;

    // Only a finite JSON number; an overflowing literal such as 1e400 is not one.
    public static Option<double> FiniteDouble(JsonElement element) =>
        element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out var value) && double.IsFinite(value)
            ? new Some<double>(value)
            : None.Instance;

    public static Option<long> Int64(JsonElement element) =>
        element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out var value)
            ? new Some<long>(value)
            : None.Instance;

    public static Option<int> Int32(JsonElement element) =>
        element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var value)
            ? new Some<int>(value)
            : None.Instance;
}
