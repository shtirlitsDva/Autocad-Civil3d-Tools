using GDALService.Common;
using GDALService.Terrain;

namespace GDALService.Tests.Fakes;

// Opens whatever `open` says for a mosaic, and remembers every mosaic asked for.
internal sealed class FakeRasterFactory : IRasterFactory
{
    private readonly Func<string, IReadOnlyList<string>, Result<IRaster>> _open;

    public FakeRasterFactory(Func<string, IReadOnlyList<string>, Result<IRaster>> open) { _open = open; }

    public static FakeRasterFactory Always(IRaster raster) => new((_, _) => new Ok<IRaster>(raster));

    public static FakeRasterFactory Failing(Fault fault) => new((_, _) => fault);

    public List<(string Name, IReadOnlyList<string> Tiles)> Opened { get; } = [];

    public Result<IRaster> OpenMosaic(string name, IReadOnlyList<string> tilePaths)
    {
        Opened.Add((name, tilePaths));
        return _open(name, tilePaths);
    }
}
