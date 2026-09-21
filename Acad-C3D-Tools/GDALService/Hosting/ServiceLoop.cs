using GDALService.Common;
using GDALService.Domain;
using GDALService.Project;
using GDALService.Protocol;
using GDALService.Raster;

namespace GDALService.Hosting;

// Reads one request per line, writes one reply per line, until the input ends
// or a SHUTDOWN arrives. The streams come in from outside, so the whole
// protocol can be driven in-process by tests.
internal sealed class ServiceLoop
{
    private readonly TextReader _input;
    private readonly TextWriter _output;
    private readonly TextWriter _log;
    private readonly Result<string> _gdal;

    public ServiceLoop(TextReader input, TextWriter output, TextWriter log, Result<string> gdal)
    {
        _input = input;
        _output = output;
        // Sampling workers report progress on the log from several threads.
        _log = TextWriter.Synchronized(log);
        _gdal = gdal;
    }

    public int Run()
    {
        _log.WriteLine(_gdal switch
        {
            Ok<string> release => "READY gdal=" + release.Value,
            Fault fault => "READY without GDAL: " + fault.Message,
        });

        using var projects = new ProjectStore(_log);
        foreach (var line in StreamEdge.Lines(_input))
        {
            if (line.Trim().Length == 0) { continue; }

            var (reply, stop) = Answer(line, projects);
            _output.WriteLine(reply);
            _output.Flush();
            if (stop) { break; }
        }
        return 0;
    }

    private (string Reply, bool Stop) Answer(string line, ProjectStore projects)
    {
        // Parsing, dispatch and serialisation all sit inside the guard: #175 was
        // a reply that failed to serialise outside the old one.
        ReplyTo to = Unaddressed.Instance;
        try
        {
            (to, var request) = RequestParser.Parse(line);
            var (reply, stop) = request switch
            {
                Ok<Request> ok => Dispatch(ok.Value, projects),
                Fault fault => (fault, false),
            };
            return (ReplyWriter.Serialize(to, reply), stop);
        }
        catch (Exception ex)
        {
            // The one last-resort guard. Nothing inside is expected to throw -
            // every failure the service anticipates is a Fault - so reaching this
            // is a bug. It is answered, logged in full, and the loop goes on.
            _log.WriteLine("BUG " + ex);
            return (ReplyWriter.Serialize(to, new Fault(FaultKind.Internal, "internal error: " + ex.Message)), false);
        }
    }

    private (Result<ReplyBody> Reply, bool Stop) Dispatch(Request request, ProjectStore projects) => request switch
    {
        Hello => (Reply(HelloAck.Instance), false),
        SetProject set => (WithGdal(() => projects.Open(set.ProjectId, set.BasePath).Map(Describe)), false),
        SamplePoints points => (WithGdal(() => projects.Current.Map(open =>
        {
            var rows = Sampler.Points(open.Raster, points.Points, new Progress(points.Id.Value, points.Points.Count, _log));
            return (ReplyBody)new PointsSampled(rows, SampleSummary.Of(rows.Select(r => r.Sample)));
        })), false),
        SampleGrid grid => (WithGdal(() => projects.Current.Bind(open =>
            Sampler.Grid(open.Raster, grid.GridDist, total => new Progress(grid.Id.Value, total, _log)).Map(cells =>
                (ReplyBody)new GridSampled(cells, SampleSummary.Of(cells.Select(c => c.Sample)))))), false),
        Shutdown => (Reply(Bye.Instance), true),
    };

    // A request that needs GDAL is refused, not crashed, when GDAL did not load.
    private Result<ReplyBody> WithGdal(Func<Result<ReplyBody>> work) => _gdal switch
    {
        Ok<string> => work(),
        Fault fault => fault,
    };

    private static ReplyBody Describe(OpenProject open) => new ProjectOpened(
        open.ProjectId, open.ElevationsDir, open.Raster.Path,
        open.Raster.Width, open.Raster.Height, open.Raster.Bands, open.Raster.Projection);

    private static Result<ReplyBody> Reply(ReplyBody body) => new Ok<ReplyBody>(body);
}
