using System.Text;
using System.Text.Json;

using GDALService.Common;

namespace GDALService.Protocol;

// Writes a one-line PROGRESS JSON to stderr every `every` samples - part of the
// wire, since clients parse it for their progress bars. Safe to call from the
// sampling workers: each call gets its own count from Interlocked, so exactly
// one worker reports each multiple of `every`.
internal sealed class Progress
{
    private readonly RequestId _request;
    private readonly int _total;
    private readonly int _every;
    private readonly ServiceLog _log;
    private int _done;

    public Progress(RequestId request, int total, int every, ServiceLog log)
    {
        _request = request;
        _total = total;
        _every = Math.Max(1, every);
        _log = log;
    }

    public void Advance()
    {
        int done = Interlocked.Increment(ref _done);
        if (done % _every != 0) { return; }

        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteString("id", _request.Value);
            json.WriteString("type", "PROGRESS");
            json.WriteNumber("done", done);
            json.WriteNumber("total", _total);
            json.WriteNumber("pct", _total > 0 ? done * 100.0 / _total : 0.0);
            json.WriteEndObject();
        }
        _log.Line(Encoding.UTF8.GetString(buffer.ToArray()));
    }
}
