using System;
using System.Collections.Generic;
using System.Linq;

namespace IntersectUtilities.LongitudinalProfiles;

internal readonly record struct ProfileViewGrid<T>(T Id, double MinX, double MinY, double MaxX, double MaxY)
{
    internal bool IsValid => double.IsFinite(MinX) && double.IsFinite(MinY) &&
        double.IsFinite(MaxX) && double.IsFinite(MaxY) && MinX < MaxX && MinY < MaxY;

    internal double DistanceTo(double x, double y)
    {
        double dx = Math.Max(Math.Max(MinX - x, 0), x - MaxX);
        double dy = Math.Max(Math.Max(MinY - y, 0), y - MaxY);
        return Math.Sqrt(dx * dx + dy * dy);
    }
}

internal static class ProfileViewProximity
{
    // Return every closest match so overlapping grids and distance ties remain explicit.
    internal static IReadOnlyList<T> FindClosest<T>(
        IEnumerable<ProfileViewGrid<T>> grids, double x, double y, double tolerance)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y) ||
            !double.IsFinite(tolerance) || tolerance < 0)
            return Array.Empty<T>();

        var candidates = grids.Where(grid => grid.IsValid)
            .Select(grid => (grid.Id, Distance: grid.DistanceTo(x, y)))
            .Where(candidate => double.IsFinite(candidate.Distance)).ToArray();
        if (candidates.Length == 0)
            return Array.Empty<T>();

        double minimum = candidates.Min(candidate => candidate.Distance);
        return candidates.Where(candidate => minimum == 0
                ? candidate.Distance == 0
                : candidate.Distance - minimum <= tolerance)
            .Select(candidate => candidate.Id).ToArray();
    }
}
