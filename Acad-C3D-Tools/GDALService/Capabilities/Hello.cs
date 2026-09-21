using System.Text.Json;

using GDALService.Common;
using GDALService.Protocol;

namespace GDALService.Capabilities;

// HELLO: the client's check that the service is up. Any payload is ignored.
internal sealed class Hello : ICapability
{
    public string Type => "HELLO";

    public Result<Reply> Handle(Envelope request) => Reply.Continue(Ack.Instance);

    private sealed class Ack : IReplyBody
    {
        public static readonly Ack Instance = new();

        public void WriteTo(Utf8JsonWriter json) => json.WriteString("msg", "HELLO_ACK");
    }
}
