using Norsyn.AecPropertySets;
using System;
using System.Collections.Generic;
using System.Linq;

namespace IntersectUtilities.LongitudinalProfiles.Quality;

internal static class QaLevels
{
    internal static Option<double> ElevationAt(IReadOnlyList<QaSegment> segments, double station) => Highest(
        segments.Where(s => Contains(s.Start, s.End, station)).Select(s => s.At(Math.Clamp(station, s.Start, s.End))));

    internal static Option<double> CasingTopAt(IReadOnlyList<QaSegment> segments, IReadOnlyList<QaPipeSpan> sizes, double station) => Highest(
        from segment in segments
        where Contains(segment.Start, segment.End, station)
        from size in sizes
        where Contains(size.Start, size.End, station) && double.IsFinite(size.Diameter) && size.Diameter > 0
        select segment.At(Math.Clamp(station, segment.Start, segment.End)) + size.Diameter / 2);

    internal static Option<double> Depth(Option<double> surface, Option<double> top) => surface.Match(
        terrain => top.Match(elevation => Option<double>.Of(terrain - elevation), () => Option<double>.Nothing),
        () => Option<double>.Nothing);

    private static bool Contains(double start, double end, double station) => station >= start - QaGeometry.StationTolerance
        && station <= end + QaGeometry.StationTolerance;

    private static Option<double> Highest(IEnumerable<double> values)
    {
        double[] finite = values.Where(double.IsFinite).ToArray();
        return finite.Length == 0 ? Option<double>.Nothing : Option<double>.Of(finite.Max());
    }
}
