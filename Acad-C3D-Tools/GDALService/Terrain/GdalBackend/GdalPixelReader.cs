using GDALService.Common;

using OSGeo.GDAL;

namespace GDALService.Terrain.GdalBackend;

// One worker's band of a thread-safe dataset, as GDAL's OF_THREAD_SAFE mode
// expects: each thread fetches its own band and releases it when done.
internal sealed class GdalPixelReader : IPixelReader
{
    private readonly Band _band;

    public GdalPixelReader(Band band) { _band = band; }

    public Result<double> Read(int column, int row) => GdalEdge.ReadPixel(_band, column, row);

    public void Dispose() => _band.Dispose();
}
