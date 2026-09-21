using GDALService.Common;
using GDALService.Domain;
using GDALService.Hosting;
using GDALService.Project;
using GDALService.Raster;

namespace GDALService.Tests;

public sealed class SamplerTests : IDisposable
{
    private readonly TileFolder _folder = new();
    private readonly ProjectStore _store = new(TextWriter.Null);
    private readonly OpenRaster _tst;

    public SamplerTests()
    {
        _tst = Expect.Ok(_store.Open("TST", _folder.BasePath)).Raster;
    }

    public void Dispose()
    {
        _store.Dispose();
        _folder.Dispose();
    }

    private static Progress Quiet(int total) => new("t", total, TextWriter.Null);

    private Sample One(double x, double y) =>
        Sampler.Points(_tst, [new PointQuery(1, 0, 0, x, y)], Quiet(1))[0].Sample;

    [Theory]
    [InlineData(0, 0)]
    [InlineData(9, 9)]
    [InlineData(10, 0)]   // first pixel of the second tile
    [InlineData(19, 9)]   // last pixel of the mosaic
    public void A_point_inside_gets_its_pixels_height(int col, int row)
    {
        var (x, y) = TileFolder.CentreOf(col, row);
        var elevation = Assert.IsType<Elevation>(One(x, y).Value);
        Assert.Equal(TileFolder.Expected(col, row), elevation.Metres, 4);
    }

    [Theory]
    [InlineData(999.99, 2005)]    // west
    [InlineData(1020.01, 2005)]   // east
    [InlineData(1010, 2010.01)]   // north
    [InlineData(1010, 1999.99)]   // south
    [InlineData(700000, 6000000)] // far away: the #175 point
    public void A_point_off_the_raster_is_outside(double x, double y) =>
        Assert.IsType<Outside>(One(x, y).Value);

    [Fact]
    public void The_min_edges_are_inside_and_the_max_edges_are_outside()
    {
        Assert.IsType<Elevation>(One(TileFolder.OriginX, TileFolder.TopY).Value);                 // top-left corner
        Assert.IsType<Outside>(One(TileFolder.OriginX + 20, 2005).Value);                        // east edge
        Assert.IsType<Outside>(One(1005, TileFolder.TopY - 10).Value);                           // south edge
    }

    [Fact]
    public void The_declared_nodata_pixel_is_nodata()
    {
        var (x, y) = TileFolder.CentreOf(3, 3);
        Assert.IsType<NoData>(One(x, y).Value);
    }

    [Fact]
    public void An_undeclared_nan_pixel_is_nodata()
    {
        var (x, y) = TileFolder.CentreOf(4, 4);
        Assert.IsType<NoData>(One(x, y).Value);
    }

    [Fact]
    public void A_band_whose_nodata_is_nan_gives_nodata_for_nan_and_heights_elsewhere()
    {
        using var store = new ProjectStore(TextWriter.Null);
        var nan = Expect.Ok(store.Open("NAN", _folder.BasePath)).Raster;
        Assert.True(double.IsNaN(Expect.Some(nan.NoData)));

        var rows = Sampler.Points(nan, [new PointQuery(1, 0, 0, 3002.5, 4002.5), new PointQuery(1, 1, 0, 3000.5, 4004.5)], Quiet(2));

        Assert.IsType<NoData>(rows[0].Sample.Value);
        Assert.Equal(new Elevation(7), rows[1].Sample.Value);
    }

    [Fact]
    public void Rows_come_back_in_request_order_with_their_queries()
    {
        var queries = Enumerable.Range(0, 200)
            .Select(i => new PointQuery(5, 199 - i, i, 1000.5 + i % 20, 2000.5 + i % 10))
            .ToList();

        var rows = Sampler.Points(_tst, queries, Quiet(queries.Count));

        Assert.Equal(queries, rows.Select(r => r.Query));
    }

    [Fact]
    public void Parallel_sampling_of_the_in_memory_mosaic_matches_a_one_at_a_time_pass()
    {
        var queries = Enumerable.Range(0, 5000)
            .Select(i => new PointQuery(1, i, 0, 1000 + (i * 7 % 2000) / 100.0, 2000 + (i * 13 % 1000) / 100.0))
            .ToList();

        // Compared by the case records inside the unions, which have value equality.
        var parallel = Sampler.Points(_tst, queries, Quiet(queries.Count)).Select(r => (object?)r.Sample.Value).ToList();
        var oneByOne = queries.Select(q => (object?)One(q.X, q.Y).Value).ToList();

        Assert.Equal(oneByOne, parallel);
    }

    [Fact]
    public void A_grid_covers_the_raster_row_major_from_the_south_west()
    {
        var cells = Expect.Ok(Sampler.Grid(_tst, 5, total => Quiet(total)));

        // x 1000..1020 and y 2000..2010 at 5 m: 5 columns x 3 rows.
        Assert.Equal(15, cells.Count);
        Assert.Equal((1000.0, 2000.0), (cells[0].X, cells[0].Y));
        Assert.Equal((1005.0, 2000.0), (cells[1].X, cells[1].Y));
        Assert.Equal((1000.0, 2005.0), (cells[5].X, cells[5].Y));
        Assert.IsType<Outside>(cells[4].Sample.Value);   // x = 1020 is the east edge
    }

    [Fact]
    public void A_grid_too_fine_is_refused_before_anything_is_allocated()
    {
        var fault = Expect.Fault(Sampler.Grid(_tst, 0.001, total => Quiet(total)));
        Assert.Equal(FaultKind.InvalidArgs, fault.Kind);
        // Both edges are grid lines: (20 / 0.001 + 1) x (10 / 0.001 + 1).
        Assert.Contains("200030001 points", fault.Message);
    }
}
