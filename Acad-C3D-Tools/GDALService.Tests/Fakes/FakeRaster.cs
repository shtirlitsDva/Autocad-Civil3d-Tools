using GDALService.Common;
using GDALService.Terrain;

namespace GDALService.Tests.Fakes;

// A raster in memory: `width` x `height` pixels of 1 m, the top-left corner at
// (left, top), each pixel's value from `pixel(column, row)`. Reads can be made to
// fail per pixel, and opening a reader can be made to fail altogether, which a
// real GDAL raster cannot be asked to do.
internal sealed class FakeRaster : IRaster
{
    private readonly Func<int, int, double> _pixel;
    private readonly HashSet<(int Column, int Row)> _failing = [];
    private int _disposed;
    private int _readers;

    public FakeRaster(int width, int height, Func<int, int, double> pixel, Option<double> noData,
                      double left = 0, double top = 0, string source = "fake")
    {
        _pixel = pixel;
        var toGeo = new GeoTransform(left, 1, 0, top, 0, -1);
        Info = new RasterInfo(source, width, height, 1, toGeo, Expect.Some(toGeo.Invert()), noData, None.Instance);
    }

    public RasterInfo Info { get; }

    public bool ReadersFail { get; init; }

    public int DisposeCount => _disposed;

    public int ReadersOpened => _readers;

    // Set up before sampling starts; read concurrently by the workers after.
    public FakeRaster FailAt(int column, int row)
    {
        _failing.Add((column, row));
        return this;
    }

    public Result<IPixelReader> OpenReader()
    {
        Interlocked.Increment(ref _readers);
        return ReadersFail
            ? new Fault(FaultKind.Gdal, "the raster has no band 1")
            : new Ok<IPixelReader>(new Reader(this));
    }

    public void Dispose() => Interlocked.Increment(ref _disposed);

    private sealed class Reader(FakeRaster raster) : IPixelReader
    {
        public Result<double> Read(int column, int row) =>
            raster._failing.Contains((column, row))
                ? new Fault(FaultKind.Gdal, $"raster read failed at pixel ({column}, {row})")
                : new Ok<double>(raster._pixel(column, row));

        public void Dispose() { }
    }
}
