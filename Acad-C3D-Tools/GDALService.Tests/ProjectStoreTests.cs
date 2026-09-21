using GDALService.Common;
using GDALService.Project;
using GDALService.Terrain;
using GDALService.Tests.Fakes;

namespace GDALService.Tests;

// When SET_PROJECT reuses the open raster, when it replaces it, and what a
// failed replacement leaves behind - over a fake catalog and fake rasters, so
// every outcome is one the test chooses.
public class ProjectStoreTests
{
    private const string Base = @"C:\Projects\A";

    private static readonly TileSet Two = FakeTileCatalog.Tiles(Base, ("TST_1.tif", 100), ("TST_2.tif", 100));

    private static FakeRaster Raster() => new(20, 10, (_, _) => 1, None.Instance);

    private static FakeRasterFactory NewRasterEachTime() => new((_, _) => new Ok<IRaster>(Raster()));

    [Fact]
    public void Nothing_is_open_until_a_project_is()
    {
        using var store = new ProjectStore(FakeTileCatalog.Serving(Two), NewRasterEachTime());
        Assert.Equal(FaultKind.NotInitialized, Expect.Fault(store.Current).Kind);
    }

    [Fact]
    public void Opening_builds_one_mosaic_of_the_catalogs_tiles_named_after_the_project()
    {
        var rasters = NewRasterEachTime();
        using var store = new ProjectStore(FakeTileCatalog.Serving(Two), rasters);

        var project = Expect.Ok(store.Open("TST", Base));

        var (name, tiles) = Assert.Single(rasters.Opened);
        Assert.Equal("TST", name);
        Assert.Equal(Two.Tiles.Select(t => t.Path), tiles);
        Assert.Same(project, Expect.Ok(store.Current));
    }

    [Fact]
    public void The_same_project_over_the_same_tiles_is_reused_whatever_the_case_of_its_id()
    {
        var rasters = NewRasterEachTime();
        using var store = new ProjectStore(FakeTileCatalog.Serving(Two), rasters);

        var first = Expect.Ok(store.Open("TST", Base));
        var again = Expect.Ok(store.Open("tst", Base));

        Assert.Same(first, again);
        Assert.Single(rasters.Opened);
    }

    [Fact]
    public void Another_folder_a_changed_tile_or_another_project_each_replace_the_open_raster()
    {
        const string other = @"C:\Projects\B";
        var catalog = FakeTileCatalog.Serving(Two);
        var rasters = NewRasterEachTime();
        using var store = new ProjectStore(catalog, rasters);
        var first = Expect.Ok(store.Open("TST", Base));

        catalog.Answer = (_, _) => new Ok<TileSet>(FakeTileCatalog.Tiles(other, ("TST_1.tif", 100), ("TST_2.tif", 100)));
        var otherFolder = Expect.Ok(store.Open("TST", other));
        catalog.Answer = (_, _) => new Ok<TileSet>(FakeTileCatalog.Tiles(other, ("TST_1.tif", 100), ("TST_2.tif", 101)));
        var changedTile = Expect.Ok(store.Open("TST", other));
        var otherProject = Expect.Ok(store.Open("OTHER", other));

        Assert.Equal(4, rasters.Opened.Count);
        Assert.Equal(4, new[] { first, otherFolder, changedTile, otherProject }.Distinct().Count());
    }

    [Fact]
    public void A_replaced_raster_is_disposed_once_and_the_last_one_with_the_store()
    {
        var first = Raster();
        var second = Raster();
        var queue = new Queue<FakeRaster>([first, second]);
        var store = new ProjectStore(FakeTileCatalog.Serving(Two), new FakeRasterFactory((_, _) => new Ok<IRaster>(queue.Dequeue())));

        Expect.Ok(store.Open("TST", Base));
        Expect.Ok(store.Open("OTHER", Base));
        Assert.Equal((1, 0), (first.DisposeCount, second.DisposeCount));

        store.Dispose();
        Assert.Equal((1, 1), (first.DisposeCount, second.DisposeCount));
    }

    [Fact]
    public void A_failed_switch_closes_the_previous_project_rather_than_leaving_it_answering()
    {
        var first = Raster();
        var rasters = new FakeRasterFactory((name, _) => name == "TST"
            ? new Ok<IRaster>(first)
            : new Fault(FaultKind.Gdal, "GDALBuildVRT failed"));
        using var store = new ProjectStore(FakeTileCatalog.Serving(Two), rasters);
        Expect.Ok(store.Open("TST", Base));

        var fault = Expect.Fault(store.Open("BAD", Base));

        Assert.Equal("GDALBuildVRT failed", fault.Message);
        Assert.Equal(FaultKind.NotInitialized, Expect.Fault(store.Current).Kind);
        Assert.Equal(1, first.DisposeCount);
    }

    [Fact]
    public void A_backend_that_can_open_nothing_is_the_answer_before_any_tile_is_looked_for()
    {
        var asked = 0;
        var catalog = new FakeTileCatalog((_, _) =>
        {
            asked++;
            return new Fault(FaultKind.NotFound, "Elevations folder not found");
        });
        var rasters = new FakeRasterFactory((_, _) => new Ok<IRaster>(Raster()))
        {
            Unavailable = new Some<Fault>(new Fault(FaultKind.Gdal, "GDAL native libraries are not usable")),
        };
        using var store = new ProjectStore(catalog, rasters);

        var fault = Expect.Fault(store.Open("TST", @"C:\nowhere"));

        Assert.Equal((FaultKind.Gdal, "GDAL native libraries are not usable"), (fault.Kind, fault.Message));
        Assert.Equal(0, asked);
        Assert.Empty(rasters.Opened);
    }

    [Fact]
    public void A_catalog_fault_is_the_answer_and_nothing_is_opened()
    {
        var rasters = NewRasterEachTime();
        var catalog = new FakeTileCatalog((_, _) => new Fault(FaultKind.NotFound, "Elevations folder not found: X"));
        using var store = new ProjectStore(catalog, rasters);

        Assert.Equal("Elevations folder not found: X", Expect.Fault(store.Open("TST", Base)).Message);
        Assert.Empty(rasters.Opened);
    }
}
