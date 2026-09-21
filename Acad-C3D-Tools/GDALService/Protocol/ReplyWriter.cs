using System.Text;
using System.Text.Json;

using GDALService.Common;

namespace GDALService.Protocol;

// Writes one reply line: {"id","status","result"} on success, {"id","status",
// "error"} on failure. A field with nothing to say is left out rather than
// written as null - an unaddressed reply has no "id".
internal static class ReplyWriter
{
    public static string Serialize(ReplyTo to, Result<IReplyBody> reply)
    {
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            to.Id.Switch(id => json.WriteString("id", id.Value), () => { });
            reply.Switch(
                body =>
                {
                    json.WriteNumber("status", 0);
                    json.WriteStartObject("result");
                    body.WriteTo(json);
                    json.WriteEndObject();
                },
                fault =>
                {
                    json.WriteNumber("status", StatusOf(fault.Kind));
                    json.WriteString("error", fault.Message);
                });
            json.WriteEndObject();
        }
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    // The wire's numeric status codes, unchanged from the old protocol.
    public static int StatusOf(FaultKind kind) => kind switch
    {
        FaultKind.Gdal => 1,
        FaultKind.Internal => 1,
        FaultKind.InvalidArgs => 2,
        FaultKind.NotFound => 3,
        FaultKind.NotInitialized => 4,
    };
}
