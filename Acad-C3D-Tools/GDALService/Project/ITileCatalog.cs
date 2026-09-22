using GDALService.Common;

namespace GDALService.Project;

// Where a project's elevation tiles are: `<basePath>\Elevations\<projectId>_<n>.tif`.
internal interface ITileCatalog
{
    Result<TileSet> Find(string projectId, string basePath);
}
