using GDALService.Common;
using GDALService.Raster;

namespace GDALService.Project;

// A tile set opened for sampling: which project, from where, built from exactly
// which tiles, and the raster over them.
internal sealed class OpenProject : IDisposable
{
    public string ProjectId { get; }
    public string BasePath { get; }
    public string ElevationsDir { get; }
    public IReadOnlyList<TileFile> Tiles { get; }
    public OpenRaster Raster { get; }

    public OpenProject(string projectId, string basePath, string elevationsDir,
                       IReadOnlyList<TileFile> tiles, OpenRaster raster)
    {
        ProjectId = projectId;
        BasePath = basePath;
        ElevationsDir = elevationsDir;
        Tiles = tiles;
        Raster = raster;
    }

    // The same project, from the same folder, over byte-for-byte the same tiles.
    public bool Serves(string projectId, string basePath, IReadOnlyList<TileFile> tiles) =>
        string.Equals(ProjectId, projectId, StringComparison.OrdinalIgnoreCase)
        && string.Equals(BasePath, basePath, StringComparison.OrdinalIgnoreCase)
        && Tiles.SequenceEqual(tiles);

    public void Dispose() => Raster.Dispose();
}

// The one project the service is serving, if any. The request loop is single
// threaded, so this holds no lock.
//
// The VRT mosaic lives in GDAL's in-memory file system, never in the tile
// folder: the terrain download writes its own <BASE>.vrt there, and several
// service processes may point at the same shared folder. Each build gets a name
// unique within the process (the in-memory file system is process-wide),
// because a thread-safe dataset reopens its file by name per thread.
internal sealed class ProjectStore : IDisposable
{
    private static int s_builds;

    private readonly TextWriter _log;
    private Option<OpenProject> _current = None.Instance;

    public ProjectStore(TextWriter log) { _log = log; }

    public Result<OpenProject> Current => _current switch
    {
        Some<OpenProject> open => new Ok<OpenProject>(open.Value),
        None => new Fault(FaultKind.NotInitialized, "No project initialized. Call SET_PROJECT first always!"),
    };

    public Result<OpenProject> Open(string projectId, string basePath) =>
        FileSystemEdge.FullPath(basePath).Bind(fullBase =>
        {
            var elevationsDir = Path.Combine(fullBase, "Elevations");
            return FileSystemEdge.ListTiles(elevationsDir, projectId).Bind(tiles =>
                tiles.Count == 0
                    ? new Fault(FaultKind.NotFound, $"No GeoTIFF tiles for '{projectId}' in {elevationsDir}")
                    : Reuse(projectId, fullBase, tiles) switch
                    {
                        Some<OpenProject> same => new Ok<OpenProject>(same.Value),
                        None => Replace(projectId, fullBase, elevationsDir, tiles),
                    });
        });

    private Option<OpenProject> Reuse(string projectId, string fullBase, IReadOnlyList<TileFile> tiles) =>
        _current switch
        {
            Some<OpenProject> open when open.Value.Serves(projectId, fullBase, tiles) => open,
            Some<OpenProject> => None.Instance,
            None => None.Instance,
        };

    // The old project is closed before the new one is built, and stays closed
    // if the build fails: a failed SET_PROJECT must never leave the previous
    // project answering SAMPLE requests as if it were the one asked for.
    private Result<OpenProject> Replace(string projectId, string fullBase, string elevationsDir,
                                        IReadOnlyList<TileFile> tiles)
    {
        Close();
        var vrtPath = $"/vsimem/gdalservice/{projectId}-{Interlocked.Increment(ref s_builds)}.vrt";
        var opened = GdalEdge.BuildVrt(vrtPath, [.. tiles.Select(t => t.Path)])
            .Bind(built => OpenRaster.Open(built) switch
            {
                Ok<OpenRaster> raster => raster,
                Fault fault => ReleaseAndFail(built, fault),
            })
            .Map(raster => new OpenProject(projectId, fullBase, elevationsDir, tiles, raster));
        _current = opened switch
        {
            Ok<OpenProject> ok => new Some<OpenProject>(ok.Value),
            Fault => None.Instance,
        };
        return opened;
    }

    private Result<OpenRaster> ReleaseAndFail(string vrtPath, Fault fault)
    {
        Release(vrtPath);
        return fault;
    }

    private void Close()
    {
        var close = _current switch
        {
            Some<OpenProject> open => (Action)(() =>
            {
                open.Value.Dispose();
                Release(open.Value.Raster.Path);
            }),
            None => () => { },
        };
        close();
        _current = None.Instance;
    }

    // An in-memory VRT that cannot be released only costs memory; it is logged,
    // not treated as a failure of the request that replaced it.
    private void Release(string vrtPath)
    {
        var report = GdalEdge.Unlink(vrtPath) switch
        {
            Ok<string> => (Action)(() => { }),
            Fault fault => () => _log.WriteLine("WARN " + fault.Message),
        };
        report();
    }

    public void Dispose() => Close();
}
