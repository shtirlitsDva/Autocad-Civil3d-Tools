using IntersectUtilities.LongitudinalProfiles.Quality;
using Norsyn.AecPropertySets;

public sealed class ProfileQaLevelsTests
{
    private static QaPipeSpan Pipe(double start, double end, double diameter) => new(start, end, 50, "AluPex", "Twin", "S2", diameter);

    [Fact]
    public void DepthIsVerticalAtTheCrossingStationAndUsesHalfCasingDiameter()
    {
        var surface = new[] { QaSegment.Line(new(0, 10), new(10, 20), 1) };
        var centreline = new[] { QaSegment.Line(new(0, 8), new(10, 8), 1) };
        var terrain = QaLevels.ElevationAt(surface, 5);
        var dhTop = QaLevels.CasingTopAt(centreline, new[] { Pipe(0, 10, 0.2) }, 5);
        Assert.Equal(6.9, Required(QaLevels.Depth(terrain, dhTop)), 10);
        Assert.Equal(0.5, Required(QaLevels.Depth(terrain, Option<double>.Of(14.5))), 10);
    }

    [Fact]
    public void SizeTransitionUsesTheHigherCasingTop()
    {
        var centreline = new[] { QaSegment.Line(new(0, 10), new(10, 10), 1) };
        var sizes = new[] { Pipe(0, 5, 0.2), Pipe(5, 10, 0.4) };
        Assert.Equal(10.1, Required(QaLevels.CasingTopAt(centreline, sizes, 4)), 10);
        Assert.Equal(10.2, Required(QaLevels.CasingTopAt(centreline, sizes, 5)), 10);
        Assert.Equal(10.2, Required(QaLevels.CasingTopAt(centreline, sizes, 6)), 10);
    }

    [Fact]
    public void MissingDataDoesNotProduceAFabricatedDepth()
    {
        var centreline = new[] { QaSegment.Line(new(0, 10), new(4, 10), 1), QaSegment.Line(new(6, 10), new(10, 10), 2) };
        Assert.True(IsMissing(QaLevels.ElevationAt(centreline, 5)));
        Assert.True(IsMissing(QaLevels.CasingTopAt(centreline, new[] { Pipe(0, 10, 0.2) }, 5)));
        Assert.True(IsMissing(QaLevels.CasingTopAt(centreline, Array.Empty<QaPipeSpan>(), 2)));
        Assert.True(IsMissing(QaLevels.Depth(Option<double>.Of(12), Option<double>.Nothing)));
        Assert.True(IsMissing(QaLevels.Depth(Option<double>.Nothing, Option<double>.Of(10))));
    }

    [Fact]
    public void AboveGroundCasingHasNegativeDepth()
    {
        Assert.Equal(-0.25, Required(QaLevels.Depth(Option<double>.Of(10), Option<double>.Of(10.25))), 10);
    }

    [Fact]
    public void BoundaryRoundoffDoesNotExtrapolateTheSurface()
    {
        var surface = new[] { QaSegment.Line(new(0, 10), new(10, 20), 1) };
        Assert.Equal(20, Required(QaLevels.ElevationAt(surface, 10 + QaGeometry.StationTolerance / 2)), 10);
        Assert.True(IsMissing(QaLevels.ElevationAt(surface, 10 + QaGeometry.StationTolerance * 2)));
    }

    private static double Required(Option<double> value) => value.Match(number => number, () => throw new InvalidOperationException("Expected a level."));
    private static bool IsMissing(Option<double> value) => value.Match(_ => false, () => true);
}
