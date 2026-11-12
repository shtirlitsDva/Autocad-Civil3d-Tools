using System;
using System.Linq;
using IntersectUtilities.LongitudinalProfiles.Baseline;
using Xunit;

public sealed class ProfileBaselineFailureFlowTests
{
    [Fact]
    public void EmptyDesiredResultCanStillRecoverAfterMinimumIsChosen()
    {
        var guide = new[] { new BaselinePoint(0, 0), new BaselinePoint(5, 5), new BaselinePoint(10, 0) };
        var desired = ProfileBaselineGeometry.Analyze(guide, 50)
            .Match(plan => plan, message => throw new Xunit.Sdk.XunitException(message));
        Assert.NotEmpty(desired.Problems);
        Assert.Empty(desired.Parts);

        var retry = ProfileBaselineGeometry.RetryWithMinimum(guide, 50, 5, desired)
            .Match(plan => plan, message => throw new Xunit.Sdk.XunitException(message));
        Assert.Empty(retry.Problems);
        Assert.Equal(new[] { 2 }, retry.MinimumVertices);
        var part = Assert.Single(retry.Parts);
        Assert.Equal(guide[0], part.Vertices[0].Point);
        Assert.Equal(guide[^1], part.Vertices[^1].Point);
        Assert.All(part.Segments.OfType<BaselineSegment.Arc>(), arc => Assert.Equal(5, arc.Radius));
        Assert.True(ProfileBaselineGeometry.ValidateCover(part.Segments, guide.Select(point => point - part.Origin).ToArray())
            .Match(_ => true, _ => false));
    }
}
