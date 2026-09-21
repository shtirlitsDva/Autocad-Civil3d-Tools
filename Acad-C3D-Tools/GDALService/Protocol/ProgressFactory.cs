using GDALService.Common;
using GDALService.Terrain;

namespace GDALService.Protocol;

// Makes the progress reporter for one request, so a capability does not need to
// know where progress goes or how often it is written.
internal sealed class ProgressFactory
{
    private readonly ServiceLog _log;
    private readonly SamplingOptions _options;

    public ProgressFactory(ServiceLog log, SamplingOptions options)
    {
        _log = log;
        _options = options;
    }

    public Progress For(RequestId request, int total) => new(request, total, _options.ProgressEvery, _log);
}
