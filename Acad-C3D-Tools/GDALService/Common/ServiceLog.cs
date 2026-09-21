namespace GDALService.Common;

// The service's stderr: the READY banner, warnings, bugs and the PROGRESS lines
// clients parse. Lines are written whole and one at a time, because sampling
// workers report progress from several threads. Plain lines on purpose - the
// clients read this stream, so its format is part of the protocol.
internal sealed class ServiceLog
{
    private readonly TextWriter _error;

    public ServiceLog(TextWriter error) { _error = TextWriter.Synchronized(error); }

    public void Ready(Result<string> gdal) => _error.WriteLine(gdal switch
    {
        Ok<string> release => "READY gdal=" + release.Value,
        Fault fault => "READY without GDAL: " + fault.Message,
    });

    public void Warn(string message) => _error.WriteLine("WARN " + message);

    public void Bug(Exception bug) => Bug(bug.ToString());

    public void Bug(string bug) => _error.WriteLine("BUG " + bug);

    public void Line(string line) => _error.WriteLine(line);
}
