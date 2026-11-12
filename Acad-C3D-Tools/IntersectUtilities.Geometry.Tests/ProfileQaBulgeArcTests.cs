using IntersectUtilities.LongitudinalProfiles.Quality;

public sealed class ProfileQaBulgeArcTests
{
    private static QaBulgeArc Arc(QaPoint start, QaPoint end, double bulge) => QaBulgeArc.FromEndpoints(start, end, bulge)
        .Match(arc => arc, message => throw new InvalidOperationException(message));

    [Theory]
    [InlineData(724995.3674188775, 6190974.217191134, 724994.471718597, 6190974.2147451425, -0.0013654044057214204, 164)]
    [InlineData(725043.6832320191, 6190974.478630063, 725042.7875317385, 6190974.476184072, 0.0013654044057209763, 164)]
    [InlineData(726053.5527255038, 6190858.961336034, 726053.8178172944, 6190858.953555331, -0.000656450658053141, 101)]
    public void ShortArcsFromProfiles002And003KeepTheirDesignedRadius(double x1, double y1, double x2, double y2, double bulge, double expected)
    {
        var start = new QaPoint(x1, y1); var end = new QaPoint(x2, y2);
        var arc = Arc(start, end, bulge);
        Assert.InRange(arc.Radius, expected - 1e-6, expected + 1e-6);
        var curve = QaSegment.Ellipse(Math.Min(x1, x2), Math.Max(x1, x2), arc.Centre.S, arc.Centre.Z, arc.Radius, arc.Radius,
            Math.Sign(Math.Sin(arc.Angle + arc.Sweep / 2)), 1);
        Assert.InRange(Math.Abs(curve.At(x1) - y1), 0, 1e-8);
        Assert.InRange(Math.Abs(curve.At(x2) - y2), 0, 1e-8);
        Assert.InRange(curve.MinimumRadius(curve.Start, curve.End).Radius, expected - 1e-6, expected + 1e-6);
    }

    [Fact]
    public void ReversingArcDirectionPreservesCentreAndRadius()
    {
        var start = new QaPoint(700000, 6000000); var end = new QaPoint(700001, 6000000.1);
        var forward = Arc(start, end, 0.001);
        var backward = Arc(end, start, -0.001);
        Assert.Equal(forward.Radius, backward.Radius, 10);
        Assert.Equal(forward.Centre.S, backward.Centre.S, 8);
        Assert.Equal(forward.Centre.Z, backward.Centre.Z, 8);
        Assert.Equal(forward.Sweep, -backward.Sweep, 10);
    }

    [Theory]
    [InlineData(163.99995, "REVIEW")]
    [InlineData(136.99995, "FAIL")]
    public void GenuineRadiusShortfallIsStillReported(double radius, string expected)
    {
        double bulge = 1 / (2 * radius + Math.Sqrt(4 * radius * radius - 1));
        var arc = Arc(new(0, 0), new(1, 0), bulge);
        var pipe = new QaPipeSpan(0, 1, 250, "Stål", "Enkelt", "S2", 0.45);
        var rule = ProfileQaRadii.Get(pipe, true).Match(value => value, message => throw new InvalidOperationException(message));
        Assert.Equal(expected, ProfileQaRadii.Status(arc.Radius, rule, pipe));
    }
}
