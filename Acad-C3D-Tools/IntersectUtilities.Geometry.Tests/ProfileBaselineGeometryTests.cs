using IntersectUtilities.LongitudinalProfiles.Baseline;
using Norsyn.AecPropertySets;
using Xunit;

public sealed class ProfileBaselineGeometryTests
{
    [Fact]
    public void CrestUsesTrueRadiusAndTouchesBothDraftEdges()
    {
        var solution = Solve(4, P(-10, 0), P(0, 5), P(10, 0));
        var arc = Assert.Single(solution.Segments.OfType<BaselineSegment.Arc>());
        Assert.Equal(1, solution.Crests);
        Assert.Equal(0, solution.Sags);
        Assert.Equal(4, arc.Radius);
        Assert.True(arc.Bulge < 0);
        AssertClose(Math.Atan(0.5), arc.StartDirection);
        AssertClose(Math.Atan(-0.5), arc.EndDirection);
        AssertClose(arc.Start.X / 2, arc.Start.Y);
        AssertClose(10 - arc.End.X / 2, arc.End.Y);
        AssertEndpoints(solution, P(-10, 0), P(10, 0));
        AssertTangency(solution);
    }

    [Fact]
    public void SymmetricSagPassesThroughVertexAndStartsAndEndsWithLines()
    {
        var solution = Solve(4, P(-10, 5), P(0, 0), P(10, 5));
        var arc = Assert.Single(solution.Segments.OfType<BaselineSegment.Arc>());
        Assert.Equal(1, solution.Sags);
        Assert.True(arc.Bulge > 0);
        // Local origin is (-10,5).
        AssertClose(-5, arc.HeightAt(10));
        Assert.Equal(0, solution.Vertices[0].Bulge);
        Assert.Equal(0, solution.Vertices[^2].Bulge);
        AssertEndpoints(solution, P(-10, 5), P(10, 5));
        AssertTangency(solution);
    }

    [Fact]
    public void AsymmetricSagMatchesTheTenAnd145DegreeBisectorExample()
    {
        double degrees = Math.PI / 180;
        var vertex = P(0, 0);
        var solution = Solve(2, P(-10, 10 * Math.Tan(35 * degrees)), vertex,
            P(10, 10 * Math.Tan(10 * degrees)));
        var arc = Assert.Single(solution.Segments.OfType<BaselineSegment.Arc>());
        var center = arc.Center + solution.Origin;
        AssertClose(77.5 * degrees, Math.Atan2(center.Y, center.X));
        AssertClose(2, center.Length);
        AssertClose(vertex.Y, arc.HeightAt(vertex.X - solution.Origin.X) + solution.Origin.Y);
        AssertClose(-12.5 * degrees, Math.Atan2(vertex.X - center.X,
            Math.Sqrt(4 - Math.Pow(vertex.X - center.X, 2))));
        AssertTangency(solution);
    }

    [Theory]
    [InlineData(2, 8, 1, 0)]
    [InlineData(6, 8, 0, 1)]
    public void TwoUphillEdgesAreClassifiedByTheirChangeInAngle(double middle, double last, int sags, int crests)
    {
        var solution = Solve(2, P(-20, 0), P(0, middle), P(20, last));
        Assert.Equal(sags, solution.Sags);
        Assert.Equal(crests, solution.Crests);
        AssertTangency(solution);
    }

    [Fact]
    public void CrestSagCrestUsesCommonTangentsAndKeepsAllRequiredContacts()
    {
        var points = new[] { P(-4, 2), P(0, 4), P(8, 0), P(16, 4), P(20, 2) };
        var solution = Solve(4, points);
        Assert.Equal(2, solution.Crests);
        Assert.Equal(1, solution.Sags);
        Assert.Equal(7, solution.Segments.Count);
        AssertTangency(solution);
        AssertBelowGuide(solution, points);
        var sag = solution.Segments.OfType<BaselineSegment.Arc>().Single(arc => arc.Curvature > 0);
        AssertClose(0, sag.HeightAt(8 - solution.Origin.X) + solution.Origin.Y);
        Assert.True(solution.Segments[2].EndDirection < Math.Atan(-0.5));
    }

    [Fact]
    public void ReverseVertexOrderCreatesIdenticalGeometry()
    {
        var points = new[] { P(-10, 0), P(0, 5), P(10, 1), P(20, 4) };
        var forward = Solve(2, points);
        var reversed = Solve(2, points.Reverse().ToArray());
        Assert.Equal(forward.Vertices, reversed.Vertices);
    }

    [Fact]
    public void LargeSurveyCoordinatesPreserveGeometryAndEndpoints()
    {
        var points = new[] { P(-10, 0), P(0, 5), P(10, 1), P(20, 4) };
        var offset = P(724979.0883, 6185453.9966);
        var local = Solve(2, points);
        var shiftedPoints = points.Select(point => point + offset).ToArray();
        var shifted = Solve(2, shiftedPoints);
        AssertEndpoints(shifted, shiftedPoints[0], shiftedPoints[^1]);
        Assert.Equal(local.Vertices.Count, shifted.Vertices.Count);
        for (int i = 0; i < local.Vertices.Count; i++)
        {
            AssertClose(local.Vertices[i].Point.X + offset.X, shifted.Vertices[i].Point.X);
            AssertClose(local.Vertices[i].Point.Y + offset.Y, shifted.Vertices[i].Point.Y);
            AssertClose(local.Vertices[i].Bulge, shifted.Vertices[i].Bulge);
        }
        AssertBelowGuide(shifted, shiftedPoints);
    }

    [Fact]
    public void CollinearGuideProducesOneStraightAndPreservesEndpoints()
    {
        var solution = Solve(100, P(0, 0), P(2, 1), P(4, 2));
        Assert.Single(solution.Segments);
        Assert.Equal(0, solution.Crests + solution.Sags);
        AssertEndpoints(solution, P(0, 0), P(4, 2));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidRadiiAreRejected(double radius)
        => AssertFault(ProfileBaselineGeometry.Solve(new[] { P(0, 0), P(10, 0) }, radius));

    [Fact]
    public void VerticalDuplicateBacktrackingAndNonFiniteVerticesAreRejected()
    {
        AssertFault(ProfileBaselineGeometry.Solve(new[] { P(0, 0), P(0, 1), P(10, 0) }, 2));
        AssertFault(ProfileBaselineGeometry.Solve(new[] { P(0, 0), P(0, 0), P(10, 0) }, 2));
        AssertFault(ProfileBaselineGeometry.Solve(new[] { P(0, 0), P(10, 1), P(5, 0), P(20, 0) }, 2));
        AssertFault(ProfileBaselineGeometry.Solve(new[] { P(0, 0), P(double.NaN, 0) }, 2));
    }

    [Fact]
    public void OversizeRadiiReportVerticesRatherThanReducingTheRadius()
    {
        string crest = AssertFault(ProfileBaselineGeometry.Solve(new[] { P(-1, 0), P(0, 1), P(1, 0) }, 100));
        string sag = AssertFault(ProfileBaselineGeometry.Solve(new[] { P(-1, 0.5), P(0, 0), P(1, 0.5) }, 4));
        Assert.Contains("vertex 2", crest);
        Assert.Contains("vertices", sag);
    }

    [Fact]
    public void CoverCheckFindsAnArcCrossingBetweenSafeEndpoints()
    {
        var arc = new BaselineSegment.Arc(P(-0.6, 0.8), P(0.6, 0.8), P(0, 0),
            1, -1, Math.Atan(0.75), -Math.Atan(0.75), 2);
        Assert.True(arc.Start.Y < 0.9 && arc.End.Y < 0.9);
        AssertFault(ProfileBaselineGeometry.ValidateCover(new[] { arc }, new[] { P(-1, 0.9), P(1, 0.9) }));
    }

    [Fact]
    public void CoverCheckFindsADraftDipBetweenStraightEndpoints()
    {
        var line = new BaselineSegment.Line(P(-1, 0), P(1, 0), 2);
        AssertFault(ProfileBaselineGeometry.ValidateCover(new[] { line }, new[] { P(-1, 0), P(0, -1), P(1, 0) }));
    }

    [Fact]
    public void DeterministicMixedProfilesKeepRadiusTangencyAndCover()
    {
        var random = new Random(771);
        int successes = 0;
        for (int test = 0; test < 150; test++)
        {
            var points = new List<BaselinePoint> { P(0, 0) };
            for (int i = 0; i < 8; i++)
                points.Add(points[^1] + P(10 + random.NextDouble() * 10, random.NextDouble() * 10 - 5));
            double radius = 1 + random.NextDouble() * 5;
            ProfileBaselineGeometry.Solve(points, radius).Match(
                solution =>
                {
                    successes++;
                    AssertTangency(solution);
                    AssertBelowGuide(solution, points);
                    AssertEndpoints(solution, points[0], points[^1]);
                    Assert.All(solution.Segments.OfType<BaselineSegment.Arc>(), arc => Assert.Equal(radius, arc.Radius));
                    return 0;
                }, _ => 0);
        }
        Assert.True(successes > 100, $"Only {successes} of 150 ordinary profiles were feasible.");
    }

    private static BaselinePoint P(double x, double y) => new(x, y);
    private static BaselineSolution Solve(double radius, params BaselinePoint[] points)
        => ProfileBaselineGeometry.Solve(points, radius).Match(solution => solution,
            message => throw new Xunit.Sdk.XunitException(message));
    private static string AssertFault<T>(Result<T> result)
        => result.Match(_ => throw new Xunit.Sdk.XunitException("Expected an explicit failure."), message => message);
    private static void AssertClose(double expected, double actual) => Assert.True(
        Math.Abs(expected - actual) <= 1e-8, $"Expected {expected:R}, actual {actual:R}.");
    private static void AssertEndpoints(BaselineSolution solution, BaselinePoint first, BaselinePoint last)
    {
        Assert.Equal(first, solution.Vertices[0].Point);
        Assert.Equal(last, solution.Vertices[^1].Point);
        Assert.Equal(0, solution.Vertices[0].Bulge);
        Assert.Equal(0, solution.Vertices[^2].Bulge);
    }
    private static void AssertTangency(BaselineSolution solution)
    {
        for (int i = 1; i < solution.Segments.Count; i++)
        {
            AssertClose(solution.Segments[i - 1].End.X, solution.Segments[i].Start.X);
            AssertClose(solution.Segments[i - 1].End.Y, solution.Segments[i].Start.Y);
            AssertClose(solution.Segments[i - 1].EndDirection, solution.Segments[i].StartDirection);
        }
    }
    private static void AssertBelowGuide(BaselineSolution solution, IReadOnlyList<BaselinePoint> guide)
    {
        foreach (var segment in solution.Segments)
            for (int sample = 0; sample <= 40; sample++)
            {
                double x = segment.Start.X + (segment.End.X - segment.Start.X) * sample / 40;
                double worldX = x + solution.Origin.X;
                int edge = 0;
                while (edge < guide.Count - 2 && guide[edge + 1].X < worldX)
                    edge++;
                var a = guide[edge];
                var b = guide[edge + 1];
                double ceiling = a.Y + (b.Y - a.Y) * ((worldX - a.X) / (b.X - a.X));
                Assert.True(segment.HeightAt(x) + solution.Origin.Y <= ceiling + 1e-8);
            }
    }
}
