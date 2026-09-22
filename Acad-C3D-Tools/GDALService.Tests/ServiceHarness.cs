using System.Text.Json;

using GDALService.Common;
using GDALService.Hosting;
using GDALService.Terrain;

using Microsoft.Extensions.DependencyInjection;

namespace GDALService.Tests;

// The service in-process, wired by the same ServiceComposition the executable
// uses, with the console swapped for strings. `configure` swaps an edge for a
// fake or adds a capability, exactly as a real registration would.
internal static class ServiceHarness
{
    public static (string Stdout, string Stderr) Run(Result<string> gdal, Action<IServiceCollection> configure,
                                                     params string[] lines)
    {
        var output = new StringWriter { NewLine = "\n" };
        var error = new StringWriter { NewLine = "\n" };
        var streams = new ServiceStreams(new StringReader(string.Join("\n", lines)), output, error);
        using (var provider = ServiceComposition.Build(streams, gdal, new SamplingOptions(), configure))
        {
            Expect.Ok(ServiceComposition.Loop(provider)).Run();
        }
        return (output.ToString(), error.ToString());
    }

    public static (string Stdout, string Stderr) Run(params string[] lines) => Run(GdalForTests.Loaded, _ => { }, lines);

    public static List<JsonElement> Replies(string stdout) =>
        [.. stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => JsonElement.Parse(line))];

    public static int Status(JsonElement reply) => reply.GetProperty("status").GetInt32();
}
