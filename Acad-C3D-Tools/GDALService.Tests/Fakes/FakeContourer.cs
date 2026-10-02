using GDALService.Common;
using GDALService.Terrain;

namespace GDALService.Tests.Fakes;

// Answers every contour request with what `contour` says, and remembers every request.
internal sealed class FakeContourer : IContourer
{
    private readonly Func<ContourRequest, Result<ContourLines>> _contour;

    public FakeContourer(Func<ContourRequest, Result<ContourLines>> contour) { _contour = contour; }

    public List<ContourRequest> Requests { get; } = [];

    public Result<ContourLines> Contour(ContourRequest request)
    {
        Requests.Add(request);
        return _contour(request);
    }
}
