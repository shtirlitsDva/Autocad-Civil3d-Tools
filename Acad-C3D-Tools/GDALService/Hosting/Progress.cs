using System.Text.Json;

namespace GDALService.Hosting;

// Emits a one-line PROGRESS JSON on the log stream every `every` points. Safe to
// call from the sampling workers: each call gets its own count from Interlocked,
// so exactly one worker reports each multiple of `every`.
internal sealed class Progress
{
    public const int DefaultEvery = 500;

    private readonly string _requestId;
    private readonly int _total;
    private readonly int _every;
    private readonly TextWriter _log;
    private int _done;

    public Progress(string requestId, int total, TextWriter log, int every = DefaultEvery)
    {
        _requestId = requestId;
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
            json.WriteString("id", _requestId);
            json.WriteString("type", "PROGRESS");
            json.WriteNumber("done", done);
            json.WriteNumber("total", _total);
            json.WriteNumber("pct", _total > 0 ? done * 100.0 / _total : 0.0);
            json.WriteEndObject();
        }
        _log.WriteLine(System.Text.Encoding.UTF8.GetString(buffer.ToArray()));
    }
}
