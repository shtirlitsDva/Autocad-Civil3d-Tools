using GDALService.Common;
using GDALService.Domain;
using GDALService.Project;
using GDALService.Terrain;
using GDALService.Terrain.GdalBackend;

using OSGeo.GDAL;

namespace GDALService.Tests;

// The GDAL backend against real GeoTIFFs: the in-memory mosaic, what a real read
// returns, thread-safe reads, and releasing the mosaic. The sampler's own logic
// is tested on fakes in SamplerTests; this is what the fakes stand in for.
public sealed class GdalRasterTests : IDisposable
{
    private readonly TileFolder _folder = new();
    private readonly StringWriter _stderr = new();
    private readonly GdalRasterFactory _factory;
    private static readonly Sampler Sampler = new(new SamplingOptions());

    public GdalRasterTests() { _factory = new GdalRasterFactory(new ServiceLog(_stderr)); }

    public void Dispose() => _folder.Dispose();

    private IRaster Mosaic(string projectId) =>
        Expect.Ok(_factory.OpenMosaic(projectId, [.. Expect.Ok(new FileSystemTileCatalog().Find(projectId, _folder.BasePath)).Tiles.Select(t => t.Path)]));

    private static Sample At(IRaster raster, double x, double y) =>
        Sampler.Points(raster, [new PointQuery(1, 0, 0, x, y)], () => { })[0].Sample;

    [Fact]
    public void The_mosaic_lives_in_memory_and_covers_both_tiles()
    {
        using var raster = Mosaic("TST");

        Assert.StartsWith("/vsimem/gdalservice/TST-", raster.Info.Source);
        Assert.Equal((20, 10, 1), (raster.Info.Width, raster.Info.Height, raster.Info.Bands));
        Assert.Equal(-9999, Expect.Some(raster.Info.NoData));
        Assert.False(File.Exists(Path.Combine(_folder.ElevationsDir, "TST.vrt")));
    }

    [Fact]
    public void Real_reads_give_heights_nodata_and_nan_as_the_tiles_hold_them()
    {
        using var raster = Mosaic("TST");
        var (x, y) = TileFolder.CentreOf(12, 3);
        var (x3, y3) = TileFolder.CentreOf(3, 3);
        var (x4, y4) = TileFolder.CentreOf(4, 4);

        Assert.Equal(new Elevation(TileFolder.Expected(12, 3)), At(raster, x, y).Value);
        Assert.IsType<NoData>(At(raster, x3, y3).Value);
        Assert.IsType<NoData>(At(raster, x4, y4).Value);
    }

    [Fact]
    public void A_band_whose_nodata_is_nan_gives_nodata_for_nan_and_heights_elsewhere()
    {
        using var raster = Mosaic("NAN");
        Assert.True(double.IsNaN(Expect.Some(raster.Info.NoData)));

        Assert.IsType<NoData>(At(raster, 3002.5, 4002.5).Value);
        Assert.Equal(new Elevation(7), At(raster, 3000.5, 4004.5).Value);
    }

    [Fact]
    public void Parallel_reads_of_the_in_memory_mosaic_match_a_one_at_a_time_pass()
    {
        using var raster = Mosaic("TST");
        var queries = Enumerable.Range(0, 5000)
            .Select(i => new PointQuery(1, i, 0, 1000 + (i * 7 % 2000) / 100.0, 2000 + (i * 13 % 1000) / 100.0))
            .ToList();

        var parallel = Sampler.Points(raster, queries, () => { }).Select(r => (object?)r.Sample.Value).ToList();
        var oneByOne = queries.Select(q => (object?)At(raster, q.X, q.Y).Value).ToList();

        Assert.Equal(oneByOne, parallel);
    }

    [Fact]
    public void Disposing_the_raster_releases_its_in_memory_mosaic()
    {
        var raster = Mosaic("TST");
        var source = raster.Info.Source;

        raster.Dispose();

        Assert.Equal(FaultKind.Gdal, Expect.Fault(GdalEdge.OpenThreadSafe(source)).Kind);
        Assert.Equal("", _stderr.ToString());
    }

    [Fact]
    public void A_mosaic_that_cannot_be_released_is_a_warning_not_a_failure()
    {
        var raster = Mosaic("TST");
        Assert.Equal(0, Gdal.Unlink(raster.Info.Source));   // gone before the raster lets go of it

        raster.Dispose();

        Assert.StartsWith("WARN could not release /vsimem/gdalservice/TST-", _stderr.ToString());
    }

    [Fact]
    public void A_tile_that_is_not_a_raster_fails_the_mosaic()
    {
        File.WriteAllText(Path.Combine(_folder.ElevationsDir, "BAD_1.tif"), "not a tiff");

        var fault = Expect.Fault(_factory.OpenMosaic("BAD", [Path.Combine(_folder.ElevationsDir, "BAD_1.tif")]));

        Assert.Equal(FaultKind.Gdal, fault.Kind);
    }

    [Fact]
    public void Gdal_unavailable_refuses_every_mosaic_with_the_reason()
    {
        var why = new Fault(FaultKind.Gdal, "GDAL native libraries are not usable");
        Assert.Same(why, Expect.Fault(new UnavailableRasterFactory(why).OpenMosaic("TST", ["x.tif"])));
    }

    [Fact]
    public void A_project_over_real_tiles_notices_tiles_added_and_removed()
    {
        using var store = new ProjectStore(new FileSystemTileCatalog(), _factory);
        var first = Expect.Ok(store.Open("TST", _folder.BasePath));

        TileFolder.WriteTile(Path.Combine(_folder.ElevationsDir, "TST_3.tif"), 1020, 2010, 10, 10, -9999f, (_, _) => 1f);
        var added = Expect.Ok(store.Open("TST", _folder.BasePath));
        File.Delete(Path.Combine(_folder.ElevationsDir, "TST_3.tif"));
        File.Delete(Path.Combine(_folder.ElevationsDir, "TST_2.tif"));
        var removed = Expect.Ok(store.Open("TST", _folder.BasePath));

        Assert.NotSame(first, added);
        Assert.Equal((30, 10), (added.Raster.Info.Width, removed.Raster.Info.Width));
    }
}
