using GDALService.Capabilities;
using GDALService.Common;
using GDALService.Protocol;

namespace GDALService.Hosting;

// Reads one request per line, hands it to the capability for its type, writes
// one reply per line - until the input ends or a capability says stop. It knows
// no request type by name: those are the registry's.
internal sealed class ServiceLoop
{
    private readonly ServiceStreams _streams;
    private readonly CapabilityRegistry _capabilities;
    private readonly ServiceLog _log;
    private readonly GdalStatus _gdal;

    public ServiceLoop(ServiceStreams streams, CapabilityRegistry capabilities, ServiceLog log, GdalStatus gdal)
    {
        _streams = streams;
        _capabilities = capabilities;
        _log = log;
        _gdal = gdal;
    }

    public int Run()
    {
        _log.Ready(_gdal.Release);

        foreach (var line in StreamEdge.Lines(_streams.Input))
        {
            if (line.Trim().Length == 0) { continue; }

            var (reply, then) = Answer(line);
            _streams.Output.WriteLine(reply);
            _streams.Output.Flush();
            if (then == AfterReply.Stop) { break; }
        }
        return 0;
    }

    private (string Reply, AfterReply Then) Answer(string line)
    {
        // Reading, dispatch and serialisation all sit inside the guard: #175 was
        // a reply that failed to serialise outside the old one.
        ReplyTo to = Unaddressed.Instance;
        try
        {
            (to, var request) = RequestReader.Read(line);
            var reply = request.Bind(envelope =>
                _capabilities.Find(envelope.Type).Bind(capability => capability.Handle(envelope)));
            var then = reply switch
            {
                Ok<Reply> ok => ok.Value.Then,
                Fault => AfterReply.Continue,
            };
            return (ReplyWriter.Serialize(to, reply.Map(r => r.Body)), then);
        }
        catch (Exception ex)
        {
            // The one last-resort guard. Nothing inside is expected to throw -
            // every failure the service anticipates is a Fault - so reaching this
            // is a bug. It is answered, logged in full, and the loop goes on.
            _log.Bug(ex);
            return (ReplyWriter.Serialize(to, new Fault(FaultKind.Internal, "internal error: " + ex.Message)),
                    AfterReply.Continue);
        }
    }
}
