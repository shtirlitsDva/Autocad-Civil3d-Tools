using GDALService.Common;
using GDALService.Project;

namespace GDALService.Tests;

// The file-system edge against a real tile folder.
public sealed class FileSystemTileCatalogTests : IDisposable
{
    private readonly TileFolder _folder = new();
    private readonly FileSystemTileCatalog _catalog = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void Only_numbered_tiles_of_the_project_are_found_in_name_order()
    {
        var tiles = Expect.Ok(_catalog.Find("TST", _folder.BasePath));

        Assert.Equal(["TST_1.tif", "TST_2.tif"], tiles.Tiles.Select(t => Path.GetFileName(t.Path)));
        Assert.Equal(_folder.ElevationsDir, tiles.ElevationsDir);
        Assert.All(tiles.Tiles, t => Assert.True(t.Length > 0));
    }

    [Fact]
    public void The_base_path_is_made_full_and_loses_its_trailing_separator()
    {
        var tiles = Expect.Ok(_catalog.Find("TST", _folder.BasePath + Path.DirectorySeparatorChar));
        Assert.Equal(_folder.BasePath, tiles.BasePath);
    }

    [Fact]
    public void A_folder_without_elevations_is_not_found()
    {
        var fault = Expect.Fault(_catalog.Find("TST", Path.Combine(_folder.BasePath, "nope")));
        Assert.Equal(FaultKind.NotFound, fault.Kind);
        Assert.StartsWith("Elevations folder not found", fault.Message);
    }

    [Fact]
    public void A_project_id_with_no_tiles_is_not_found()
    {
        var fault = Expect.Fault(_catalog.Find("MISSING", _folder.BasePath));
        Assert.Equal(FaultKind.NotFound, fault.Kind);
        Assert.StartsWith("No GeoTIFF tiles for 'MISSING'", fault.Message);
    }

    [Fact]
    public void An_unusable_path_is_invalid()
    {
        var fault = Expect.Fault(_catalog.Find("TST", "C:\\bad\0path"));
        Assert.Equal(FaultKind.InvalidArgs, fault.Kind);
    }
}
