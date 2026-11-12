using IntersectUtilities.LongitudinalProfiles;

namespace IntersectUtilities.Geometry.Tests;

public sealed class ProfileExtensionRulesTests
{
    [Fact]
    public void CommandDoesNotReadUnsafeNativeEditabilityGetter()
    {
        // Windows event 1026 identified this native getter in both 2026-11-11 crashes.
        string source = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "IntersectUtilities", "LongitudinalProfiles", "ExtendProfilesToViewCommand.cs"));
        Assert.DoesNotMatch(@"profile\s*\.\s*IsEditable\b", File.ReadAllText(source));
    }

    [Theory]
    [InlineData("004 TOP")]
    [InlineData("004_midt")]
    [InlineData("004-bUnD")]
    [InlineData("MIDT")]
    public void MatchesPipeProfileTokens(string name) => Assert.True(ProfileExtensionRules.IsPipeProfile(name));

    [Theory]
    [InlineData("TOPSOIL")]
    [InlineData("MIDTRANGE")]
    [InlineData("BUNDLED")]
    [InlineData("004 TERRAIN")]
    public void DoesNotMatchUnrelatedNames(string name) => Assert.False(ProfileExtensionRules.IsPipeProfile(name));

    [Theory]
    [InlineData(true, 0.65, 99.7, 0, 100, 0)]
    [InlineData(false, 0.65, 99.7, 0, 100, 100)]
    [InlineData(true, 1, 99, 0, 100, 0)]
    [InlineData(false, 1, 99, 0, 100, 100)]
    [InlineData(true, 1250.5, 1299.25, 1250, 1300, 1250)]
    [InlineData(false, 1250.5, 1299.25, 1250, 1300, 1300)]
    [InlineData(true, -9.5, 9.5, -10, 10, -10)]
    public void ExtendsToActualViewBoundaryWithinOneMetre(bool atStart, double start, double end,
        double viewStart, double viewEnd, double target)
    {
        var extension = RequiredExtension(ProfileExtensionRules.Plan(new(start, end), new(viewStart, viewEnd), atStart));
        Assert.Equal(atStart, extension.AtStart);
        Assert.Equal(target, extension.TargetStation);
        Assert.InRange(extension.Gap, 0, 1);
    }

    [Theory]
    [InlineData(true, 1.000001, 99.7)]
    [InlineData(false, 0.3, 98.999999)]
    public void RejectsGapGreaterThanOneMetre(bool atStart, double start, double end)
    {
        string message = Failure(ProfileExtensionRules.Plan(new(start, end), new(0, 100), atStart));
        Assert.Contains("exceeds", message);
        Assert.Contains("m", message);
    }

    [Theory]
    [InlineData(true, 0, 100)]
    [InlineData(false, 0, 100)]
    [InlineData(true, -2, 102)]
    [InlineData(false, -2, 102)]
    [InlineData(true, 0.000000001, 100)]
    public void AlreadyCoveredEndsAreNeverTrimmed(bool atStart, double start, double end) =>
        Assert.True(ProfileExtensionRules.Plan(new(start, end), new(0, 100), atStart)
            .Match(option => option.Match(_ => false, () => true), _ => false));

    [Fact]
    public void ExcessiveStartGapDoesNotPreventValidEndExtension()
    {
        var original = new ProfileStationRange(1.2, 99.7);
        Assert.Contains("exceeds", Failure(ProfileExtensionRules.Plan(original, new(0, 100), true)));
        Assert.Equal(100, RequiredExtension(ProfileExtensionRules.Plan(original, new(0, 100), false)).TargetStation);
    }

    [Fact]
    public void MultipleViewsUseOriginalGapAndMergeEachEndOnce()
    {
        var original = new ProfileStationRange(10.8, 20);
        var views = new[] { new ProfileStationRange(10, 20.5), new(9.9, 20.8), new(9, 21.5) };
        var requests = new List<ProfileEndExtension>();
        var failures = new List<string>();
        foreach (var view in views)
        foreach (bool atStart in new[] { true, false })
            ProfileExtensionRules.Plan(original, view, atStart).Match(option => option.Match(extension =>
            {
                requests.Add(extension);
                return Unit.Value;
            }, () => Unit.Value), error => { failures.Add(error); return Unit.Value; });
        var merged = ProfileExtensionRules.Merge(requests);
        Assert.Equal(2, merged.Length);
        Assert.Equal(9.9, merged.Single(extension => extension.AtStart).TargetStation);
        Assert.Equal(20.8, merged.Single(extension => !extension.AtStart).TargetStation);
        Assert.Equal(2, failures.Count);
        Assert.All(merged, extension => Assert.InRange(extension.Gap, 0, 1));
    }

    [Fact]
    public void RepeatingCompletedExtensionIsANoOp()
    {
        var view = new ProfileStationRange(0, 100);
        foreach (bool atStart in new[] { true, false })
            Assert.True(ProfileExtensionRules.Plan(view, view, atStart)
                .Match(option => option.Match(_ => false, () => true), _ => false));
    }

    [Theory]
    [InlineData(0, 0, 0, 100)]
    [InlineData(10, 1, 0, 100)]
    [InlineData(0.2, 99.8, 10, 10)]
    [InlineData(100.5, 110, 0, 100)]
    [InlineData(0, 10, 10, 20)]
    [InlineData(double.NaN, 100, 0, 100)]
    [InlineData(0, double.PositiveInfinity, 0, 100)]
    public void InvalidOrUnrelatedRangesAreReported(double start, double end, double viewStart, double viewEnd) =>
        Assert.NotEmpty(Failure(ProfileExtensionRules.Plan(new(start, end), new(viewStart, viewEnd), true)));

    [Theory]
    [InlineData(0.5, 10, 0.02, 0, 9.99)]
    [InlineData(99.5, 10, 0.02, 100, 10.01)]
    [InlineData(0.5, 10, -0.02, 0, 10.01)]
    [InlineData(99.5, 10, -0.02, 100, 9.99)]
    [InlineData(99.5, 10, 0, 100, 10)]
    public void ContinuesExistingSignedSlope(double station, double elevation, double grade, double target, double expected) =>
        Assert.Equal(expected, ProfileExtensionRules.ProjectElevation(station, elevation, grade, target)
            .Match(value => value, error => throw new Xunit.Sdk.XunitException(error)), 10);

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void RejectsNonFiniteSlope(double grade) => Assert.True(
        ProfileExtensionRules.ProjectElevation(0.5, 10, grade, 0).Match(_ => false, _ => true));

    private static ProfileEndExtension RequiredExtension(Result<Option<ProfileEndExtension>> result) =>
        result.Match(option => option.Match(extension => extension,
            () => throw new Xunit.Sdk.XunitException("Expected an extension, got already covered.")),
            error => throw new Xunit.Sdk.XunitException(error));

    private static string Failure(Result<Option<ProfileEndExtension>> result) =>
        result.Match(_ => throw new Xunit.Sdk.XunitException("Expected a reported skip."), message => message);
}
