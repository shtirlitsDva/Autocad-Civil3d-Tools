using System.Text.Json;

using GDALService.Common;

namespace GDALService.Protocol;

// What a capability answers with: the members of the reply's "result" object.
// Each capability owns its reply shape, so a new capability brings its own.
internal interface IReplyBody
{
    void WriteTo(Utf8JsonWriter json);
}

// Whether the service keeps reading after this reply. Only SHUTDOWN stops it.
internal enum AfterReply { Continue, Stop }

internal sealed record Reply(IReplyBody Body, AfterReply Then)
{
    public static Result<Reply> Continue(IReplyBody body) => new Ok<Reply>(new Reply(body, AfterReply.Continue));

    public static Result<Reply> Stop(IReplyBody body) => new Ok<Reply>(new Reply(body, AfterReply.Stop));
}
