using GDALService.Common;

namespace GDALService.Terrain;

// Opens one raster over a set of tiles - a mosaic named after the project.
internal interface IRasterFactory
{
    // Why this factory can open no raster at all, if so - known before any tile
    // is looked for, so a client is told the cause rather than a symptom.
    Option<Fault> Unavailable { get; }

    Result<IRaster> OpenMosaic(string name, IReadOnlyList<string> tilePaths);
}
