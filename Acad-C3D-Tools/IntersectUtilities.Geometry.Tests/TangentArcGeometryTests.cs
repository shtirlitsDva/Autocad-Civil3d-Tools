using IntersectUtilities.ArcConstruction;

namespace IntersectUtilities.ArcConstruction.Tests;

public class TangentArcGeometryTests
{
    private static readonly ArcVector Origin = new(0, 0, 0);
    private static readonly ArcVector X = new(1, 0, 0);
    private static readonly ArcVector Y = new(0, 1, 0);

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, -1)]
    [InlineData(-1, 1)]
    [InlineData(-1, -1)]
    public void RightAngleUsesPickedCorner(int firstSide, int secondSide)
    {
        var result = TangentArcGeometry.Solve(X * -10, X * 10, X * firstSide,
            Y * -10, Y * 10, Y * secondSide, Math.PI * 2);
        Assert.Equal(TangentArcStatus.Success, result.Status);
        var arc = result.Geometry;
        Near(4, arc.Radius);
        Near(new ArcVector(4 * firstSide, 4 * secondSide, 0), arc.Center);
        Near(X * (4 * firstSide), arc.FirstTangent);
        Near(Y * (4 * secondSide), arc.SecondTangent);
        Assert.False(arc.UsesExtensions);
        AssertArc(arc, X, Y, Math.PI * 2);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(90)]
    [InlineData(120)]
    [InlineData(170)]
    [InlineData(179.9)]
    public void AcuteAndObtuseCornersHaveExactLengthAndTangency(double cornerDegrees)
    {
        double angle = cornerDegrees * Math.PI / 180;
        var ray = new ArcVector(Math.Cos(angle), Math.Sin(angle), 0);
        var result = TangentArcGeometry.Solve(Origin, X * 1000, X,
            Origin, ray * 1000, ray, 12.3);
        Assert.Equal(TangentArcStatus.Success, result.Status);
        Near(12.3 / ((180 - cornerDegrees) * Math.PI / 180), result.Geometry.Radius, 1e-6);
        AssertArc(result.Geometry, X, ray, 12.3);
    }

    [Fact]
    public void ReversingEndpointsAndSelectionOrderPreservesTheArc()
    {
        var forward = TangentArcGeometry.Solve(Origin, X * 20, X, Origin, Y * 20, Y, 10).Geometry;
        var reversed = TangentArcGeometry.Solve(Y * 20, Origin, Y, X * 20, Origin, X, 10).Geometry;
        Near(forward.Center, reversed.Center);
        Near(forward.FirstTangent, reversed.SecondTangent);
        Near(forward.SecondTangent, reversed.FirstTangent);
        Near(forward.Normal, reversed.Normal * -1);
        AssertArc(reversed, Y, X, 10);
    }

    [Fact]
    public void ShortSeparatedSegmentsUseTheirExtensions()
    {
        var result = TangentArcGeometry.Solve(X * 1, X * 2, X * 1.5,
            Y * 1, Y * 2, Y * 1.5, 10);
        Assert.Equal(TangentArcStatus.Success, result.Status);
        Assert.True(result.Geometry.UsesExtensions);
        AssertArc(result.Geometry, X, Y, 10);
    }

    [Fact]
    public void RotatedPlanesAndSurveyCoordinatesPreserveLengthAndTangency()
    {
        var random = new Random(1427);
        var origin = new ArcVector(450000, 6200000, 125);
        var basisX = new ArcVector(0.6, 0.8, 0);
        var basisY = new ArcVector(-0.48, 0.36, 0.8);
        for (int i = 0; i < 300; i++)
        {
            double angle = (10 + random.NextDouble() * 160) * Math.PI / 180;
            double rotation = random.NextDouble() * Math.PI * 2;
            var first = basisX * Math.Cos(rotation) + basisY * Math.Sin(rotation);
            var second = basisX * Math.Cos(rotation + angle) + basisY * Math.Sin(rotation + angle);
            double length = 1 + random.NextDouble() * 50;
            var result = TangentArcGeometry.Solve(origin - first * 20, origin + first * 100,
                origin + first * 10, origin - second * 30, origin + second * 90,
                origin + second * 10, length);
            Assert.Equal(TangentArcStatus.Success, result.Status);
            var arc = result.Geometry;
            AssertArc(arc, first, second, length, 1e-6);
            Near(0, (arc.FirstTangent - origin).Cross(first).Length, 1e-6);
            Near(0, (arc.SecondTangent - origin).Cross(second).Length, 1e-6);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidLengthsAreExplicitFailures(double length) =>
        Assert.Equal(TangentArcStatus.InvalidLength,
            TangentArcGeometry.Solve(Origin, X, X, Origin, Y, Y, length).Status);

    [Fact]
    public void DegenerateAndSkewLinesAreRejected()
    {
        Assert.Equal(TangentArcStatus.InvalidLine,
            TangentArcGeometry.Solve(Origin, Origin, X, Origin, Y, Y, 1).Status);
        Assert.Equal(TangentArcStatus.InvalidLine,
            TangentArcGeometry.Solve(new(double.NaN, 0, 0), X, X, Origin, Y, Y, 1).Status);
        Assert.Equal(TangentArcStatus.ParallelLines,
            TangentArcGeometry.Solve(Origin, X, X, Y, X + Y, X + Y, 1).Status);
        Assert.Equal(TangentArcStatus.ParallelLines,
            TangentArcGeometry.Solve(Origin, X, X, X * 2, X * 3, X * 2, 1).Status);
        Assert.Equal(TangentArcStatus.NonCoplanarLines,
            TangentArcGeometry.Solve(Origin, X, X, new(0, 0, 1), new(0, 1, 1), new(0, 1, 1), 1).Status);
        Assert.Equal(TangentArcStatus.AmbiguousPick,
            TangentArcGeometry.Solve(Origin, X, Origin, Origin, Y, Y, 1).Status);
        Assert.Equal(TangentArcStatus.Unrepresentable,
            TangentArcGeometry.Solve(Origin, X, X, Origin, Y, Y, 1e-20).Status);
    }

    [Fact]
    public void EveryFailureHasAnActionableMessage()
    {
        foreach (var status in Enum.GetValues<TangentArcStatus>().Where(s => s != TangentArcStatus.Success))
        {
            string message = TangentArcResult.Fault(status).Match(_ => "", error => error);
            Assert.False(string.IsNullOrWhiteSpace(message));
        }
    }

    private static void AssertArc(TangentArcSolution arc, ArcVector firstDirection,
        ArcVector secondDirection, double length, double tolerance = 1e-7)
    {
        Near(length, arc.Radius * arc.Sweep, tolerance);
        Near(arc.Radius, (arc.FirstTangent - arc.Center).Length, tolerance);
        Near(arc.Radius, (arc.SecondTangent - arc.Center).Length, tolerance);
        Near(0, (arc.FirstTangent - arc.Center).Dot(firstDirection), tolerance);
        Near(0, (arc.SecondTangent - arc.Center).Dot(secondDirection), tolerance);
        Near(1, arc.Normal.Length);
        Near(1, arc.StartDirection.Length);
        Near(0, arc.StartDirection.Dot(arc.Normal));
        Near(0, arc.Normal.Dot(firstDirection), tolerance);
        Near(0, arc.Normal.Dot(secondDirection), tolerance);
        Assert.InRange(arc.Sweep, 0, Math.PI);
        // Independently trace the circle in its declared positive-normal direction.
        var radial = arc.StartDirection * arc.Radius;
        Near(arc.FirstTangent, arc.Center + radial, tolerance);
        var end = arc.Center + radial * Math.Cos(arc.Sweep) + arc.Normal.Cross(radial) * Math.Sin(arc.Sweep);
        Near(arc.SecondTangent, end, tolerance);
    }

    private static void Near(double expected, double actual, double tolerance = 1e-8) =>
        Assert.True(Math.Abs(expected - actual) <= tolerance, $"Expected {expected:G17}, got {actual:G17}");

    private static void Near(ArcVector expected, ArcVector actual, double tolerance = 1e-8) =>
        Near(0, (expected - actual).Length, tolerance);
}
