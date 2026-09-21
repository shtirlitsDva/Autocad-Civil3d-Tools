using GDALService.Common;

namespace GDALService.Domain;

// What one plan point turned out to be. Only an Elevation carries a number, and
// that number is always finite: there is no "elevation of NaN" to serialise.

internal sealed record Elevation(double Metres);

internal sealed record NoData
{
    public static readonly NoData Instance = new();
    private NoData() { }
}

internal sealed record Outside
{
    public static readonly Outside Instance = new();
    private Outside() { }
}

internal sealed record ReadFailed(string Reason);

internal union Sample(Elevation, NoData, Outside, ReadFailed)
{
    // The one place a raw pixel value becomes a Sample. A NaN pixel is no data
    // whether or not the band declares NaN as its NoData value, and so is an
    // infinity: neither is a ground level.
    public static Sample Classify(double pixel, Option<double> noData)
    {
        if (!double.IsFinite(pixel)) { return NoData.Instance; }
        return noData switch
        {
            Some<double> declared when pixel == declared.Value => NoData.Instance,
            Some<double> => new Elevation(pixel),
            None => new Elevation(pixel),
        };
    }

    // The wire's names for the four cases, unchanged from the old protocol.
    public string WireStatus => this switch
    {
        Elevation => "OK",
        NoData => "NODATA",
        Outside => "OUTSIDE",
        ReadFailed => "ERR",
    };

    // Only an Elevation has a height; the wire writes one only when there is one.
    public Option<double> Height => this switch
    {
        Elevation elevation => new Some<double>(elevation.Metres),
        NoData => None.Instance,
        Outside => None.Instance,
        ReadFailed => None.Instance,
    };
}

internal sealed record PointQuery(long GeomId, int Seq, double S, double X, double Y);

internal sealed record SampledPoint(PointQuery Query, Sample Sample);

internal sealed record GridSample(double X, double Y, Sample Sample);

// The counts are named *Count so they do not shadow the Outside/NoData case
// types inside the switch below.
internal sealed record SampleSummary(int Total, int OkCount, int OutsideCount, int NoDataCount, int ErrCount)
{
    public static readonly SampleSummary Empty = new(0, 0, 0, 0, 0);

    // A switch expression, not a switch statement: only the expression form is
    // checked for exhaustiveness, so a fifth Sample case fails the build here.
    public SampleSummary Add(Sample sample) => sample switch
    {
        Elevation => this with { Total = Total + 1, OkCount = OkCount + 1 },
        Outside => this with { Total = Total + 1, OutsideCount = OutsideCount + 1 },
        NoData => this with { Total = Total + 1, NoDataCount = NoDataCount + 1 },
        ReadFailed => this with { Total = Total + 1, ErrCount = ErrCount + 1 },
    };

    public static SampleSummary Of(IEnumerable<Sample> samples) => samples.Aggregate(Empty, (sum, s) => sum.Add(s));
}
