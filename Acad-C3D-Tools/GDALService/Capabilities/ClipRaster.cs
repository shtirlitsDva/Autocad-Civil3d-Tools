using System.Text.Json;

using GDALService.Common;
using GDALService.Protocol;
using GDALService.Terrain;

namespace GDALService.Capabilities;

// CLIP_RASTER {sources:[path], bounds:{minX, minY, maxX, maxY}, targetWkt,
// resolution, outPath}: the source rasters cut to the box, warped into the
// target CRS and resampled to `resolution`, written as one GeoTIFF at outPath.
// The box and the resolution are in the target CRS. It needs no open project:
// NSGIS uses it to turn downloaded terrain tiles into the drawing's terrain.
internal sealed class ClipRaster : ICapability
{
    private readonly IRasterClipper _clipper;

    public ClipRaster(IRasterClipper clipper) { _clipper = clipper; }

    public string Type => "CLIP_RASTER";

    public Result<Reply> Handle(Envelope request) =>
        JsonRead.Payload(request)
            .Bind(Parse)
            .Bind(_clipper.Clip)
            .Bind(clipped => Reply.Continue(new Clipped(clipped)));

    private static Result<ClipRequest> Parse(JsonElement payload) =>
        ParseSources(payload).Bind(sources =>
        JsonRead.Required(payload, "bounds", Object, "an object").Bind(bounds =>
        JsonRead.Required(bounds, "minX", JsonEdge.FiniteDouble, "a finite number", "bounds").Bind(minX =>
        JsonRead.Required(bounds, "minY", JsonEdge.FiniteDouble, "a finite number", "bounds").Bind(minY =>
        JsonRead.Required(bounds, "maxX", JsonEdge.FiniteDouble, "a finite number", "bounds").Bind(maxX =>
        JsonRead.Required(bounds, "maxY", JsonEdge.FiniteDouble, "a finite number", "bounds").Bind(maxY =>
        JsonRead.Required(payload, "targetWkt", JsonEdge.String, "a string").Bind(targetWkt =>
        JsonRead.Required(payload, "resolution", JsonEdge.FiniteDouble, "a finite number").Bind(resolution =>
        JsonRead.Required(payload, "outPath", JsonEdge.String, "a string").Bind(outPath =>
            minX >= maxX || minY >= maxY
                ? (Result<ClipRequest>)JsonRead.Invalid("'bounds' must have minX < maxX and minY < maxY")
                : resolution <= 0
                    ? JsonRead.Invalid("'resolution' must be greater than 0")
                    : targetWkt.Trim().Length == 0
                        ? JsonRead.Invalid("'targetWkt' is empty")
                        : outPath.Trim().Length == 0
                            ? JsonRead.Invalid("'outPath' is empty")
                            : new Ok<ClipRequest>(new ClipRequest(sources, minX, minY, maxX, maxY, targetWkt, resolution, outPath)))))))))));

    private static Option<JsonElement> Object(JsonElement element) =>
        element.ValueKind == JsonValueKind.Object ? new Some<JsonElement>(element) : None.Instance;

    private static Result<IReadOnlyList<string>> ParseSources(JsonElement payload) =>
        JsonEdge.Property(payload, "sources") switch
        {
            Some<JsonElement> sources when sources.Value.ValueKind == JsonValueKind.Array =>
                sources.Value.EnumerateArray()
                    .Select((element, index) => (element, index))
                    .Aggregate(
                        (Result<List<string>>)new Ok<List<string>>([]),
                        (sofar, item) => sofar.Bind(list => JsonEdge.String(item.element) switch
                        {
                            Some<string> path when path.Value.Trim().Length > 0 => Add(list, path.Value),
                            Some<string> => JsonRead.Invalid($"'sources[{item.index}]' is empty"),
                            None => JsonRead.Invalid($"'sources[{item.index}]' must be a string"),
                        }))
                    .Bind(list => list.Count == 0
                        ? (Result<IReadOnlyList<string>>)JsonRead.Invalid("'sources' is empty")
                        : new Ok<IReadOnlyList<string>>(list)),
            Some<JsonElement> => JsonRead.Invalid("'sources' must be an array"),
            None => JsonRead.Invalid("'sources' is missing"),
        };

    private static Result<List<string>> Add(List<string> list, string path)
    {
        list.Add(path);
        return new Ok<List<string>>(list);
    }

    private sealed record Clipped(ClippedRaster Raster) : IReplyBody
    {
        public void WriteTo(Utf8JsonWriter json)
        {
            json.WriteString("outPath", Raster.OutPath);
            json.WriteNumber("width", Raster.Width);
            json.WriteNumber("height", Raster.Height);
        }
    }
}
