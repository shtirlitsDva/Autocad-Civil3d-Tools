using GDALService.Common;
using GDALService.Domain;
using GDALService.Hosting;
using GDALService.Project;
using GDALService.Raster;

namespace GDALService.Tests;

public sealed class ProjectStoreTests : IDisposable
{
    private readonly TileFolder _folder = new();
    private readonly ProjectStore _store = new(TextWriter.Null);

    public void Dispose()
    {
        _store.Dispose();
        _folder.Dispose();
    }

    private static Sample HeightAt(OpenProject project, double x, double y) =>
        Sampler.Points(project.Raster, [new PointQuery(1, 0, 0, x, y)], new Progress("t", 1, TextWriter.Null))[0].Sample;

    [Fact]
    public void Nothing_is_open_until_set_project()
    {
        var fault = Expect.Fault(_store.Current);
        Assert.Equal(FaultKind.NotInitialized, fault.Kind);
    }

    [Fact]
    public void Opening_builds_an_in_memory_mosaic_of_the_numbered_tiles_only()
    {
        var project = Expect.Ok(_store.Open("TST", _folder.BasePath));

        Assert.StartsWith("/vsimem/gdalservice/TST-", project.Raster.Path);
        Assert.Equal((20, 10), (project.Raster.Width, project.Raster.Height));
        Assert.Equal(["TST_1.tif", "TST_2.tif"], project.Tiles.Select(t => Path.GetFileName(t.Path)));
        Assert.False(File.Exists(Path.Combine(_folder.ElevationsDir, "TST.vrt")));
        Assert.Same(project, Expect.Ok(_store.Current));
    }

    [Fact]
    public void The_same_project_from_the_same_folder_is_reused()
    {
        var first = Expect.Ok(_store.Open("TST", _folder.BasePath));
        var again = Expect.Ok(_store.Open("tst", _folder.BasePath + Path.DirectorySeparatorChar));
        Assert.Same(first, again);
    }

    [Fact]
    public void The_same_project_id_from_another_folder_is_not_reused()
    {
        using var other = new TileFolder("Other");
        // Same tile names and georeference, every height 500 higher.
        other.WriteTstTile(1, 0, (c, r) => (float)TileFolder.Expected(c, r) + 500);
        other.WriteTstTile(2, 1, (c, r) => (float)TileFolder.Expected(c + 10, r) + 500);

        var first = Expect.Ok(_store.Open("TST", _folder.BasePath));
        var second = Expect.Ok(_store.Open("TST", other.BasePath));

        Assert.NotSame(first, second);
        var (x, y) = TileFolder.CentreOf(0, 0);
        Assert.Equal(TileFolder.Expected(0, 0) + 500, Assert.IsType<Elevation>(HeightAt(second, x, y).Value).Metres, 4);
    }

    [Fact]
    public void A_tile_added_after_opening_is_picked_up_by_the_next_set_project()
    {
        var first = Expect.Ok(_store.Open("TST", _folder.BasePath));
        TileFolder.WriteTile(Path.Combine(_folder.ElevationsDir, "TST_3.tif"), 1020, 2010, 10, 10, -9999f, (_, _) => 1f);

        var second = Expect.Ok(_store.Open("TST", _folder.BasePath));

        Assert.NotSame(first, second);
        Assert.Equal(30, second.Raster.Width);
    }

    [Fact]
    public void A_tile_removed_after_opening_is_noticed_by_the_next_set_project()
    {
        Expect.Ok(_store.Open("TST", _folder.BasePath));
        File.Delete(Path.Combine(_folder.ElevationsDir, "TST_2.tif"));

        var second = Expect.Ok(_store.Open("TST", _folder.BasePath));

        Assert.Equal(10, second.Raster.Width);
    }

    [Fact]
    public void A_folder_without_elevations_is_not_found()
    {
        var fault = Expect.Fault(_store.Open("TST", Path.Combine(_folder.BasePath, "nope")));
        Assert.Equal(FaultKind.NotFound, fault.Kind);
        Assert.StartsWith("Elevations folder not found", fault.Message);
    }

    [Fact]
    public void A_project_id_with_no_tiles_is_not_found()
    {
        var fault = Expect.Fault(_store.Open("MISSING", _folder.BasePath));
        Assert.Equal(FaultKind.NotFound, fault.Kind);
        Assert.StartsWith("No GeoTIFF tiles for 'MISSING'", fault.Message);
    }

    [Fact]
    public void A_failed_switch_closes_the_previous_project_rather_than_leaving_it_answering()
    {
        Expect.Ok(_store.Open("TST", _folder.BasePath));
        // A numbered tile that is not a raster: the mosaic cannot be built.
        File.WriteAllText(Path.Combine(_folder.ElevationsDir, "BAD_1.tif"), "not a tiff");

        Expect.Fault(_store.Open("BAD", _folder.BasePath));

        Assert.Equal(FaultKind.NotInitialized, Expect.Fault(_store.Current).Kind);
    }
}
