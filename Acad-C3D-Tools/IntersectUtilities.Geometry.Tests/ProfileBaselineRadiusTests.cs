using System;
using System.Collections.Generic;
using System.Linq;
using IntersectUtilities.LongitudinalProfiles.Baseline;
using Xunit;

public sealed class ProfileBaselineRadiusTests
{
    [Fact]
    public void SuccessfulDesiredRadiusIsNeverReduced()
    {
        var guide = new[] { P(0, 0), P(100, 5), P(200, 0) };
        var first = Analyze(guide, 50);
        var final = Analyze(guide, 50, 5);
        Assert.Empty(final.Problems);
        Assert.Empty(final.MinimumVertices);
        Assert.Equal(first.Parts[0].Vertices, final.Parts[0].Vertices);
        Assert.All(Arcs(final), arc => Assert.Equal(50, arc.Radius));
    }

    [Fact]
    public void TwoTightCornersUseMinimumWhileOtherCornersKeepDesiredRadius()
    {
        var guide = TwoTightCorners();
        var first = Analyze(guide, 50);
        Assert.NotEmpty(first.Problems);
        var final = Analyze(guide, 50, 5);
        Assert.Equal(new[] { 3, 4 }, final.MinimumVertices);
        Assert.Empty(final.Problems);
        Assert.Single(final.Parts);
        Assert.Contains(Arcs(final), arc => arc.Radius == 50);
        Assert.Contains(Arcs(final), arc => arc.Radius == 5);
        foreach (var arc in Arcs(final))
            Assert.Equal(final.MinimumVertices.Contains(arc.SourceVertex) ? 5 : 50, arc.Radius);
        foreach (var unchanged in Arcs(final).Where(arc => arc.Radius == 50))
            Assert.Contains(Arcs(first), original => original.SourceVertex == unchanged.SourceVertex && original.Center == unchanged.Center);
        Check(final, guide);
    }

    [Fact]
    public void SharedTangentFailureRetriesBothInternalCorners()
    {
        var guide = new[] { P(0, 0), P(100, 5), P(110, 4), P(120, 5), P(220, 0) };
        var first = Analyze(guide, 50);
        Assert.Contains(first.Problems, p => p.FirstVertex != p.LastVertex);
        var final = Analyze(guide, 50, 5);
        var expected = first.Problems.SelectMany(p => new[] { p.FirstVertex, p.LastVertex })
            .Where(i => i > 1 && i < guide.Length).Distinct().OrderBy(i => i).ToArray();
        Assert.Equal(expected, final.MinimumVertices);
        Assert.Empty(final.Problems);
        Check(final, guide);
    }

    [Fact]
    public void RemainingMinimumFailuresStayAvailableForPartialCreation()
    {
        var guide = TwoTightCorners();
        var first = Analyze(guide, 50);
        var final = Analyze(guide, 50, 40);
        Assert.NotEmpty(final.Problems);
        Assert.NotEmpty(final.Parts);
        var expected = first.Problems.SelectMany(p => new[] { p.FirstVertex, p.LastVertex })
            .Where(i => i > 1 && i < guide.Length).Distinct().OrderBy(i => i).ToArray();
        Assert.Equal(expected, final.MinimumVertices);
        Assert.All(Arcs(final), arc => Assert.True(arc.Radius == 50 || arc.Radius == 40));
        Check(final, guide);
    }

    [Fact]
    public void EqualRadiiPreserveTheSinglePassResult()
    {
        var guide = TwoTightCorners();
        var first = Analyze(guide, 50);
        var final = Analyze(guide, 50, 50);
        Assert.Empty(final.MinimumVertices);
        Assert.Equal(first.Problems, final.Problems);
        Assert.Equal(first.Parts.SelectMany(p => p.Vertices), final.Parts.SelectMany(p => p.Vertices));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(51)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidMinimumIsRejected(double minimum)
        => Assert.True(ProfileBaselineGeometry.Analyze(TwoTightCorners(), 50, minimum).Match(_ => false, _ => true));

    [Fact]
    public void ReversedSurveyCoordinatesKeepTheSameLocalRadiusAssignment()
    {
        var shift = P(724979.0883, 6185453.9966);
        var guide = TwoTightCorners().Select(p => p + shift).Reverse().ToArray();
        var final = Analyze(guide, 50, 5);
        Assert.Empty(final.Problems);
        Assert.Equal(new[] { 3, 4 }, final.MinimumVertices);
        Check(final, guide.Reverse().ToArray());
        foreach (var arc in Arcs(final))
            Assert.Equal(final.MinimumVertices.Contains(arc.SourceVertex) ? 5 : 50, arc.Radius);
    }

    [Fact]
    public void CollinearVerticesDoNotShiftFallbackToTheWrongCorner()
    {
        var guide = new[] { P(0, 0), P(30, 1.5), P(60, 3), P(100, 4), P(104, 3), P(150, 4), P(220, 0) };
        var final = Analyze(guide, 50, 5);
        Assert.Empty(final.Problems);
        Assert.Equal(new[] { 4, 5 }, final.MinimumVertices);
        foreach (var arc in Arcs(final))
            Assert.Equal(final.MinimumVertices.Contains(arc.SourceVertex) ? 5 : 50, arc.Radius);
        Check(final, guide);
    }

    private static BaselinePoint[] TwoTightCorners() => new[] { P(0, 0), P(60, 3), P(100, 4), P(104, 3), P(150, 4), P(220, 0) };
    private static BaselinePoint P(double x, double y) => new(x, y);
    private static BaselinePlan Analyze(BaselinePoint[] guide, double desired)
        => ProfileBaselineGeometry.Analyze(guide, desired).Match(p => p, message => throw new Xunit.Sdk.XunitException(message));
    private static BaselinePlan Analyze(BaselinePoint[] guide, double desired, double minimum)
        => ProfileBaselineGeometry.Analyze(guide, desired, minimum).Match(p => p, message => throw new Xunit.Sdk.XunitException(message));
    private static IEnumerable<BaselineSegment.Arc> Arcs(BaselinePlan plan) => plan.Parts.SelectMany(p => p.Segments).OfType<BaselineSegment.Arc>();
    private static void Check(BaselinePlan plan, IReadOnlyList<BaselinePoint> guide)
    {
        Assert.Equal(guide[0], plan.Parts[0].Vertices[0].Point);
        Assert.Equal(guide[^1], plan.Parts[^1].Vertices[^1].Point);
        foreach (var part in plan.Parts)
        {
            Assert.True(ProfileBaselineGeometry.ValidateCover(part.Segments, guide.Select(p => p - part.Origin).ToArray()).Match(_ => true, _ => false));
            for (int i = 1; i < part.Segments.Count; i++)
            {
                Assert.True((part.Segments[i - 1].End - part.Segments[i].Start).Length <= 1e-8);
                Assert.True(Math.Abs(part.Segments[i - 1].EndDirection - part.Segments[i].StartDirection) <= 1e-8);
            }
        }
    }
}
