using Autodesk.AutoCAD.Geometry;

namespace IntersectUtilities.MPE.PipePlanDE;

/// <summary>
/// Pure geometry for PDANNOTATE: splits each straight (tangent-to-tangent) drawn segment into
/// aligned-dimension sub-spans at the points where other centrelines cross it. Crossings are
/// supplied by the caller (computed with <c>IntersectWithValidation</c> on the real drawn
/// polylines); this only does the on-segment projection + split so it stays DB-free and testable.
/// </summary>
internal static class PipePlanDEAnnotationGeometry
{
    private const double ParamTol = 1e-4;
    // A crossing must lie within this XY distance of the segment to split it (crossings on an
    // arc, or on a different straight, project farther and are ignored).
    private const double OnSegmentTol = 0.05;

    /// <summary>
    /// For each straight drawn segment (a tangent-to-tangent start/end pair), returns the
    /// aligned-dimension spans: the segment split at any crossing that lies on it, dropping
    /// degenerate sub-spans.
    /// </summary>
    public static List<(Point3d Start, Point3d End)> SplitSegments(
        IReadOnlyList<(Point3d Start, Point3d End)> segments,
        IReadOnlyList<Point3d> crossings,
        double minSpan = 1e-3)
    {
        List<(Point3d Start, Point3d End)> result = new();
        foreach ((Point3d a, Point3d b) in segments)
        {
            SplitOne(a, b, crossings, minSpan, result);
        }

        return result;
    }

    private static void SplitOne(Point3d a, Point3d b, IReadOnlyList<Point3d> crossings, double minSpan, List<(Point3d, Point3d)> result)
    {
        double abx = b.X - a.X;
        double aby = b.Y - a.Y;
        double len2 = (abx * abx) + (aby * aby);
        if (len2 < minSpan * minSpan)
        {
            return;
        }

        List<double> splits = new();
        foreach (Point3d x in crossings)
        {
            double t = (((x.X - a.X) * abx) + ((x.Y - a.Y) * aby)) / len2;
            if (t <= ParamTol || t >= 1.0 - ParamTol)
            {
                continue;
            }

            double projX = a.X + (abx * t);
            double projY = a.Y + (aby * t);
            double dxy = Math.Sqrt(((projX - x.X) * (projX - x.X)) + ((projY - x.Y) * (projY - x.Y)));
            if (dxy <= OnSegmentTol)
            {
                splits.Add(t);
            }
        }

        splits.Sort();

        List<double> ts = [0.0];
        foreach (double t in splits)
        {
            if (t - ts[^1] > ParamTol)
            {
                ts.Add(t);
            }
        }

        if (1.0 - ts[^1] > ParamTol)
        {
            ts.Add(1.0);
        }
        else
        {
            ts[^1] = 1.0;
        }

        for (int k = 0; k < ts.Count - 1; k++)
        {
            Point3d s = new(a.X + (abx * ts[k]), a.Y + (aby * ts[k]), a.Z);
            Point3d e = new(a.X + (abx * ts[k + 1]), a.Y + (aby * ts[k + 1]), a.Z);
            if (s.DistanceTo(e) >= minSpan)
            {
                result.Add((s, e));
            }
        }
    }
}
