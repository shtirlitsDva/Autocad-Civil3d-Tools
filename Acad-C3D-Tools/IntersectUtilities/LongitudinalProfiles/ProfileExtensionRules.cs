using Norsyn.AecPropertySets;

namespace IntersectUtilities.LongitudinalProfiles;

internal readonly record struct ProfileStationRange(double Start, double End);
internal readonly record struct ProfileEndExtension(bool AtStart, double OriginalStation, double TargetStation)
{
    internal double Gap => Math.Abs(TargetStation - OriginalStation);
    internal string EndName => AtStart ? "START" : "END";
}

internal static class ProfileExtensionRules
{
    internal const double MaximumGap = 1.0;
    internal const double StationTolerance = 1e-8;

    internal static bool IsPipeProfile(string name) => name
        .Split(new[] { ' ', '_', '-' }, StringSplitOptions.RemoveEmptyEntries)
        .Any(token => token.Equals("TOP", StringComparison.OrdinalIgnoreCase)
            || token.Equals("MIDT", StringComparison.OrdinalIgnoreCase)
            || token.Equals("BUND", StringComparison.OrdinalIgnoreCase));

    // None means this end already covers the view; a fault is a reportable skip.
    internal static Result<Option<ProfileEndExtension>> Plan(
        ProfileStationRange profile, ProfileStationRange view, bool atStart)
    {
        if (!ValidRange(profile) || !ValidRange(view))
            return Result<Option<ProfileEndExtension>>.Failure("Invalid or empty station range.");
        if (Math.Min(profile.End, view.End) - Math.Max(profile.Start, view.Start) <= StationTolerance)
            return Result<Option<ProfileEndExtension>>.Failure("The profile does not overlap this view.");

        double station = atStart ? profile.Start : profile.End;
        double target = atStart ? view.Start : view.End;
        double gap = atStart ? station - target : target - station;
        if (gap <= StationTolerance)
            return Result<Option<ProfileEndExtension>>.Success(Option<ProfileEndExtension>.Nothing);
        if (gap > MaximumGap + StationTolerance)
            return Result<Option<ProfileEndExtension>>.Failure(
                $"Gap {gap:F6} m exceeds the {MaximumGap:F3} m tolerance.");
        return Result<Option<ProfileEndExtension>>.Success(
            Option<ProfileEndExtension>.Of(new(atStart, station, target)));
    }

    // All requests must be planned against the original range, before any writes.
    internal static ProfileEndExtension[] Merge(IEnumerable<ProfileEndExtension> requests) => requests
        .GroupBy(request => request.AtStart)
        .Select(group => group.OrderByDescending(request => request.Gap).First())
        .OrderByDescending(request => request.AtStart).ToArray();

    internal static Result<double> ProjectElevation(double station, double elevation, double grade, double target)
    {
        double projected = elevation + grade * (target - station);
        return double.IsFinite(station) && double.IsFinite(elevation) && double.IsFinite(grade)
            && double.IsFinite(target) && double.IsFinite(projected)
            ? Result<double>.Success(projected)
            : Result<double>.Failure("The endpoint elevation or slope is not finite.");
    }

    private static bool ValidRange(ProfileStationRange range) =>
        double.IsFinite(range.Start) && double.IsFinite(range.End)
        && range.End - range.Start > StationTolerance;
}
