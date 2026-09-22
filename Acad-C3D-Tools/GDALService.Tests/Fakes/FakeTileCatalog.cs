using GDALService.Common;
using GDALService.Project;

namespace GDALService.Tests.Fakes;

// A catalog whose answer the test decides - and can change between calls, as
// tiles on disk would.
internal sealed class FakeTileCatalog : ITileCatalog
{
    public Func<string, string, Result<TileSet>> Answer { get; set; }

    public FakeTileCatalog(Func<string, string, Result<TileSet>> answer) { Answer = answer; }

    // A tile set of the given file names under basePath\Elevations, each with
    // the given length (a stand-in for "this file changed").
    public static TileSet Tiles(string basePath, params (string Name, long Length)[] files) =>
        new(basePath, Path.Combine(basePath, "Elevations"),
            [.. files.Select(f => new TileFile(Path.Combine(basePath, "Elevations", f.Name), f.Length, new DateTime(2026, 1, 1)))]);

    public static FakeTileCatalog Serving(TileSet tiles) => new((_, _) => new Ok<TileSet>(tiles));

    public Result<TileSet> Find(string projectId, string basePath) => Answer(projectId, basePath);
}
