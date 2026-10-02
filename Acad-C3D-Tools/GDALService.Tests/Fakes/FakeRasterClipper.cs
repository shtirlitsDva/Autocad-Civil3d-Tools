using GDALService.Common;
using GDALService.Terrain;

namespace GDALService.Tests.Fakes;

// Answers every clip with what `clip` says, and remembers every request.
internal sealed class FakeRasterClipper : IRasterClipper
{
    private readonly Func<ClipRequest, Result<ClippedRaster>> _clip;

    public FakeRasterClipper(Func<ClipRequest, Result<ClippedRaster>> clip) { _clip = clip; }

    public List<ClipRequest> Requests { get; } = [];

    public Result<ClippedRaster> Clip(ClipRequest request)
    {
        Requests.Add(request);
        return _clip(request);
    }
}
