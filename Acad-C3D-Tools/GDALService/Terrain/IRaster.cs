using GDALService.Common;

namespace GDALService.Terrain;

// An open elevation raster. Disposing it releases everything opening it took,
// including an in-memory mosaic.
internal interface IRaster : IDisposable
{
    RasterInfo Info { get; }

    // One reader per worker thread; a reader is not shared between threads.
    Result<IPixelReader> OpenReader();
}

internal interface IPixelReader : IDisposable
{
    Result<double> Read(int column, int row);
}
