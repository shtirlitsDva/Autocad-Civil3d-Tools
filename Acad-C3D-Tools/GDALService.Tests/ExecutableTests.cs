using System.Diagnostics;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace GDALService.Tests;

// The built GDALService.exe as the client runs it: a child process with UTF-8
// on stdin and stdout. This is the only test that exercises Program itself -
// the console encoding and the GDAL start-up next to the executable.
public sealed class ExecutableTests : IDisposable
{
    private readonly TileFolder _folder = new("Grønnegård æøå");

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void The_executable_reads_a_danish_base_path_from_utf8_stdin()
    {
        var exe = Path.Combine(AppContext.BaseDirectory, "GDALService.exe");
        var start = new ProcessStartInfo(exe)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = Encoding.UTF8,
            UseShellExecute = false,
            // CREATE_NO_WINDOW, as GdalServiceClient.cpp starts it: the child
            // gets a hidden console whose code page is the OEM one, which is what
            // Program's UTF-8 console setup exists to override.
            CreateNoWindow = true,
        };
        // The test runs on whichever .NET 11 runtime hosts it (it may be a
        // user-local install); the child is pointed at the same one.
        start.Environment["DOTNET_ROOT"] = Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(typeof(object).Assembly.Location)!, "..", "..", ".."));

        using var process = Process.Start(start)!;
        var request = new { id = "p", type = "SET_PROJECT", payload = new { projectId = "TST", basePath = _folder.BasePath } };
        // Raw UTF-8 letters, as nlohmann::json writes them - not ø escapes,
        // which would reach the service as plain ASCII and prove nothing.
        var raw = new JsonSerializerOptions { Encoder = JavaScriptEncoder.Create(UnicodeRanges.All) };
        var line = JsonSerializer.Serialize(request, raw);
        Assert.Contains("ø", line);
        process.StandardInput.WriteLine(line);
        process.StandardInput.Close();

        var reply = JsonDocument.Parse(process.StandardOutput.ReadLine() ?? "{}").RootElement;
        Assert.True(process.WaitForExit(30_000));

        Assert.Equal(0, reply.GetProperty("status").GetInt32());
        Assert.Equal(_folder.ElevationsDir, reply.GetProperty("result").GetProperty("elevationsDir").GetString());
    }
}
