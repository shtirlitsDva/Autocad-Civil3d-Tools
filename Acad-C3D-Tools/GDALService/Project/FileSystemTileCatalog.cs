using System.Security;
using System.Text.RegularExpressions;

using GDALService.Common;

namespace GDALService.Project;

// The boundary to the file system, which reports a bad path, a vanished network
// share or a denied folder by throwing. This file catches those and nothing else
// does.
internal sealed class FileSystemTileCatalog : ITileCatalog
{
    public Result<TileSet> Find(string projectId, string basePath) =>
        FullPath(basePath).Bind(fullBase =>
        {
            var elevationsDir = Path.Combine(fullBase, "Elevations");
            return ListTiles(elevationsDir, projectId).Bind(tiles =>
                tiles.Count == 0
                    ? (Result<TileSet>)new Fault(FaultKind.NotFound, $"No GeoTIFF tiles for '{projectId}' in {elevationsDir}")
                    : new Ok<TileSet>(new TileSet(fullBase, elevationsDir, tiles)));
        });

    private static Result<string> FullPath(string path)
    {
        try
        {
            return new Ok<string>(Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException
                                   or PathTooLongException or SecurityException)
        {
            return new Fault(FaultKind.InvalidArgs, $"basePath '{path}' is not a usable path: {ex.Message}");
        }
    }

    // The tiles of one set: `<projectId>_<n>.tif`, where n is digits - the same
    // rule the terrain download and the NorsynDrawingTools client use. Ordered
    // by name so the mosaic is built the same way every time.
    private static Result<IReadOnlyList<TileFile>> ListTiles(string elevationsDir, string projectId)
    {
        if (!Directory.Exists(elevationsDir))
        {
            return new Fault(FaultKind.NotFound, "Elevations folder not found: " + elevationsDir);
        }

        var tileName = new Regex("^" + Regex.Escape(projectId) + @"_\d+\.tif$",
                                 RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        try
        {
            return new Ok<IReadOnlyList<TileFile>>(
            [
                .. new DirectoryInfo(elevationsDir)
                    .EnumerateFiles("*.tif", SearchOption.TopDirectoryOnly)
                    .Where(f => tileName.IsMatch(f.Name))
                    .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(f => new TileFile(f.FullName, f.Length, f.LastWriteTimeUtc)),
            ]);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            return new Fault(FaultKind.NotFound, $"cannot list {elevationsDir}: {ex.Message}");
        }
    }
}
