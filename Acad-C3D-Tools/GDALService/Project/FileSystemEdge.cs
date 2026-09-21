using System.Security;
using System.Text.RegularExpressions;

using GDALService.Common;

namespace GDALService.Project;

// One tile as it is on disk now. Two listings with equal tiles describe the same
// terrain, which is what lets SET_PROJECT reuse an open raster.
internal sealed record TileFile(string Path, long Length, DateTime LastWriteUtc);

// The boundary to the file system, which reports a bad path, a vanished network
// share or a denied folder by throwing. This file catches those and nothing else
// does.
internal static class FileSystemEdge
{
    public static Result<string> FullPath(string path)
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
    public static Result<IReadOnlyList<TileFile>> ListTiles(string elevationsDir, string projectId)
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
                new DirectoryInfo(elevationsDir)
                    .EnumerateFiles("*.tif", SearchOption.TopDirectoryOnly)
                    .Where(f => tileName.IsMatch(f.Name))
                    .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(f => new TileFile(f.FullName, f.Length, f.LastWriteTimeUtc))
                    .ToList());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            return new Fault(FaultKind.NotFound, $"cannot list {elevationsDir}: {ex.Message}");
        }
    }
}
