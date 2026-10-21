using System;
using System.Linq;
using IntersectUtilities.LER2.AttachStik;
using Xunit;
using Xunit.Sdk;

namespace IntersectUtilities.Geometry.Tests;

public sealed class StikAttachmentGeometryTests
{
    private static readonly StikEdge Main = new(new(0, -10, 10), new(0, 10, 14));

    [Fact]
    public void ProjectsAlongBranchDirectionAndInterpolatesInsideMainEdge()
    {
        StikPoint[] branch = [new(-1, 1, 30), new(-3, 3, -99), new(-6, 3, -99)];
        StikAttachment result = Attach(branch, [Main]);
        Assert.True(result.AtStart);
        Point(new(0, 0, 12), result.Points[0]);
        Assert.Equal(Math.Sqrt(2), result.Distance, 8);
        Point(new(-3, 3, 12 + Math.Sqrt(18) * .02), result.Points[1]);
        Point(new(-6, 3, 12 + (Math.Sqrt(18) + 3) * .02), result.Points[2]);
        Assert.Equal(30, branch[0].Z); // Input snapshot is immutable.
    }

    [Fact]
    public void ReversingBranchStoragePreservesTheResultAndVertexOrder()
    {
        StikPoint[] branch = [new(-1, 0, 30), new(-4, 0, -99), new(-4, 4, -99)];
        StikAttachment first = Attach(branch, [Main]);
        StikAttachment reverse = Attach(branch.Reverse().ToArray(), [Main]);
        Assert.False(reverse.AtStart);
        Assert.Equal(first.Points, reverse.Points.Reverse().ToArray());
    }

    [Fact]
    public void AllUnknownEndpointsChooseTheNearerForwardIntersection()
    {
        StikEdge leftMain = new(new(-8, -2, 4), new(-8, 2, 8));
        StikPoint[] branch = [new(-1, 0, -99), new(-5, 0, -99)];
        StikAttachment result = Attach(branch, [Main, leftMain]);
        Assert.True(result.AtStart);
        Point(new(0, 0, 12), result.Points[0]);
        Point(new(-5, 0, 12.1), result.Points[1]);
    }

    [Fact]
    public void OnlyTheKnownEndpointIsEligibleEvenWhenTheOtherEndIsNearer()
    {
        StikEdge leftMain = new(new(-6, -2, 4), new(-6, 2, 8));
        StikPoint[] branch = [new(-2, 0, 30), new(-5, 0, -99)];
        StikAttachment result = Attach(branch, [Main, leftMain]);
        Assert.True(result.AtStart);
        Assert.Equal(2, result.Distance);
    }

    [Fact]
    public void BothKnownEndpointsAreSkipped()
    {
        Assert.Contains("Both endpoints", Skip([new(-1, 0, 30), new(-5, 0, 29)], [Main]));
    }

    [Theory]
    [InlineData(-99, 14)]
    [InlineData(10, -99)]
    [InlineData(-99, -99)]
    [InlineData(0, 14)]
    [InlineData(10, 0)]
    public void AnUnknownTargetVertexCannotBeUsedForInterpolation(double z0, double z1)
    {
        Assert.Contains("No extend/trim", Skip([new(-1, 0, 30), new(-5, 0, -99)],
            [new(new(0, -10, z0), new(0, 10, z1))]));
    }

    [Fact]
    public void AValidEdgeOnAPartiallyUnknownMainRemainsUsable()
    {
        StikEdge invalid = new(new(-.5, -10, 10), new(-.5, 10, -99));
        StikAttachment result = Attach([new(-1, 0, 30), new(-5, 0, -99)], [invalid, Main]);
        Point(new(0, 0, 12), result.Points[0]);
    }

    [Fact]
    public void ChoosesTheFirstForwardIntersectionIndependentOfEdgeOrder()
    {
        StikEdge far = new(new(3, -10, 40), new(3, 10, 60));
        StikPoint[] branch = [new(-1, 0, 30), new(-5, 0, -99)];
        Assert.Equal(Attach(branch, [far, Main]).Points, Attach(branch, [Main, far]).Points);
        Assert.Equal(12, Attach(branch, [far, Main]).Points[0].Z);
    }

    [Fact]
    public void MainMayNotBeExtendedPastItsActualSegment()
    {
        Assert.Contains("No extend/trim", Skip([new(-1, 0, 30), new(-5, 0, -99)],
            [new(new(0, 1, 10), new(0, 2, 14))]));
    }

    [Fact]
    public void TrimmingCannotPassTheNextDistinctBranchVertex()
    {
        Assert.Contains("No extend/trim", Skip([new(1, 0, 30), new(5, 0, -99)],
            [new(new(6, -10, 10), new(6, 10, 14))]));
    }

    [Fact]
    public void SearchLimitMeasuresForwardExtensionRatherThanPerpendicularGap()
    {
        StikEdge shallow = new(new(-100, .1, 10), new(100, .1, 12));
        Assert.Contains("No extend/trim", Skip([new(0, 0, 30), new(-1, -.001, -99)], [shallow], maximum: 2));
    }

    [Fact]
    public void TrimsTheOvershootingStartAndUsesTheRemainingHorizontalPathLength()
    {
        StikAttachment result = Attach([new(1, 0, 30), new(-5, 0, -99), new(-5, 4, -99)], [Main]);
        Assert.True(result.AtStart);
        Assert.Equal(1, result.Distance, 8);
        Point(new(0, 0, 12), result.Points[0]);
        Point(new(-5, 0, 12.1), result.Points[1]);
        Point(new(-5, 4, 12.18), result.Points[2]);
    }

    [Fact]
    public void TrimsTheOvershootingEndWithoutReversingVertexStorage()
    {
        StikAttachment result = Attach([new(-5, 4, -99), new(-5, 0, -99), new(1, 0, 30)], [Main]);
        Assert.False(result.AtStart);
        Point(new(0, 0, 12), result.Points[2]);
        Point(new(-5, 0, 12.1), result.Points[1]);
        Point(new(-5, 4, 12.18), result.Points[0]);
    }

    [Fact]
    public void ChoosesAShorterTrimOverALongerExtension()
    {
        StikEdge outward = new(new(3, -10, 20), new(3, 10, 24));
        StikAttachment result = Attach([new(1, 0, 30), new(-5, 0, -99)], [outward, Main]);
        Point(new(0, 0, 12), result.Points[0]);
        Assert.Equal(1, result.Distance, 8);
    }

    [Fact]
    public void TrimmingUsesTheSameInclusiveMaximumMovementAsExtension()
    {
        StikPoint[] branch = [new(1, 0, 30), new(-5, 0, -99)];
        Assert.Equal(1, Attach(branch, [Main], maximum: 1).Distance);
        Assert.Contains("No extend/trim", Skip(branch, [Main], maximum: .99));
    }

    [Fact]
    public void TrimmingCannotCrossABend()
    {
        StikEdge behindBend = new(new(-2, -10, 10), new(-2, 10, 14));
        Assert.Contains("No extend/trim", Skip([new(1, 0, 30), new(-1, 0, -99), new(-1, 5, -99)], [behindBend]));
    }

    [Fact]
    public void TrimmingCannotCollapseTheTerminalSegment()
    {
        StikEdge atNextVertex = new(new(-5, -10, 10), new(-5, 10, 14));
        Assert.Contains("No extend/trim", Skip([new(1, 0, 30), new(-5, 0, -99)], [atNextVertex]));
    }

    [Fact]
    public void TrimmingMovesDuplicateTerminalVerticesTogetherWithoutLeavingAnOvershoot()
    {
        StikAttachment result = Attach([new(1, 0, 30), new(1, 0, -99), new(-5, 0, -99)], [Main]);
        Point(new(0, 0, 12), result.Points[0]);
        Point(new(0, 0, 12), result.Points[1]);
        Point(new(-5, 0, 12.1), result.Points[2]);
        Assert.Equal(3, result.Points.Count);
    }

    [Fact]
    public void WithTwoUnknownEndsTheNearerTrimChoosesTheConnectionEnd()
    {
        StikAttachment result = Attach([new(1, 0, -99), new(-5, 0, -99)], [Main]);
        Assert.True(result.AtStart);
        Assert.Equal(1, result.Distance, 8);
        Point(new(0, 0, 12), result.Points[0]);
        Point(new(-5, 0, 12.1), result.Points[1]);
    }

    [Fact]
    public void EquallyNearTrimAndExtensionTargetsAreAmbiguous()
    {
        StikEdge outward = new(new(2, -10, 20), new(2, 10, 24));
        Assert.Contains("disagree", Skip([new(1, 0, 30), new(-5, 0, -99)], [outward, Main]));
    }

    [Fact]
    public void SearchDistanceIsInclusive()
    {
        StikPoint[] branch = [new(-1, 0, 30), new(-5, 0, -99)];
        Assert.Equal(1, Attach(branch, [Main], maximum: 1).Distance);
        Assert.Contains("No extend/trim", Skip(branch, [Main], maximum: .99));
    }

    [Theory]
    [InlineData(20, 12.1)]
    [InlineData(0, 12)]
    [InlineData(-20, 11.9)]
    public void SlopeIsSignedAndMeasuredInPermille(double slope, double expected)
    {
        StikAttachment result = Attach([new(-1, 0, 30), new(-5, 0, -99)], [Main], slope);
        Assert.Equal(expected, result.Points[1].Z, 8);
    }

    [Fact]
    public void AnEndAlreadyOnAnEdgeInteriorGetsTheInterpolatedElevation()
    {
        StikAttachment result = Attach([new(0, 2, 30), new(-5, 2, -99)], [Main]);
        Assert.Equal(0, result.Distance);
        Point(new(0, 2, 12.4), result.Points[0]);
    }

    [Fact]
    public void AnEndAlreadyOnACollinearEdgeIsSupported()
    {
        StikAttachment result = Attach([new(0, 2, 30), new(0, 1, -99)], [Main]);
        Assert.Equal(0, result.Distance);
        Assert.Equal(12.42, result.Points[1].Z, 8);
    }

    [Fact]
    public void DuplicateEndVerticesUseTheFirstNonzeroDirection()
    {
        StikAttachment result = Attach([new(-1, 0, 30), new(-1, 0, -99), new(-5, 0, -99)], [Main]);
        Point(new(0, 0, 12), result.Points[0]);
        Assert.Equal(12.02, result.Points[1].Z, 8);
        Assert.Equal(12.1, result.Points[2].Z, 8);
    }

    [Fact]
    public void CoincidentTargetVerticesWithTheSameZAreOneConnection()
    {
        StikEdge first = new(new(0, -10, 10), new(0, 0, 12));
        StikEdge second = new(new(0, 0, 12), new(0, 10, 14));
        StikAttachment result = Attach([new(-1, 0, 30), new(-5, 0, -99)], [first, second]);
        Assert.Equal(12, result.Points[0].Z);
    }

    [Fact]
    public void EquallyNearMainsAtDifferentZAreSkipped()
    {
        StikEdge otherLevel = new(new(0, -10, 20), new(0, 10, 24));
        Assert.Contains("disagree", Skip([new(-1, 0, 30), new(-5, 0, -99)], [Main, otherLevel]));
    }

    [Fact]
    public void EquallyNearUnknownEndsAreSkipped()
    {
        StikEdge leftMain = new(new(-6, -10, 10), new(-6, 10, 14));
        Assert.Contains("disagree", Skip([new(-1, 0, -99), new(-5, 0, -99)], [Main, leftMain]));
    }

    [Fact]
    public void ParallelAndDegenerateGeometryDoNotModifyTheBranch()
    {
        Assert.Contains("No extend/trim", Skip([new(-1, 0, 30), new(-1, 5, -99)], [Main]));
        Assert.Contains("No extend/trim", Skip([new(-1, 0, 30), new(-1, 0, -99)], [Main]));
        Assert.Contains("No extend/trim", Skip([new(-1, 0, 30), new(-5, 0, -99)], [new(new(0, 0, 10), new(0, 0, 14))]));
    }

    [Fact]
    public void InputValidationRejectsNonfiniteValuesAndInvalidSearchLimits()
    {
        StikPoint[] branch = [new(-1, 0, 30), new(-5, 0, -99)];
        Assert.Contains("finite", Skip(branch, [Main], double.NaN));
        Assert.Contains("finite", Skip(branch, [Main], maximum: double.PositiveInfinity));
        Assert.Contains("positive", Skip(branch, [Main], maximum: 0));
        Assert.Contains("invalid", Skip([new(double.NaN, 0, 30), new(-5, 0, -99)], [Main]));
        Assert.Contains("too few", Skip([], [Main]));
    }

    [Fact]
    public void RotatedBentBranchesAtSurveyCoordinatesSatisfyIndependentSlopeChecks()
    {
        Random random = new(8173);
        for (int i = 0; i < 300; i++)
        {
            double angle = random.NextDouble() * Math.PI * 2;
            double ux = Math.Cos(angle), uy = Math.Sin(angle);
            double x = 725000 + random.NextDouble() * 100;
            double y = 6170000 + random.NextDouble() * 100;
            double gap = .1 + random.NextDouble() * 3;
            double slope = -40 + random.NextDouble() * 100;
            StikPoint pivot = new(x, y, 14);
            StikEdge main = new(new(x - 10 * uy, y + 10 * ux, 10), new(x + 10 * uy, y - 10 * ux, 18));
            StikPoint[] branch = [new(x - gap * ux, y - gap * uy, 30),
                new(x - (gap + 3) * ux, y - (gap + 3) * uy, -99),
                new(x - (gap + 6) * ux + 2 * uy, y - (gap + 6) * uy - 2 * ux, -99)];
            StikAttachment result = Attach(branch, [main], slope);
            Assert.InRange(result.Points[0].DistanceXY(pivot), 0, 1e-7);
            Assert.InRange(Math.Abs(result.Points[0].Z - 14), 0, 1e-7);
            double length = 0;
            for (int v = 1; v < branch.Length; v++)
            {
                Assert.Equal(branch[v].X, result.Points[v].X);
                Assert.Equal(branch[v].Y, result.Points[v].Y);
                length += result.Points[v].DistanceXY(result.Points[v - 1]);
                Assert.InRange(Math.Abs(result.Points[v].Z - (14 + slope / 1000 * length)), 0, 1e-7);
            }
        }
    }

    private static StikAttachment Attach(StikPoint[] branch, StikEdge[] edges, double slope = 20, double maximum = 10) =>
        StikAttachmentGeometry.Solve(branch, edges, slope, maximum).Match(
            result => result, reason => throw new XunitException("Unexpected skip: " + reason));

    private static string Skip(StikPoint[] branch, StikEdge[] edges, double slope = 20, double maximum = 10) =>
        StikAttachmentGeometry.Solve(branch, edges, slope, maximum).Match(_ => "Unexpected attachment", reason => reason);

    private static void Point(StikPoint expected, StikPoint actual)
    {
        Assert.Equal(expected.X, actual.X, 8);
        Assert.Equal(expected.Y, actual.Y, 8);
        Assert.Equal(expected.Z, actual.Z, 8);
    }
}
