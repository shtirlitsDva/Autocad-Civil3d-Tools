using System;

namespace AutoProfileSolver.Alignment;

/// <summary>Arc/biarc construction helpers (port of solver.py _arc_through / _biarc).</summary>
public static class Geometry
{
    private const double TwoPi = 2.0 * Math.PI;

    /// <summary>
    /// Circular arc starting at <paramref name="pa"/> with start tangent <paramref name="ta"/>
    /// (segmenter sign convention) and ending at <paramref name="pb"/>. Returns null when the
    /// data is straight (centre at infinity). Indices are placeholders; the caller assigns them.
    /// </summary>
    public static Arc? ArcThrough(Pt pa, Pt ta, Pt pb, double eps = 1e-9)
    {
        ta = ta.Unit();
        Pt normal = new(-ta.Y, ta.X);               // left normal of the tangent
        Pt w = pa - pb;
        double nw = normal.Dot(w);
        if (Math.Abs(nw) < eps) return null;        // pa, pb symmetric about ta → straight
        double r = -w.Dot(w) / (2.0 * nw);          // signed radius along the normal
        Pt center = pa + normal * r;
        double radius = Math.Abs(r);
        double a0 = Math.Atan2(pa.Y - center.Y, pa.X - center.X);
        double a1 = Math.Atan2(pb.Y - center.Y, pb.X - center.X);
        Pt tccw = new(-Math.Sin(a0), Math.Cos(a0)); // CCW tangent at the start point
        double raw = ((a1 - a0) % TwoPi + TwoPi) % TwoPi;   // CCW sweep landing on pb, in [0, 2π)
        // Pick the branch whose forward tangent (sign(sweep)·tccw) matches ta exactly.
        double sweep = tccw.Dot(ta) > 0.0 ? raw : raw - TwoPi;
        return new Arc(center, radius, a0, sweep);
    }

    /// <summary>
    /// Equal-tangent biarc interpolating endpoint + tangent at both ends: two circular arcs
    /// meeting at a joint with a shared tangent (tangent-continuous by construction). Returns
    /// the two arcs, or null if the data is degenerate (caller falls back to a line).
    /// </summary>
    public static Arc[]? Biarc(Pt p1, Pt t1, Pt p2, Pt t2, double eps = 1e-9)
    {
        t1 = t1.Unit();
        t2 = t2.Unit();
        Pt v = p2 - p1;
        double a = 2.0 * (1.0 - t1.Dot(t2));
        double b = 2.0 * v.Dot(t1 + t2);
        double c = -v.Dot(v);
        double d;
        if (Math.Abs(a) < eps)                      // tangents parallel → linear in d
        {
            if (Math.Abs(b) < eps) return null;
            d = -c / b;
        }
        else
        {
            double disc = b * b - 4.0 * a * c;
            if (disc < 0.0) return null;
            d = (-b + Math.Sqrt(disc)) / (2.0 * a);
            if (d <= 0.0)
            {
                double alt = (-b - Math.Sqrt(disc)) / (2.0 * a);
                if (alt > 0.0) d = alt;
            }
        }
        if (!double.IsFinite(d) || d <= 0.0) return null;

        Pt joint = (p1 + t1 * d + (p2 - t2 * d)) * 0.5;
        Arc? arc1 = ArcThrough(p1, t1, joint);
        if (arc1 is null) return null;
        Pt tJoint = arc1.TangentAtEnd();            // shared tangent at the joint
        Arc? arc2 = ArcThrough(joint, tJoint, p2);
        if (arc2 is null) return null;
        return new[] { arc1, arc2 };
    }
}
