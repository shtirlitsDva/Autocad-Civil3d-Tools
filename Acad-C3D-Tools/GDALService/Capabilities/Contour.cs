using System.Text.Json;

using GDALService.Common;
using GDALService.Protocol;
using GDALService.Terrain;

namespace GDALService.Capabilities;

// CONTOUR {source, interval, outPath}: contour lines through the source
// raster every `interval` (from 0), written as GeoJSON at outPath: 3D lines in
// the raster's CRS, the height as z and in "elev". Needs no open project: NSGIS
// uses it on a terrain cut made by CLIP_RASTER.
internal sealed class Contour : ICapability
{
    private readonly IContourer _contourer;

    public Contour(IContourer contourer) { _contourer = contourer; }

    public string Type => "CONTOUR";

    public Result<Reply> Handle(Envelope request) =>
        JsonRead.Payload(request)
            .Bind(Parse)
            .Bind(_contourer.Contour)
            .Bind(lines => Reply.Continue(new Traced(lines)));

    private static Result<ContourRequest> Parse(JsonElement payload) =>
        JsonRead.Required(payload, "source", JsonEdge.String, "a string").Bind(source =>
        JsonRead.Required(payload, "interval", JsonEdge.FiniteDouble, "a finite number").Bind(interval =>
        JsonRead.Required(payload, "outPath", JsonEdge.String, "a string").Bind(outPath =>
            source.Trim().Length == 0
                ? (Result<ContourRequest>)JsonRead.Invalid("'source' is empty")
                : interval <= 0
                    ? JsonRead.Invalid("'interval' must be greater than 0")
                    : outPath.Trim().Length == 0
                        ? JsonRead.Invalid("'outPath' is empty")
                        : new Ok<ContourRequest>(new ContourRequest(source, interval, outPath)))));

    private sealed record Traced(ContourLines Lines) : IReplyBody
    {
        public void WriteTo(Utf8JsonWriter json)
        {
            json.WriteString("outPath", Lines.OutPath);
            json.WriteNumber("count", Lines.Count);
        }
    }
}
