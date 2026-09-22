using System.Text.Json;
using System.Text.RegularExpressions;

using GDALService.Common;
using GDALService.Project;
using GDALService.Protocol;

namespace GDALService.Capabilities;

// SET_PROJECT {projectId, basePath}: opens the project's tiles for sampling,
// or keeps the open one when it is the same project over the same tiles.
internal sealed partial class SetProject : ICapability
{
    private readonly ProjectStore _projects;

    public SetProject(ProjectStore projects) { _projects = projects; }

    public string Type => "SET_PROJECT";

    public Result<Reply> Handle(Envelope request) =>
        JsonRead.Payload(request)
            .Bind(Parse)
            .Bind(asked => _projects.Open(asked.ProjectId, asked.BasePath))
            .Bind(open => Reply.Continue(new Opened(open)));

    private sealed record Asked(string ProjectId, string BasePath);

    private static Result<Asked> Parse(JsonElement payload) =>
        JsonRead.Required(payload, "projectId", JsonEdge.String, "a string").Bind(projectId =>
        JsonRead.Required(payload, "basePath", JsonEdge.String, "a string").Bind(basePath =>
            !ProjectIdShape().IsMatch(projectId)
                ? (Result<Asked>)JsonRead.Invalid($"'projectId' must be letters, digits, '_' or '-' (it names files): '{projectId}'")
                : basePath.Trim().Length == 0
                    ? JsonRead.Invalid("'basePath' is empty")
                    : new Ok<Asked>(new Asked(projectId, basePath))));

    [GeneratedRegex("^[A-Za-z0-9_-]+$")]
    private static partial Regex ProjectIdShape();

    private sealed record Opened(OpenProject Project) : IReplyBody
    {
        public void WriteTo(Utf8JsonWriter json)
        {
            var info = Project.Raster.Info;
            json.WriteString("projectId", Project.ProjectId);
            json.WriteString("elevationsDir", Project.Tiles.ElevationsDir);
            json.WriteString("vrtPath", info.Source);
            json.WriteNumber("width", info.Width);
            json.WriteNumber("height", info.Height);
            json.WriteNumber("bands", info.Bands);
            info.Projection.Switch(wkt => json.WriteString("projection", wkt), () => { });
        }
    }
}
