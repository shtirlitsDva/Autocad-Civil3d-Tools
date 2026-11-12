using IntersectUtilities.LongitudinalProfiles;

public sealed class ProfileViewProximityTests
{
    [Fact]
    public void GridContainingPickWinsOverCloserInsertionPoint()
    {
        var grids = new[] { Grid("long", 0, 0, 1000, 20), Grid("short", 990, 30, 1010, 50) };
        Assert.Equal(new[] { "long" }, Find(grids, 999, 10));
    }

    [Fact]
    public void OutsidePickUsesDistanceToNearestGridEdge()
    {
        var grids = new[] { Grid("long", 0, 0, 1000, 20), Grid("short", 990, 50, 1010, 70) };
        Assert.Equal(new[] { "long" }, Find(grids, 999, 30));
    }

    [Fact]
    public void DiagonalDistanceUsesBothAxes()
    {
        var grids = new[] { Grid("diagonal", 3, 4, 4, 5), Grid("horizontal", 6, -1, 7, 1) };
        Assert.Equal(new[] { "diagonal" }, Find(grids, 0, 0));
    }

    [Fact]
    public void OverlappingGridsRemainAmbiguous()
    {
        var grids = new[] { Grid("one", 0, 0, 10, 10), Grid("two", 5, 5, 15, 15) };
        Assert.Equal(new[] { "one", "two" }, Find(grids, 7, 7));
    }

    [Fact]
    public void EqualOutsideDistancesRemainAmbiguous()
    {
        var grids = new[] { Grid("left", -20, 0, -10, 10), Grid("right", 10, 0, 20, 10) };
        Assert.Equal(new[] { "left", "right" }, Find(grids, 0, 5));
    }

    [Fact]
    public void SharedBoundaryIsAmbiguous()
    {
        var grids = new[] { Grid("left", 0, 0, 10, 10), Grid("right", 10, 0, 20, 10) };
        Assert.Equal(new[] { "left", "right" }, Find(grids, 10, 5));
    }

    [Fact]
    public void InsidePickWinsEvenIfAnotherGridIsWithinDistanceTolerance()
    {
        var grids = new[] { Grid("inside", 0, 0, 10, 10), Grid("near", 10.000001, 0, 20, 10) };
        Assert.Equal(new[] { "inside" }, ProfileViewProximity.FindClosest(grids, 10, 5, 0.001));
    }

    [Fact]
    public void TiesUseTrueMinimumRegardlessOfEnumerationOrder()
    {
        var grids = new[]
        {
            Grid("third", 10.0018, 0, 20, 10), Grid("second", 10.0009, 0, 20, 10),
            Grid("nearest", 10, 0, 20, 10)
        };
        string[] expected = { "nearest", "second" };
        Assert.Equal(expected, ProfileViewProximity.FindClosest(grids, 0, 5, 0.001).OrderBy(id => id));
        Assert.Equal(expected, ProfileViewProximity.FindClosest(grids.Reverse(), 0, 5, 0.001).OrderBy(id => id));
    }

    [Fact]
    public void EmptyCandidatesReturnNoMatch()
    {
        Assert.Empty(Find(Array.Empty<ProfileViewGrid<string>>(), 0, 0));
    }

    [Fact]
    public void InvalidGridBoundsCannotWin()
    {
        var grids = new[]
        {
            Grid("nan", double.NaN, 0, 10, 10), Grid("reversed", 10, 0, 0, 10),
            Grid("zero", 0, 0, 0, 10), Grid("valid", 20, 0, 30, 10)
        };
        Assert.Equal(new[] { "valid" }, Find(grids, 0, 5));
    }

    [Theory]
    [InlineData(double.NaN, 0)]
    [InlineData(0, double.PositiveInfinity)]
    public void InvalidPickReturnsNoMatch(double x, double y)
    {
        Assert.Empty(Find(new[] { Grid("view", 0, 0, 10, 10) }, x, y));
    }

    [Fact]
    public void LargeDrawingCoordinatesPreserveNearestView()
    {
        const double x = 723000, y = 6172000;
        var grids = new[]
        {
            Grid("one", x, y, x + 500, y + 30),
            Grid("two", x, y + 100, x + 500, y + 130)
        };
        Assert.Equal(new[] { "two" }, Find(grids, x + 450, y + 120));
    }

    private static ProfileViewGrid<string> Grid(string id, double minX, double minY, double maxX, double maxY)
        => new(id, minX, minY, maxX, maxY);

    private static IReadOnlyList<string> Find(IEnumerable<ProfileViewGrid<string>> grids, double x, double y)
        => ProfileViewProximity.FindClosest(grids, x, y, 1e-8);
}
