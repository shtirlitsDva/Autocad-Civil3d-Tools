using System.Text.Json;

using GDALService.Common;
using GDALService.Protocol;

namespace GDALService.Capabilities;

// SHUTDOWN: answers BYE, and the service stops reading after that reply.
internal sealed class Shutdown : ICapability
{
    public string Type => "SHUTDOWN";

    public Result<Reply> Handle(Envelope request) => Reply.Stop(Bye.Instance);

    private sealed class Bye : IReplyBody
    {
        public static readonly Bye Instance = new();

        public void WriteTo(Utf8JsonWriter json) => json.WriteString("msg", "BYE");
    }
}
