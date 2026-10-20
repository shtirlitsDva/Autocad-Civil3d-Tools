using System;
using System.Collections.Generic;
using System.Linq;

namespace IntersectUtilities.LER2.AttachStik;

internal readonly record struct StikPoint(double X, double Y, double Z)
{
    public bool IsFinite => double.IsFinite(X) && double.IsFinite(Y) && double.IsFinite(Z);
    public double DistanceXY(StikPoint other) =>
        Math.Sqrt(((X - other.X) * (X - other.X)) + ((Y - other.Y) * (Y - other.Y)));
}

internal readonly record struct StikEdge(StikPoint Start, StikPoint End);
internal sealed record StikAttachment(IReadOnlyList<StikPoint> Points, bool AtStart, double Distance);

// Expected absence is a value. Match requires callers to handle both outcomes.
internal abstract record StikAttachmentResult
{
    private StikAttachmentResult() { }
    public abstract T Match<T>(Func<StikAttachment, T> attached, Func<string, T> skipped);
    public static StikAttachmentResult Attach(StikAttachment value) => new Attached(value);
    public static StikAttachmentResult Skip(string reason) => new Skipped(reason);

    private sealed record Attached(StikAttachment Value) : StikAttachmentResult
    {
        public override T Match<T>(Func<StikAttachment, T> attached, Func<string, T> skipped) => attached(Value);
    }

    private sealed record Skipped(string Reason) : StikAttachmentResult
    {
        public override T Match<T>(Func<StikAttachment, T> attached, Func<string, T> skipped) => skipped(Reason);
    }
}

internal static class StikAttachmentGeometry
{
    internal const double PositionTolerance = 1e-7;
    private const double ElevationTolerance = 1e-6;
    private const double AngularTolerance = 1e-10;

    // Same LER placeholder convention as UtilsCommon.is3D(): near zero and < -98
    // are unknown. A target edge must have TWO real elevations to interpolate Z.
    public static bool IsRealElevation(double z) =>
        double.IsFinite(z) && z >= -98.0 && !(z > -0.0001 && z < 0.0001);

    public static bool IsUsableEdge(StikEdge edge) =>
        edge.Start.IsFinite && edge.End.IsFinite &&
        IsRealElevation(edge.Start.Z) && IsRealElevation(edge.End.Z) &&
        edge.Start.DistanceXY(edge.End) > PositionTolerance;

    public static StikAttachmentResult Solve(
        IReadOnlyList<StikPoint> points, IEnumerable<StikEdge> mainEdges,
        double permille, double maximumDistance)
    {
        if (!double.IsFinite(permille) || !double.IsFinite(maximumDistance) || maximumDistance <= 0)
            return StikAttachmentResult.Skip("Slope must be finite and maximum distance must be positive and finite.");
        if (points.Count < 2 || points.Any(p => !p.IsFinite))
            return StikAttachmentResult.Skip("Branch has too few vertices or invalid coordinates.");

        bool startReal = IsRealElevation(points[0].Z);
        bool endReal = IsRealElevation(points[^1].Z);
        if (startReal && endReal)
            return StikAttachmentResult.Skip("Both endpoints have real elevations.");

        StikEdge[] edges = mainEdges.Where(IsUsableEdge).ToArray();
        List<Hit> hits = new();
        if (startReal || !endReal) AddHits(points, true, edges, maximumDistance, hits);
        if (endReal || !startReal) AddHits(points, false, edges, maximumDistance, hits);
        if (hits.Count == 0)
            return StikAttachmentResult.Skip("No extend/trim intersection with a valid main edge within maximum distance.");

        Hit best = hits.OrderBy(h => Math.Abs(h.Displacement)).First();
        // Shared main vertices with the same XYZ are one connection. Competing
        // ends or different Z values at an equally near crossing are ambiguous.
        if (hits.Any(h => Math.Abs(Math.Abs(h.Displacement) - Math.Abs(best.Displacement)) <= PositionTolerance &&
            (h.AtStart != best.AtStart || h.Point.DistanceXY(best.Point) > PositionTolerance ||
             Math.Abs(h.Point.Z - best.Point.Z) > ElevationTolerance)))
            return StikAttachmentResult.Skip("Equally near connections disagree; resolve this branch manually.");

        StikPoint[] adjusted = points.ToArray();
        int anchor = best.AtStart ? 0 : points.Count - 1;
        adjusted[anchor] = best.Point;
        int step = best.AtStart ? 1 : -1;
        // A trimmed end must also move coincident terminal vertices; leaving
        // them at the old endpoint would retain an overshooting, doubled-back leg.
        if (best.Displacement < -PositionTolerance)
        {
            for (int i = anchor + step; i >= 0 && i < adjusted.Length; i += step)
            {
                if (points[i].DistanceXY(points[anchor]) > PositionTolerance) break;
                adjusted[i] = best.Point;
            }
        }
        double distance = 0;
        for (int i = anchor + step; i >= 0 && i < adjusted.Length; i += step)
        {
            distance += adjusted[i].DistanceXY(adjusted[i - step]);
            adjusted[i] = adjusted[i] with { Z = best.Point.Z + (permille / 1000.0 * distance) };
        }
        if (adjusted.Any(p => !p.IsFinite))
            return StikAttachmentResult.Skip("Calculated elevations are outside the supported numeric range.");
        return StikAttachmentResult.Attach(new StikAttachment(adjusted, best.AtStart, Math.Abs(best.Displacement)));
    }

    private readonly record struct Hit(bool AtStart, StikPoint Point, double Displacement);

    private static void AddHits(
        IReadOnlyList<StikPoint> points, bool atStart, IReadOnlyList<StikEdge> edges,
        double maximumDistance, List<Hit> hits)
    {
        int anchor = atStart ? 0 : points.Count - 1;
        int step = atStart ? 1 : -1;
        StikPoint end = points[anchor];
        for (int i = anchor + step; i >= 0 && i < points.Count; i += step)
        {
            double length = end.DistanceXY(points[i]);
            if (length <= PositionTolerance) continue;
            double ux = (end.X - points[i].X) / length;
            double uy = (end.Y - points[i].Y) / length;
            foreach (StikEdge edge in edges)
                AddIntersection(end, ux, uy, length, atStart, edge, maximumDistance, hits);
            return;
        }
    }

    private static void AddIntersection(
        StikPoint end, double ux, double uy, double terminalLength, bool atStart, StikEdge edge,
        double maximumDistance, List<Hit> hits)
    {
        double sx = edge.End.X - edge.Start.X;
        double sy = edge.End.Y - edge.Start.Y;
        double lengthSquared = sx * sx + sy * sy;
        double length = Math.Sqrt(lengthSquared);

        // Also accept an end already on the interior of an edge, including a
        // collinear end. The main need not contain a vertex at this location.
        double t0 = ((end.X - edge.Start.X) * sx + (end.Y - edge.Start.Y) * sy) / lengthSquared;
        if (t0 >= 0 && t0 <= 1)
        {
            StikPoint onEdge = Interpolate(edge, t0);
            if (end.DistanceXY(onEdge) <= PositionTolerance)
            {
                hits.Add(new Hit(atStart, onEdge, 0));
                return;
            }
        }

        double determinant = Cross(ux, uy, sx, sy);
        if (Math.Abs(determinant) <= AngularTolerance * length) return;
        double qx = edge.Start.X - end.X;
        double qy = edge.Start.Y - end.Y;
        double forward = Cross(qx, qy, sx, sy) / determinant;
        double fraction = Cross(qx, qy, ux, uy) / determinant;
        double fractionTolerance = PositionTolerance / length;
        // Positive displacement extends the terminal segment; negative trims it.
        // Stop before the next distinct branch vertex, keeping its direction and
        // avoiding a collapsed/reversed segment or cutting through a bend.
        if (Math.Abs(forward) > maximumDistance + PositionTolerance ||
            forward <= -terminalLength + PositionTolerance ||
            fraction < -fractionTolerance || fraction > 1 + fractionTolerance) return;

        StikPoint hit = Interpolate(edge, Math.Clamp(fraction, 0, 1));
        hits.Add(new Hit(atStart, hit, Math.Abs(forward) <= PositionTolerance ? 0 : forward));
    }

    private static StikPoint Interpolate(StikEdge edge, double fraction) => new(
        edge.Start.X + fraction * (edge.End.X - edge.Start.X),
        edge.Start.Y + fraction * (edge.End.Y - edge.Start.Y),
        edge.Start.Z + fraction * (edge.End.Z - edge.Start.Z));

    private static double Cross(double ax, double ay, double bx, double by) => ax * by - ay * bx;
}
