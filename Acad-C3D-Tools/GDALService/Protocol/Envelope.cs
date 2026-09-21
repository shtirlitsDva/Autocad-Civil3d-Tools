using System.Text.Json;

using GDALService.Common;

namespace GDALService.Protocol;

internal sealed record RequestId(string Value);

// A line whose id could not be read. Its reply carries no id at all rather than
// an invented one; the client then reports the reply as unmatched.
internal sealed record Unaddressed
{
    public static readonly Unaddressed Instance = new();
    private Unaddressed() { }
}

internal union ReplyTo(RequestId, Unaddressed)
{
    public Option<RequestId> Id => this switch
    {
        RequestId id => new Some<RequestId>(id),
        Unaddressed => None.Instance,
    };
}

// One request as read off the wire, before any capability looks at it: who
// asked, for what, and the payload exactly as sent. What the payload must hold
// is the capability's business. The payload is a clone, so it outlives the
// parsed line.
internal sealed record Envelope(RequestId Id, string Type, Option<JsonElement> Payload);
