using GDALService.Common;
using GDALService.Domain;
using GDALService.Raster;

using OSGeo.GDAL;

namespace GDALService.Tests;

public class SampleClassifyTests
{
    private static readonly Option<double> NoneDeclared = None.Instance;

    [Fact]
    public void A_finite_pixel_is_an_elevation() =>
        Assert.Equal(new Elevation(12.25), Sample.Classify(12.25, NoneDeclared).Value);

    [Fact]
    public void The_declared_nodata_value_is_nodata() =>
        Assert.IsType<NoData>(Sample.Classify(-9999, new Some<double>(-9999)).Value);

    [Fact]
    public void Another_value_next_to_a_declared_nodata_is_an_elevation() =>
        Assert.IsType<Elevation>(Sample.Classify(-9998, new Some<double>(-9999)).Value);

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void A_non_finite_pixel_is_nodata_whether_or_not_it_was_declared(double pixel)
    {
        Assert.IsType<NoData>(Sample.Classify(pixel, NoneDeclared).Value);
        Assert.IsType<NoData>(Sample.Classify(pixel, new Some<double>(double.NaN)).Value);
        Assert.IsType<NoData>(Sample.Classify(pixel, new Some<double>(-9999)).Value);
    }

    [Fact]
    public void Wire_status_names_are_the_old_ones()
    {
        Sample[] all = [new Elevation(1), NoData.Instance, Outside.Instance, new ReadFailed("r")];
        Assert.Equal(["OK", "NODATA", "OUTSIDE", "ERR"], all.Select(s => s.WireStatus));
    }
}

public class GeoTransformTests
{
    [Fact]
    public void Inversion_matches_GDALs_own()
    {
        GdalForTests.Ensure();
        double[] gt = [685500.06, 0.4, 0.05, 6171304.06, -0.03, -0.4];
        var expected = new double[6];
        Assert.Equal(1, Gdal.InvGeoTransform(gt, expected));

        var inverse = Expect.Some(GeoTransform.FromArray(gt).Invert());

        double[] actual = [inverse.C0, inverse.C1, inverse.C2, inverse.C3, inverse.C4, inverse.C5];
        for (int i = 0; i < 6; i++) { Assert.Equal(expected[i], actual[i], 6); }
    }

    [Fact]
    public void A_point_round_trips_through_the_inverse()
    {
        var gt = new GeoTransform(1000, 1, 0, 2010, 0, -1);
        var (u, v) = Expect.Some(gt.Invert()).Apply(1003.5, 2006.5);
        Assert.Equal((3.5, 3.5), (u, v));
    }

    [Fact]
    public void A_singular_transform_has_no_inverse() =>
        Assert.IsType<None>(new GeoTransform(0, 1, 2, 0, 2, 4).Invert().Value);
}
