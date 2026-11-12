using IntersectUtilities.LongitudinalProfiles.Quality;

public sealed class ProfileQaRadiusAcceptanceTests
{
    private static QaRadiusLimit Limit(QaPipeSpan pipe) => ProfileQaRadii.Get(pipe, true).Match(rule => rule,
        message => throw new InvalidOperationException(message));

    [Theory]
    [InlineData(164.0, "OK")]
    [InlineData(163.9, "REVIEW")]
    [InlineData(137.0, "REVIEW")]
    [InlineData(136.99, "FAIL")]
    public void SteelTargetUsesWholeMetresWithoutRoundingTheActualRadius(double radius, string expected)
    {
        var pipe = new QaPipeSpan(0, 10, 250, "Stål", "Enkelt", "S2", 0.45);
        var rule = Limit(pipe);
        Assert.Equal(expected, ProfileQaRadii.Status(radius, rule, pipe));
        Assert.Equal(164.4, rule.Target, 8);
        Assert.Equal(137.0, rule.Minimum, 8);
    }

    [Theory]
    [InlineData(65, 172.0, "OK")]
    [InlineData(65, 171.99, "FAIL")]
    [InlineData(65, 171.999998, "OK")]
    [InlineData(50, 140.6, "FAIL")]
    [InlineData(50, 141.0, "OK")]
    public void SteelMinimumRoundsToTheNearestWholeMetre(int dn, double radius, string expected)
    {
        var pipe = new QaPipeSpan(0, 10, dn, "Stål", "Twin", "S2", 0.25);
        var rule = Limit(pipe);
        Assert.False(rule.HasTarget);
        Assert.Equal(expected, ProfileQaRadii.Status(radius, rule, pipe));
        Assert.Equal(dn == 65 ? 172.2 : 140.6, rule.Minimum, 8);
    }

    [Fact]
    public void SteelTargetAlsoRoundsUpWhenItsFractionIsGreaterThanHalf()
    {
        var pipe = new QaPipeSpan(0, 10, 40, "Stål", "Enkelt", "S2", 0.2);
        var rule = Limit(pipe);
        Assert.Equal(28.8, rule.Target, 8);
        Assert.Equal("REVIEW", ProfileQaRadii.Status(28.8, rule, pipe));
        Assert.Equal("OK", ProfileQaRadii.Status(29.0, rule, pipe));
    }

    [Theory]
    [InlineData("AluPex", 63, 5.0, "FAIL")]
    [InlineData("AluPex", 63, 5.2, "OK")]
    [InlineData("PertFlextra", 40, 8.0, "FAIL")]
    [InlineData("PertFlextra", 40, 8.3, "REVIEW")]
    [InlineData("PertFlextra", 40, 10.9, "REVIEW")]
    [InlineData("PertFlextra", 40, 11.0, "OK")]
    public void OtherPipeSystemsRetainTheirFullPrecisionLimits(string system, int dn, double radius, string expected)
    {
        var pipe = new QaPipeSpan(0, 10, dn, system, "Twin", "S2", 0.2);
        Assert.Equal(expected, ProfileQaRadii.Status(radius, Limit(pipe), pipe));
    }
}
