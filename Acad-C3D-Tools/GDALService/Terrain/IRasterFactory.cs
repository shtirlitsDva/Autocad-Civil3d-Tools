using GDALService.Common;

namespace GDALService.Terrain;

// Opens one raster over a set of tiles - a mosaic named after the project.
internal interface IRasterFactory
{
    Result<IRaster> OpenMosaic(string name, IReadOnlyList<string> tilePaths);
}
