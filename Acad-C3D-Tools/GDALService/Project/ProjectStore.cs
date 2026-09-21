using GDALService.Common;
using GDALService.Terrain;

namespace GDALService.Project;

// A tile set opened for sampling: which project, built from exactly which
// tiles, and the raster over them.
internal sealed record OpenProject(string ProjectId, TileSet Tiles, IRaster Raster) : IDisposable
{
    // The same project, from the same folder, over byte-for-byte the same tiles.
    public bool Serves(string projectId, TileSet tiles) =>
        string.Equals(ProjectId, projectId, StringComparison.OrdinalIgnoreCase)
        && string.Equals(Tiles.BasePath, tiles.BasePath, StringComparison.OrdinalIgnoreCase)
        && Tiles.Tiles.SequenceEqual(tiles.Tiles);

    public void Dispose() => Raster.Dispose();
}

// The one project the service is serving, if any. The request loop is single
// threaded, so this holds no lock.
internal sealed class ProjectStore : IDisposable
{
    private readonly ITileCatalog _catalog;
    private readonly IRasterFactory _rasters;
    private Option<OpenProject> _current = None.Instance;

    public ProjectStore(ITileCatalog catalog, IRasterFactory rasters)
    {
        _catalog = catalog;
        _rasters = rasters;
    }

    public Result<OpenProject> Current => _current switch
    {
        Some<OpenProject> open => new Ok<OpenProject>(open.Value),
        None => new Fault(FaultKind.NotInitialized, "No project initialized. Call SET_PROJECT first always!"),
    };

    public Result<OpenProject> Open(string projectId, string basePath) =>
        _catalog.Find(projectId, basePath).Bind(tiles =>
            Reuse(projectId, tiles) switch
            {
                Some<OpenProject> same => new Ok<OpenProject>(same.Value),
                None => Replace(projectId, tiles),
            });

    private Option<OpenProject> Reuse(string projectId, TileSet tiles) =>
        _current switch
        {
            Some<OpenProject> open when open.Value.Serves(projectId, tiles) => open,
            Some<OpenProject> => None.Instance,
            None => None.Instance,
        };

    // The old project is closed before the new one is built, and stays closed
    // if the build fails: a failed SET_PROJECT must never leave the previous
    // project answering SAMPLE requests as if it were the one asked for.
    private Result<OpenProject> Replace(string projectId, TileSet tiles)
    {
        Close();
        var opened = _rasters.OpenMosaic(projectId, [.. tiles.Tiles.Select(t => t.Path)])
            .Map(raster => new OpenProject(projectId, tiles, raster));
        _current = opened switch
        {
            Ok<OpenProject> ok => new Some<OpenProject>(ok.Value),
            Fault => None.Instance,
        };
        return opened;
    }

    private void Close()
    {
        _current.Switch(open => open.Dispose(), () => { });
        _current = None.Instance;
    }

    public void Dispose() => Close();
}
