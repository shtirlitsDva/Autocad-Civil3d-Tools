using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using System;
using System.Collections.Generic;
using System.Linq;

namespace IntersectUtilities.NdhTrace;

/// <summary>
/// Arc fitting. A legacy drafter often drew a large elastic bend as a polygon
/// of short straights. Each run of such corners is replaced by the fewest arcs
/// that stay within <see cref="MaxTraceDeviation"/> of the trace, each one
/// tangent to the straights on either side of it.
/// </summary>
internal static partial class NdhRouteBuilder
{
    //The trace is compared with a fitted arc at points this far apart.
    private const double ArcFitSampleStep = 0.5;
    //A span whose best arc is this many tolerances off only gets worse as it
    //grows: stop extending it.
    private const double ArcFitGiveUp = 2.0;
    //Radii tried log-spaced down to this fraction of the largest the legs
    //allow, before the best of them is refined by golden section.
    private const int ArcFitScanCount = 24;
    private const double ArcFitSmallestRadius = 1e-3;
    private const int ArcFitRefineSteps = 40;

    /// <summary>
    /// One piece a fitted run may be made of: an arc tangent to legs
    /// <see cref="P"/> and <see cref="Q"/> (leg k runs from vertex k to k+1),
    /// standing for the corners between them. Q = P + 1 is the corner as it is.
    /// </summary>
    private sealed class ArcPiece
    {
        public int P, Q;
        public Point2d Pi;
        public double Radius;
        //Where the piece starts on leg P and ends on leg Q, measured from the
        //leg's first vertex.
        public double Start, End;
        public double Deviation;
        public bool Kept => Q == P + 1;
    }

    /// <summary>The trace as points at most <see cref="ArcFitSampleStep"/> apart, with their distances.</summary>
    private sealed class TraceSamples
    {
        public readonly double[] D;
        public readonly Point2d[] P;

        public TraceSamples(Polyline cl)
        {
            List<double> ds = new List<double>();
            for (int i = 0; i < cl.NumberOfVertices - 1; i++)
            {
                double d0 = cl.GetDistanceAtParameter(i), d1 = cl.GetDistanceAtParameter(i + 1);
                int n = Math.Max(1, (int)Math.Ceiling((d1 - d0) / ArcFitSampleStep));
                for (int k = 0; k < n; k++) ds.Add(d0 + (d1 - d0) * k / n);
            }
            ds.Add(cl.Length);
            D = ds.ToArray();
            P = D.Select(d =>
            {
                Point3d p = cl.GetPointAtDist(Math.Min(d, cl.Length));
                return new Point2d(p.X, p.Y);
            }).ToArray();
        }

        /// <summary>The first and last sample from distance <paramref name="d0"/> to <paramref name="d1"/>.</summary>
        public (int From, int To) Window(double d0, double d1)
        {
            int from = Array.BinarySearch(D, d0);
            if (from < 0) from = ~from;
            int to = Array.BinarySearch(D, d1);
            if (to < 0) to = ~to - 1;
            return (from, to);
        }
    }

    /// <summary>
    /// Replaces every run of two or more consecutive elastic corners (bends and
    /// legacy arcs) by the fewest arcs that follow the trace within
    /// <see cref="MaxTraceDeviation"/>. A fitted arc never covers an identity
    /// change: the change needs a straight to stand on.
    /// </summary>
    private static void FitArcs(
        List<RouteVertex> vs, Polyline centreline, IReadOnlyList<LegacyIdentitySpan> identities, List<string> notes)
    {
        List<(int A, int B)> runs = new List<(int, int)>();
        for (int i = 1; i < vs.Count - 1;)
        {
            if (!IsFittable(vs[i])) { i++; continue; }
            int j = i;
            while (j + 1 < vs.Count - 1 && IsFittable(vs[j + 1])) j++;
            if (j > i) runs.Add((i, j));
            i = j + 1;
        }
        if (runs.Count == 0) return;

        TraceSamples trace = new TraceSamples(centreline);
        double[] changes = identities.Skip(1).Select(s => s.StartDist).ToArray();
        //From the last run back, so a fitted run leaves the earlier runs' indices alone.
        for (int r = runs.Count - 1; r >= 0; r--)
            FitRun(vs, runs[r].A, runs[r].B, trace, centreline, changes, notes);
    }

    private static bool IsFittable(RouteVertex v) => v.Kind is VertexKind.Bend or VertexKind.Fillet;

    /// <summary>
    /// Fits the run of corners <paramref name="a"/>..<paramref name="b"/>: every
    /// arc that can stand for a span of them is a candidate, and the chain of
    /// candidates from leg a-1 to leg b with the fewest pieces - then the least
    /// deviation - wins. Pieces on a shared leg may not overlap.
    /// </summary>
    private static void FitRun(
        List<RouteVertex> vs, int a, int b, TraceSamples trace, Polyline cl, double[] changes, List<string> notes)
    {
        List<ArcPiece> pieces = new List<ArcPiece>();
        for (int k = a; k <= b; k++) pieces.Add(KeptPiece(vs, k));
        for (int p = a - 1; p <= b - 2; p++)
            for (int q = p + 2; q <= b; q++)
            {
                ArcPiece? piece = FitArc(vs, p, q, trace, cl, changes, out double off);
                if (piece != null) pieces.Add(piece);
                else if (off > ArcFitGiveUp * MaxTraceDeviation) break;
            }
        if (pieces.All(x => x.Kept)) return;

        //A corner kept as it is needs no room check: the run as it was is always a way through.
        double roomIn = vs[a - 1].Kind == VertexKind.End ? BoundaryRoom : 0.0;
        double roomOut = vs[b + 1].Kind == VertexKind.End ? BoundaryRoom : 0.0;
        double lastLeg = vs[b].P.GetDistanceTo(vs[b + 1].P);
        Dictionary<ArcPiece, (int Count, double Deviation, ArcPiece? Before)> best = new();
        foreach (ArcPiece c in pieces.OrderBy(x => x.Q).ThenBy(x => x.P))
        {
            if (c.P == a - 1)
            {
                if (c.Kept || c.Start >= roomIn + LegSlack) best[c] = (1, c.Deviation, null);
                continue;
            }
            foreach (ArcPiece before in pieces)
            {
                if (before.Q != c.P || !best.TryGetValue(before, out var reach)) continue;
                if (!(before.Kept && c.Kept) && before.End + LegSlack > c.Start) continue;
                if (!best.TryGetValue(c, out var now) || reach.Count + 1 < now.Count ||
                    (reach.Count + 1 == now.Count && reach.Deviation + c.Deviation < now.Deviation))
                    best[c] = (reach.Count + 1, reach.Deviation + c.Deviation, before);
            }
        }

        ArcPiece? last = null;
        foreach ((ArcPiece c, (int Count, double Deviation, ArcPiece? Before) reach) in best)
        {
            if (c.Q != b || (!c.Kept && c.End > lastLeg - roomOut - LegSlack)) continue;
            if (last == null || reach.Count < best[last].Count ||
                (reach.Count == best[last].Count && reach.Deviation < best[last].Deviation))
                last = c;
        }

        //Back from the end, so each replacement leaves the ones before it in place.
        for (ArcPiece? c = last; c != null; c = best[c].Before)
        {
            if (c.Kept) continue;
            double d0 = vs[c.P + 1].D0, d1 = vs[c.Q].D1;
            notes.Add($"{c.Q - c.P} corners from {d0:F2} to {d1:F2} m fitted as one arc " +
                $"R={c.Radius:F2}, within {c.Deviation:F3} m of the trace");
            vs.RemoveRange(c.P + 1, c.Q - c.P);
            vs.Insert(c.P + 1, new RouteVertex
            { P = c.Pi, Kind = VertexKind.Fillet, Radius = c.Radius, D0 = d0, D1 = d1 });
        }
    }

    /// <summary>Corner <paramref name="k"/> as it is: a legacy arc keeps its radius, a bend is sized later.</summary>
    private static ArcPiece KeptPiece(List<RouteVertex> vs, int k)
    {
        double setback = FilletSetback(vs, k);
        return new ArcPiece
        {
            P = k - 1,
            Q = k,
            Pi = vs[k].P,
            Radius = vs[k].Radius,
            Start = vs[k - 1].P.GetDistanceTo(vs[k].P) - setback,
            End = setback,
        };
    }

    /// <summary>
    /// The arc tangent to legs <paramref name="p"/> and <paramref name="q"/>
    /// that follows the trace between them best, if that is within
    /// <see cref="MaxTraceDeviation"/> and covers no identity change. Null
    /// otherwise; <paramref name="off"/> then says how far off the best was
    /// (infinite when no arc can stand there at all).
    /// </summary>
    private static ArcPiece? FitArc(
        List<RouteVertex> vs, int p, int q, TraceSamples trace, Polyline cl, double[] changes, out double off)
    {
        off = double.PositiveInfinity;
        Point2d from = vs[p].P, to = vs[q + 1].P;
        Vector2d dp = (vs[p + 1].P - from).GetNormal();
        Vector2d dq = (to - vs[q].P).GetNormal();

        double sweep = 0.0;
        for (int k = p + 1; k <= q; k++) sweep += Turn(vs[k].P - vs[k - 1].P, vs[k + 1].P - vs[k].P);
        if (Math.Abs(sweep) > MaxArcSweep) return null;
        double turn = Turn(dp, dq);
        if (Math.Abs(turn) < MinFittingTurn * 0.01 || !TryIntersect(from, dp, to, dq, out Point2d pi)) return null;
        double reachP = (pi - from).DotProduct(dp), reachQ = (to - pi).DotProduct(dq);
        if (reachP <= 0.0 || reachQ <= 0.0) return null;

        double half = Math.Abs(turn) / 2.0;
        double rMax = Math.Min(reachP, reachQ) / Math.Tan(half);
        (int s0, int s1) = trace.Window(vs[p].D1, vs[q + 1].D0);
        if (s1 - s0 < 2) return null;
        double Off(double r) => CurveOff(trace, s0, s1, from, to, pi, dp, dq, turn, r);

        //Log-spaced scan of the radius, then golden section around the best.
        double[] rs = new double[ArcFitScanCount];
        int bestK = 0;
        double bestOff = double.PositiveInfinity;
        for (int k = 0; k < ArcFitScanCount; k++)
        {
            rs[k] = rMax * Math.Pow(ArcFitSmallestRadius, 1.0 - (double)k / (ArcFitScanCount - 1));
            double o = Off(rs[k]);
            if (o < bestOff) { bestOff = o; bestK = k; }
        }
        const double golden = 0.6180339887498949;
        double lo = rs[Math.Max(0, bestK - 1)], hi = rs[Math.Min(ArcFitScanCount - 1, bestK + 1)];
        double x1 = hi - golden * (hi - lo), x2 = lo + golden * (hi - lo);
        double f1 = Off(x1), f2 = Off(x2);
        for (int k = 0; k < ArcFitRefineSteps; k++)
        {
            if (f1 < f2) { hi = x2; x2 = x1; f2 = f1; x1 = hi - golden * (hi - lo); f1 = Off(x1); }
            else { lo = x1; x1 = x2; f1 = f2; x2 = lo + golden * (hi - lo); f2 = Off(x2); }
        }
        double radius = f1 < f2 ? x1 : x2;
        off = Math.Min(f1, f2);
        if (bestOff < off) { radius = rs[bestK]; off = bestOff; }
        if (off > MaxTraceDeviation) return null;

        //The other way round too: the arc may not bulge away from the trace.
        double t = radius * Math.Tan(half);
        Point2d start = pi - dp * t, end = pi + dq * t;
        Point2d centre = start + (turn > 0.0 ? Perpendicular(dp) : Perpendicular(dp).Negate()) * radius;
        double back = ArcOff(trace, s0, s1, centre, radius, start, turn);
        off = Math.Max(off, back);
        if (back > MaxTraceDeviation) return null;

        double d0 = DistAt(cl, start), d1 = DistAt(cl, end);
        if (changes.Any(c => c > Math.Min(d0, d1) - BoundaryRoom && c < Math.Max(d0, d1) + BoundaryRoom))
        {
            off = double.PositiveInfinity;
            return null;
        }

        return new ArcPiece
        {
            P = p,
            Q = q,
            Pi = pi,
            Radius = radius,
            Start = (start - from).DotProduct(dp),
            End = (end - vs[q].P).DotProduct(dq),
            Deviation = off,
        };
    }

    /// <summary>
    /// How far the trace samples <paramref name="s0"/>..<paramref name="s1"/>
    /// stray, at most, from the route of an arc of radius <paramref name="r"/>
    /// at <paramref name="pi"/> with its straights to <paramref name="from"/>
    /// and <paramref name="to"/>.
    /// </summary>
    private static double CurveOff(
        TraceSamples trace, int s0, int s1, Point2d from, Point2d to,
        Point2d pi, Vector2d dp, Vector2d dq, double turn, double r)
    {
        double t = r * Math.Tan(Math.Abs(turn) / 2.0);
        Point2d start = pi - dp * t, end = pi + dq * t;
        Point2d centre = start + (turn > 0.0 ? Perpendicular(dp) : Perpendicular(dp).Negate()) * r;
        double worst = 0.0;
        for (int k = s0; k <= s1; k++)
        {
            Point2d x = trace.P[k];
            double d = Math.Min(ArcDistance(x, centre, r, start, end, turn),
                Math.Min(SegmentDistance(x, from, start), SegmentDistance(x, end, to)));
            if (d > worst) worst = d;
        }
        return worst;
    }

    /// <summary>How far the arc strays, at most, from the trace samples <paramref name="s0"/>..<paramref name="s1"/>.</summary>
    private static double ArcOff(
        TraceSamples trace, int s0, int s1, Point2d centre, double r, Point2d start, double turn)
    {
        int n = Math.Max(4, (int)Math.Ceiling(r * Math.Abs(turn) / ArcFitSampleStep));
        double worst = 0.0;
        for (int k = 0; k <= n; k++)
        {
            Point2d x = Rotate(start, centre, turn * k / n);
            double d = double.PositiveInfinity;
            for (int j = s0; j < s1; j++) d = Math.Min(d, SegmentDistance(x, trace.P[j], trace.P[j + 1]));
            if (d > worst) worst = d;
        }
        return worst;
    }

    private static double ArcDistance(Point2d x, Point2d centre, double r, Point2d start, Point2d end, double turn)
    {
        double a = Turn(start - centre, x - centre);
        bool on = turn > 0.0 ? a >= 0.0 && a <= turn : a <= 0.0 && a >= turn;
        return on
            ? Math.Abs(x.GetDistanceTo(centre) - r)
            : Math.Min(x.GetDistanceTo(start), x.GetDistanceTo(end));
    }

    private static double SegmentDistance(Point2d x, Point2d a, Point2d b)
    {
        Vector2d ab = b - a;
        double len2 = ab.LengthSqrd;
        double t = len2 < 1e-18 ? 0.0 : Math.Max(0.0, Math.Min(1.0, (x - a).DotProduct(ab) / len2));
        return x.GetDistanceTo(a + ab * t);
    }
}
