using IntersectUtilities.LongitudinalProfiles.Quality;
using Norsyn.AecPropertySets;

public sealed class ProfileQaGeometryTests
{
    private static QaRegion Circle(double s, double z, double radius) => new(new[] { QaEdge.Arc(s, z, radius, radius, 0, 2 * Math.PI) });
    private static QaRadiusLimit Limit(QaPipeSpan pipe, bool vertical) => ProfileQaRadii.Get(pipe, vertical).Match(x => x, message => throw new InvalidOperationException(message));

    [Fact]
    public void CrossingDetectsContainmentAndSmallIntrusionButAllowsTangency()
    {
        var region = Circle(0, 0, 1);
        Assert.Empty(QaGeometry.Intrusions(QaSegment.Line(new(-2, 1), new(2, 1), 1), region, -2, 2));
        Assert.Single(QaGeometry.Intrusions(QaSegment.Line(new(-2, 0.999999), new(2, 0.999999), 1), region, -2, 2));
        var inside = Assert.Single(QaGeometry.Intrusions(QaSegment.Line(new(-0.1, 0), new(0.1, 0), 1), region, -0.1, 0.1));
        Assert.Equal(0.2, inside.Length, 8);
        var through = Assert.Single(QaGeometry.Intrusions(QaSegment.Line(new(-2, 0), new(2, 0), 1), region, -2, 2));
        Assert.Equal(-1, through.Start, 7); Assert.Equal(1, through.End, 7);
    }

    [Fact]
    public void AsymmetricSemicircleOnlyExcludesItsOwnSide()
    {
        var edge = QaEdge.Arc(0, 0, 2, 2, 0, Math.PI);
        var region = new QaRegion(new[] { edge, QaEdge.Line(edge.Q, edge.P) });
        Assert.True(region.Contains(new(0, 1))); Assert.False(region.Contains(new(0, -1))); Assert.False(region.Contains(new(0, 0)));
    }

    [Fact]
    public void CoverFindsInternalCrestMinimumAndExactThresholdBoundaries()
    {
        var crest = QaSegment.Ellipse(-3, 3, 0, -9, 10, 10, 1, 1);
        var terrain = QaSegment.Line(new(-3, 2), new(3, 2), 1);
        var failure = Assert.Single(QaGeometry.Cover(crest, terrain, 0.25, 0.9, -3, 3));
        Assert.Equal(0.75, failure.Minimum, 8); Assert.Equal(0, failure.Station, 8);
        Assert.Equal(0.9, terrain.At(failure.Start) - crest.At(failure.Start) - 0.25, 7);
        Assert.Empty(QaGeometry.Cover(crest, terrain, 0.25, 0.75, -3, 3));
    }

    [Fact]
    public void ParabolicProfileUsesLocalCurvatureAndCover()
    {
        var parabola = QaSegment.Parabola(0, 10, 0, 1, -0.2, 1);
        Assert.Equal(5, parabola.MinimumRadius(0, 10).Radius, 8);
        Assert.Equal(5, parabola.MinimumRadius(0, 10).Station, 8);
        Assert.Equal(1, Assert.Single(QaGeometry.Cover(parabola, QaSegment.Line(new(0, 4), new(10, 4), 1), 0.5, 2, 0, 10)).Minimum, 8);
        Assert.NotEmpty(QaGeometry.Intrusions(parabola, Circle(5, 2.5, 1), 0, 10));
    }

    [Fact]
    public void NonuniformViewScalePreservesEllipticalGeometry()
    {
        var ellipse = QaSegment.Ellipse(-3, 3, 0, -4, 10, 5, 1, 1);
        Assert.Equal(20 * Math.Pow(1 - 0.75 * 0.09, 1.5), ellipse.MinimumRadius(-3, 3).Radius, 8);
        Assert.NotEmpty(QaGeometry.Intrusions(ellipse, Circle(0, 1, 0.5), -3, 3));
    }

    [Fact]
    public void LargeStationValuesDoNotLoseIntersectionPrecision()
    {
        var hit = Assert.Single(QaGeometry.Intrusions(QaSegment.Line(new(700000, 0), new(700010, 0), 1), Circle(700005, 0, 1), 700000, 700010));
        Assert.Equal(700004, hit.Start, 6); Assert.Equal(700006, hit.End, 6);
    }

    [Fact]
    public void OnlyContinuousCoverFailuresMergeAndRetainWorstValue()
    {
        var result = QaGeometry.MergeCover(new[] { new QaCoverFailure(0, 1, 0.4, 0.5), new QaCoverFailure(1, 2, 0.3, 1.5), new QaCoverFailure(3, 4, 0.2, 3.5) });
        Assert.Equal(2, result.Count); Assert.Equal(0.3, result[0].Minimum, 8); Assert.Equal(1.5, result[0].Station, 8);
    }

    [Fact]
    public void ConfirmedSteelAndAlupexColumnsAreNotMultipliedAgain()
    {
        var steel = new QaPipeSpan(0, 10, 50, "Stål", "Twin", "S2", 0.2);
        Assert.Equal(30, Limit(steel, false).Minimum); Assert.Equal(36, Limit(steel, false).Target);
        Assert.Equal(140.6, Limit(steel, true).Minimum); Assert.False(Limit(steel, true).HasTarget);
        var alu = steel with { System = "AluPex", DN = 25 };
        Assert.Equal(1.1, Limit(alu, false).Minimum); Assert.Equal(1.5, Limit(alu, false).Target);
        Assert.Equal(3, Limit(alu, true).Minimum); Assert.False(Limit(alu, true).HasTarget);
        Assert.Equal("REVIEW", ProfileQaRadii.Status(35, Limit(steel, false)));
        Assert.Equal("FAIL", ProfileQaRadii.Status(29, Limit(steel, false)));
    }

    [Fact]
    public void SinglePertAndUnlistedSeriesRemainUnverified()
    {
        var pipe = new QaPipeSpan(0, 10, 25, "PertFlextra", "Enkelt", "S2", 0.2);
        Assert.True(ProfileQaRadii.Get(pipe, true).Match(_ => false, _ => true));
        Assert.True(ProfileQaRadii.Get(pipe with { DN = 63, Type = "Twin" }, true).Match(_ => false, _ => true));
    }

    [Fact]
    public void ShortArcRoundoffDoesNotCreateFalseRadiusFailure()
    {
        var limit = new QaRadiusLimit(5, 0, false);
        Assert.Equal("OK", ProfileQaRadii.Status(4.9999978707895965, limit));
        Assert.Equal("FAIL", ProfileQaRadii.Status(4.99995, limit));
    }

    [Fact]
    public void FiveMetreChecksIncludeExactEndpointAndStayOnStationMultiples()
    {
        var stations = QaGeometry.CoverStations(0, 253.245);
        Assert.Equal(52, stations.Count);
        Assert.Equal(0, stations[0]); Assert.Equal(5, stations[1]);
        Assert.Equal(250, stations[^2]); Assert.Equal(253.245, stations[^1]);
        Assert.Equal(new[] { 2.3, 5.0, 10.0, 13.245 }, QaGeometry.CoverStations(2.3, 13.245));
        Assert.Equal(new[] { 0.0, 5.0, 10.0, 15.0 }, QaGeometry.CoverStations(0, 15));
    }

    [Fact]
    public void WorstCoverIsAvailableEvenWhenNothingFails()
    {
        var crest = QaSegment.Ellipse(-3, 3, 0, -9, 10, 10, 1, 1);
        var terrain = QaSegment.Line(new(-3, 2), new(3, 2), 1);
        var minimum = QaGeometry.MinimumCover(crest, terrain, 0.25, -3, 3);
        Assert.Equal(0.75, minimum.Cover, 8); Assert.Equal(0, minimum.Station, 8);
        Assert.Empty(QaGeometry.Cover(crest, terrain, 0.25, 0.6, -3, 3));
    }

    [Fact]
    public void CoverAtThresholdIgnoresDrawingCoordinateRoundoff()
    {
        var centreline = QaSegment.Line(new(0, 0), new(5, 0), 1);
        var terrain = QaSegment.Line(new(0, 0.5999999997301478), new(5, 0.5999999997301478), 1);
        Assert.False(QaGeometry.IsLowCover(terrain.At(0), 0.60));
        Assert.Empty(QaGeometry.Cover(centreline, terrain, 0, 0.60, 0, 5));
        Assert.True(QaGeometry.IsLowCover(0.59999, 0.60));
    }

    [Fact]
    public void SampledWorstDoesNotUseUnsampledInternalMinimum()
    {
        var crest = QaSegment.Parabola(0, 5, 0, 1, -0.4, 1);
        var terrain = QaSegment.Line(new(0, 1.6), new(5, 1.6), 1);
        Assert.Equal(0.25, QaGeometry.MinimumCover(crest, terrain, 0.1, 0, 5).Cover, 8);
        var sampled = QaGeometry.CoverStations(0, 5).Select(s => terrain.At(s) - crest.At(s) - 0.1).Min();
        Assert.Equal(1.5, sampled, 8);
    }

    [Fact]
    public void PolynomialIntersectionsAgreeWithIndependentCircleMembership()
    {
        var random = new Random(177);
        for (int trial = 0; trial < 100; trial++)
        {
            double z = random.NextDouble() * 2 - 1;
            var segment = trial % 2 == 0
                ? QaSegment.Parabola(-2, 2, z, random.NextDouble() - 0.5, random.NextDouble() - 0.5, 1)
                : QaSegment.Ellipse(-2, 2, 0, z - 4, 5, 4, 1, 1);
            double cx = random.NextDouble() - 0.5, cz = random.NextDouble() - 0.5, radius = 0.2 + random.NextDouble();
            var ranges = QaGeometry.Intrusions(segment, Circle(cx, cz, radius), -2, 2).ToArray();
            for (int i = 0; i < 200; i++)
            {
                double station = -2 + (i + 0.371) / 200 * 4;
                bool expected = Math.Pow(station - cx, 2) + Math.Pow(segment.At(station) - cz, 2) < radius * radius;
                Assert.Equal(expected, ranges.Any(r => station > r.Start && station < r.End));
            }
        }
    }
}
