using System.Text.Json;

using GDALService.Common;

namespace GDALService.Protocol;

// One stdin line to one Envelope. Reading never throws: a line that is not JSON
// or has no readable id or type comes back as an InvalidArgs Fault. The reply
// address is separate from the result, because a request can be addressable
// (its id was read) and still be invalid.
internal static class RequestReader
{
    public static (ReplyTo To, Result<Envelope> Envelope) Read(string line) =>
        JsonEdge.Parse(line) switch
        {
            Ok<JsonDocument> document => Addressed(document.Value),
            Fault fault => (Unaddressed.Instance, fault),
        };

    private static (ReplyTo To, Result<Envelope> Envelope) Addressed(JsonDocument document)
    {
        using (document)
        {
            var root = document.RootElement;
            return JsonRead.Required(root, "id", JsonEdge.String, "a string") switch
            {
                Ok<string> id => (new RequestId(id.Value), Typed(root, new RequestId(id.Value))),
                Fault fault => (Unaddressed.Instance, fault),
            };
        }
    }

    private static Result<Envelope> Typed(JsonElement root, RequestId id) =>
        JsonRead.Required(root, "type", JsonEdge.String, "a string").Map(type =>
            new Envelope(id, type, JsonEdge.Property(root, "payload") switch
            {
                Some<JsonElement> payload => new Some<JsonElement>(payload.Value.Clone()),
                None => None.Instance,
            }));
}
