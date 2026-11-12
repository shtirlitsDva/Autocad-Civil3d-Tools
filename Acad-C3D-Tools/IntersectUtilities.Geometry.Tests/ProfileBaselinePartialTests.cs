using System;
using System.Collections.Generic;
using System.Linq;
using IntersectUtilities.LongitudinalProfiles.Baseline;
using Xunit;

public sealed class ProfileBaselinePartialTests
{
    [Fact]
    public void CompletePlanHasOnePartAndNoProblems()
    {
        var guide = new[] { P(0, 0), P(20, 5), P(40, 0) };
        var plan = Analyze(guide, 4);
        Assert.Empty(plan.Problems);
        var part = Assert.Single(plan.Parts);
        Assert.Equal(guide[0], part.Vertices[0].Point);
        Assert.Equal(guide[^1], part.Vertices[^1].Point);
        Check(plan, guide, 4);
    }

    [Fact]
    public void OversizeMiddleCornerLeavesValidPiecesOnBothSides()
    {
        var guide = DifficultGuide();
        var plan = Analyze(guide, 4);
        Assert.NotEmpty(plan.Problems);
        Assert.True(plan.Parts.Count >= 2);
        Assert.Contains(plan.Problems, p => p.FirstVertex == 3 && p.From == guide[2]);
        Assert.Equal(guide[0], plan.Parts[0].Vertices[0].Point);
        Assert.Equal(guide[^1], plan.Parts[^1].Vertices[^1].Point);
        Check(plan, guide, 4);
        for (int i = 1; i < plan.Parts.Count; i++)
            Assert.True(plan.Parts[i].Vertices[0].Point.X - plan.Parts[i - 1].Vertices[^1].Point.X > 1e-8);
    }

    [Fact]
    public void MultipleBadCornersPreserveTheValidMiddleSection()
    {
        var guide = new[] { P(0, 0), P(20, 2), P(30, 1), P(30.2, 0), P(40, 2),
            P(60, 2), P(70, 1), P(70.2, 0), P(80, 2), P(100, 0) };
        var plan = Analyze(guide, 4);
        Assert.True(plan.Parts.Count >= 3);
        Assert.Contains(plan.Problems, p => p.From == guide[2]);
        Assert.Contains(plan.Problems, p => p.From == guide[6]);
        Check(plan, guide, 4);
    }

    [Fact]
    public void NothingIsInventedWhenNoRadiusControlledGeometryFits()
    {
        var plan = Analyze(new[] { P(-1, 0), P(0, 1), P(1, 0) }, 100);
        Assert.NotEmpty(plan.Problems);
        Assert.Empty(plan.Parts);
    }

    [Fact]
    public void ProblemsUseWorldCoordinatesAndOriginalVertexNumbers()
    {
        var offset = P(724979.0883, 6185453.9966);
        var guide = DifficultGuide().Select(p => p + offset).Reverse().ToArray();
        var plan = Analyze(guide, 4);
        Assert.Contains(plan.Problems, p => p.FirstVertex == 4 && p.From == guide[3]);
        Check(plan, guide.Reverse().ToArray(), 4);
    }

    [Fact]
    public void LegacySolveStillRejectsIncompleteResults()
    {
        bool rejected = ProfileBaselineGeometry.Solve(DifficultGuide(), 4).Match(_ => false, _ => true);
        Assert.True(rejected);
    }

    [Fact]
    public void BadShortEdgeDoesNotDiscardTheLongValidStraightBesideIt()
    {
        var guide = new[] { P(-100, 0), P(0, 1), P(0.1, 0), P(20, 0) };
        var plan = Analyze(guide, 4);
        Assert.NotEmpty(plan.Problems);
        Assert.True(plan.Parts.Count >= 2);
        Assert.Equal(guide[0], plan.Parts[0].Vertices[0].Point);
        Assert.True(plan.Parts[0].Vertices[^1].Point.X - plan.Parts[0].Vertices[0].Point.X > 90);
        Check(plan, guide, 4);
    }

    private static BaselinePoint[] DifficultGuide() => new[] { P(0, 0), P(20, 2), P(30, 1), P(30.2, 0), P(40, 2), P(60, 0) };
    private static BaselinePoint P(double x, double y) => new(x, y);
    private static BaselinePlan Analyze(BaselinePoint[] guide, double radius)
        => ProfileBaselineGeometry.Analyze(guide, radius).Match(p => p, message => throw new Xunit.Sdk.XunitException(message));
    private static void Check(BaselinePlan plan, IReadOnlyList<BaselinePoint> guide, double radius)
    {
        foreach (var part in plan.Parts)
        {
            Assert.NotEmpty(part.Segments);
            Assert.True(ProfileBaselineGeometry.ValidateCover(part.Segments, guide.Select(p => p - part.Origin).ToArray())
                .Match(_ => true, _ => false));
            Assert.All(part.Segments.OfType<BaselineSegment.Arc>(), arc => Assert.Equal(radius, arc.Radius));
            for (int i = 1; i < part.Segments.Count; i++)
            {
                Assert.True((part.Segments[i - 1].End - part.Segments[i].Start).Length <= 1e-8);
                Assert.True(Math.Abs(part.Segments[i - 1].EndDirection - part.Segments[i].StartDirection) <= 1e-8);
            }
        }
    }
}
