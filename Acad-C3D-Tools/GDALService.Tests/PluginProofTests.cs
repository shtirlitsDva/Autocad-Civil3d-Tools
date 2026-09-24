using System.Text.Json;

using GDALService.Capabilities;
using GDALService.Common;
using GDALService.Project;
using GDALService.Protocol;

namespace GDALService.Tests;

// The architecture's promise, proven: a capability the service has never heard
// of - defined here, in the test project - is added with one registration and
// answered end to end. No file of the service was edited for it. It even uses a
// service dependency (the open project), which the container supplies.
public sealed class PluginProofTests : IDisposable
{
    private readonly TileFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    // ECHO {text}: answers the text back, with the open project's id if any.
    private sealed class Echo : ICapability
    {
        private readonly ProjectStore _projects;

        public Echo(ProjectStore projects) { _projects = projects; }

        public string Type => "ECHO";

        public Result<Reply> Handle(Envelope request) =>
            JsonRead.Payload(request)
                .Bind(payload => JsonRead.Required(payload, "text", JsonEdge.String, "a string"))
                .Bind(text => Reply.Continue(new Echoed(text, _projects.Current.Map(open => open.ProjectId))));

        private sealed record Echoed(string Text, Result<string> Project) : IReplyBody
        {
            public void WriteTo(Utf8JsonWriter json)
            {
                json.WriteString("text", Text);
                Project.Switch(id => json.WriteString("project", id), _ => { });
            }
        }
    }

    [Fact]
    public void A_capability_defined_outside_the_service_is_answered_after_one_registration()
    {
        var setProject = JsonSerializer.Serialize(new { id = "p", type = "SET_PROJECT", payload = new { projectId = "TST", basePath = _folder.BasePath } });

        var (stdout, _) = ServiceHarness.Run(GdalForTests.Loaded, services => services.AddCapability<Echo>(),
            """{"id":"e1","type":"ECHO","payload":{"text":"hej"}}""",
            setProject,
            """{"id":"e2","type":"ECHO","payload":{"text":"igen"}}""",
            """{"id":"e3","type":"ECHO","payload":{}}""");
        var replies = ServiceHarness.Replies(stdout);

        Assert.Equal("hej", replies[0].GetProperty("result").GetProperty("text").GetString());
        Assert.False(replies[0].GetProperty("result").TryGetProperty("project", out _));
        Assert.Equal("TST", replies[2].GetProperty("result").GetProperty("project").GetString());
        Assert.Equal((2, "'text' is missing"), (ServiceHarness.Status(replies[3]), replies[3].GetProperty("error").GetString()));
    }
}
