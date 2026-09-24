using System.Text.Json;

using GDALService.Common;
using GDALService.Domain;
using GDALService.Project;
using GDALService.Protocol;
using GDALService.Terrain;

namespace GDALService.Capabilities;

// SAMPLE_GRID {gridDist}: a regular grid of ground heights over the open
// project's whole extent, gridDist metres apart.
internal sealed class SampleGrid : ICapability
{
    private readonly ProjectStore _projects;
    private readonly Sampler _sampler;
    private readonly ProgressFactory _progress;

    public SampleGrid(ProjectStore projects, Sampler sampler, ProgressFactory progress)
    {
        _projects = projects;
        _sampler = sampler;
        _progress = progress;
    }

    public string Type => "SAMPLE_GRID";

    public Result<Reply> Handle(Envelope request) =>
        JsonRead.Payload(request).Bind(ParseGridDist).Bind(gridDist =>
        _projects.Current.Bind(open =>
        _sampler.Grid(open.Raster, gridDist, total => _progress.For(request.Id, total).Advance).Bind(cells =>
            Reply.Continue(new Sampled(cells, SampleSummary.Of(cells.Select(c => c.Sample)))))));

    private static Result<double> ParseGridDist(JsonElement payload) =>
        JsonRead.Required(payload, "gridDist", JsonEdge.FiniteDouble, "a finite number").Bind(gridDist =>
            gridDist > 0
                ? (Result<double>)new Ok<double>(gridDist)
                : JsonRead.Invalid("Grid distance must be > 0 m."));

    // The grid reply has always listed only cells that are on the raster: ground
    // with a height, and ground marked NoData. Only the former carries "z"; the
    // summary still counts every cell.
    private sealed record Sampled(IReadOnlyList<GridSample> Cells, SampleSummary Summary) : IReplyBody
    {
        public void WriteTo(Utf8JsonWriter json)
        {
            WireFormat.Summary(json, Summary);
            json.WriteStartArray("rows");
            foreach (var cell in Cells.Where(c => Listed(c.Sample)))
            {
                json.WriteStartObject();
                json.WriteNumber("x", cell.X);
                json.WriteNumber("y", cell.Y);
                WireFormat.Height(json, "z", cell.Sample);
                json.WriteString("status", cell.Sample.WireStatus);
                json.WriteEndObject();
            }
            json.WriteEndArray();
        }

        private static bool Listed(Sample sample) => sample switch
        {
            Elevation => true,
            NoData => true,
            Outside => false,
            ReadFailed => false,
        };
    }
}
