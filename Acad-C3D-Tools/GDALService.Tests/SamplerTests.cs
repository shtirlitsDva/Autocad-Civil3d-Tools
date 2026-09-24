using GDALService.Common;
using GDALService.Domain;
using GDALService.Terrain;
using GDALService.Tests.Fakes;

namespace GDALService.Tests;

// The sampler against an in-memory raster laid out like TileFolder's TST mosaic:
// 20 x 10 pixels of 1 m from (1000, 2010), pixel (col, row) = 100 + col + row / 2,
// (3, 3) the declared NoData -9999 and (4, 4) an undeclared NaN. No GDAL: every
// read failure is one the test asks for.
public class SamplerTests
{
    private static FakeRaster Tst() =>
        new(20, 10, (c, r) => c == 3 && r == 3 ? -9999 : c == 4 && r == 4 ? double.NaN : TileFolder.Expected(c, r),
            new Some<double>(-9999), TileFolder.OriginX, TileFolder.TopY);

    private static readonly Sampler Default = new(new SamplingOptions());

    private static IReadOnlyList<SampledPoint> Points(IRaster raster, params PointQuery[] points) =>
        Default.Points(raster, points, () => { });

    private static Sample One(IRaster raster, double x, double y) => Points(raster, new PointQuery(1, 0, 0, x, y))[0].Sample;

    [Theory]
    [InlineData(0, 0)]
    [InlineData(9, 9)]
    [InlineData(10, 0)]
    [InlineData(19, 9)]
    public void A_point_inside_gets_its_pixels_height(int col, int row)
    {
        var (x, y) = TileFolder.CentreOf(col, row);
        Assert.Equal(new Elevation(TileFolder.Expected(col, row)), One(Tst(), x, y).Value);
    }

    [Theory]
    [InlineData(999.99, 2005)]    // west
    [InlineData(1020.01, 2005)]   // east
    [InlineData(1010, 2010.01)]   // north
    [InlineData(1010, 1999.99)]   // south
    [InlineData(700000, 6000000)] // far away: the #175 point
    public void A_point_off_the_raster_is_outside(double x, double y) =>
        Assert.IsType<Outside>(One(Tst(), x, y).Value);

    [Fact]
    public void The_min_edges_are_inside_and_the_max_edges_are_outside()
    {
        var tst = Tst();
        Assert.IsType<Elevation>(One(tst, TileFolder.OriginX, TileFolder.TopY).Value);
        Assert.IsType<Outside>(One(tst, TileFolder.OriginX + 20, 2005).Value);
        Assert.IsType<Outside>(One(tst, 1005, TileFolder.TopY - 10).Value);
    }

    [Fact]
    public void The_declared_nodata_and_an_undeclared_nan_are_nodata()
    {
        var tst = Tst();
        var (x3, y3) = TileFolder.CentreOf(3, 3);
        var (x4, y4) = TileFolder.CentreOf(4, 4);
        Assert.IsType<NoData>(One(tst, x3, y3).Value);
        Assert.IsType<NoData>(One(tst, x4, y4).Value);
    }

    [Fact]
    public void A_failed_pixel_read_is_an_err_row_and_the_rest_of_the_batch_still_samples()
    {
        var tst = Tst().FailAt(2, 1);
        var (bad, good) = (TileFolder.CentreOf(2, 1), TileFolder.CentreOf(5, 5));

        var rows = Points(tst, new PointQuery(1, 0, 0, bad.X, bad.Y), new PointQuery(1, 1, 0, good.X, good.Y));

        Assert.Equal(new ReadFailed("raster read failed at pixel (2, 1)"), rows[0].Sample.Value);
        Assert.Equal(new Elevation(TileFolder.Expected(5, 5)), rows[1].Sample.Value);
    }

    [Fact]
    public void A_worker_whose_reader_cannot_open_answers_its_points_as_read_failures()
    {
        var broken = new FakeRaster(20, 10, (_, _) => 1, None.Instance, TileFolder.OriginX, TileFolder.TopY) { ReadersFail = true };
        var (x, y) = TileFolder.CentreOf(1, 1);

        Assert.Equal(new ReadFailed("the raster has no band 1"), One(broken, x, y).Value);
    }

    [Fact]
    public void Rows_come_back_in_request_order_and_every_point_reports_progress()
    {
        var queries = Enumerable.Range(0, 200)
            .Select(i => new PointQuery(5, 199 - i, i, 1000.5 + i % 20, 2000.5 + i % 10))
            .ToArray();
        int sampled = 0;

        var rows = Default.Points(Tst(), queries, () => Interlocked.Increment(ref sampled));

        Assert.Equal(queries, rows.Select(r => r.Query));
        Assert.Equal(200, sampled);
    }

    [Fact]
    public void Each_worker_opens_its_own_reader_and_no_more_than_the_options_allow()
    {
        var tst = Tst();
        var points = Enumerable.Range(0, 2000).Select(i => new PointQuery(1, i, 0, 1000.5, 2000.5)).ToArray();

        new Sampler(new SamplingOptions(Workers: 2)).Points(tst, points, () => { });

        Assert.InRange(tst.ReadersOpened, 1, 2);
    }

    [Fact]
    public void A_grid_covers_the_raster_row_major_from_the_south_west()
    {
        int total = -1;
        var cells = Expect.Ok(Default.Grid(Tst(), 5, t => { total = t; return () => { }; }));

        // x 1000..1020 and y 2000..2010 at 5 m: 5 columns x 3 rows.
        Assert.Equal(15, cells.Count);
        Assert.Equal(15, total);
        Assert.Equal((1000.0, 2000.0), (cells[0].X, cells[0].Y));
        Assert.Equal((1005.0, 2000.0), (cells[1].X, cells[1].Y));
        Assert.Equal((1000.0, 2005.0), (cells[5].X, cells[5].Y));
        Assert.IsType<Outside>(cells[4].Sample.Value);   // x = 1020 is the east edge
    }

    [Fact]
    public void A_grid_larger_than_the_options_allow_is_refused_before_anything_is_sampled()
    {
        var tst = Tst();
        // 20 x 10 m at 5 m is 15 points; allow 14.
        var fault = Expect.Fault(new Sampler(new SamplingOptions(MaxGridPoints: 14)).Grid(tst, 5, _ => () => { }));

        Assert.Equal(FaultKind.InvalidArgs, fault.Kind);
        Assert.Equal("gridDist 5 m asks for 15 points; at most 14 are allowed", fault.Message);
        Assert.Equal(0, tst.ReadersOpened);
    }

    [Fact]
    public void The_default_grid_limit_is_five_million_points()
    {
        // Both edges are grid lines: (20 / 0.001 + 1) x (10 / 0.001 + 1).
        var fault = Expect.Fault(Default.Grid(Tst(), 0.001, _ => () => { }));
        Assert.Equal("gridDist 0.001 m asks for 200030001 points; at most 5000000 are allowed", fault.Message);
    }
}
