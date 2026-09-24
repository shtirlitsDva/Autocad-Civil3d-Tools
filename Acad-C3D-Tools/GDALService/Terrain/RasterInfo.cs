using GDALService.Common;

namespace GDALService.Terrain;

// What a sampler needs to know about an open raster, decided once when it is
// opened: its size, its two transforms (the inverse is known to exist), and its
// NoData value and projection if it declares them.
internal sealed record RasterInfo(string Source, int Width, int Height, int Bands,
                                  GeoTransform ToGeo, GeoTransform ToPixel,
                                  Option<double> NoData, Option<string> Projection);
