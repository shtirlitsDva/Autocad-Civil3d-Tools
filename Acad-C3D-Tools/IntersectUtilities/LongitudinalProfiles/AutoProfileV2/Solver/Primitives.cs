using System;
using System.Collections.Generic;
using System.Linq;

namespace AutoProfileSolver.Alignment;

/// <summary>A 2D point in the station–elevation plane (X = station, Y = elevation).</summary>
public readonly record struct Pt(double X, double Y)
{
    public static Pt operator +(Pt a, Pt b) => new(a.X + b.X, a.Y + b.Y);
    public static Pt operator -(Pt a, Pt b) => new(a.X - b.X, a.Y - b.Y);
    public static Pt operator *(Pt a, double k) => new(a.X * k, a.Y * k);
    public double Hypot => Math.Sqrt(X * X + Y * Y);
    public double Dot(Pt o) => X * o.X + Y * o.Y;
    public double Cross(Pt o) => X * o.Y - Y * o.X;

    /// <summary>Unit vector; (1,0) for a zero vector.</summary>
    public Pt Unit()
    {
        double n = Hypot;
        return n < 1e-30 ? new Pt(1.0, 0.0) : new Pt(X / n, Y / n);
    }
}

/// <summary>A chain primitive: a straight <see cref="Line"/> or a circular <see cref="Arc"/>.</summary>
public interface IPrimitive
{
    int StartIdx { get; set; }
    int EndIdx { get; set; }

    /// <summary>Sample <paramref name="num"/> points along the primitive (start..end inclusive).</summary>
    Pt[] Points(int num);

    /// <summary>Unit tangent at the start, pointing forward along the chain.</summary>
    Pt TangentAtStart();

    /// <summary>Unit tangent at the end, pointing forward along the chain.</summary>
    Pt TangentAtEnd();
}

/// <summary>
/// Circular arc covering source indices [StartIdx, EndIdx]. <paramref name="SweepAngle"/> is
/// signed (positive = counter-clockwise). Forward tangent convention matches the Python
/// segmenter: sign(sweep)·(−sin a, cos a) at polar angle a.
/// </summary>
public sealed class Arc : IPrimitive
{
    public Pt Center;
    public double Radius;
    public double StartAngle;
    public double SweepAngle;
    public int StartIdx { get; set; }
    public int EndIdx { get; set; }

    public Arc(Pt center, double radius, double startAngle, double sweepAngle,
               int startIdx = 0, int endIdx = 0)
    {
        Center = center; Radius = radius; StartAngle = startAngle; SweepAngle = sweepAngle;
        StartIdx = startIdx; EndIdx = endIdx;
    }

    public Pt[] Points(int num)
    {
        if (num < 2) num = 2;
        var pts = new Pt[num];
        for (int i = 0; i < num; i++)
        {
            double a = StartAngle + SweepAngle * (i / (double)(num - 1));
            pts[i] = new Pt(Center.X + Radius * Math.Cos(a), Center.Y + Radius * Math.Sin(a));
        }
        return pts;
    }

    private Pt TangentAt(double angle)
    {
        double s = SweepAngle != 0.0 ? Math.Sign(SweepAngle) : 1.0;
        return new Pt(-s * Math.Sin(angle), s * Math.Cos(angle));
    }

    public Pt TangentAtStart() => TangentAt(StartAngle);
    public Pt TangentAtEnd() => TangentAt(StartAngle + SweepAngle);
}

/// <summary>Straight chord from <see cref="P0"/> to <see cref="P1"/>.</summary>
public sealed class Line : IPrimitive
{
    public Pt P0;
    public Pt P1;
    public int StartIdx { get; set; }
    public int EndIdx { get; set; }

    public Line(Pt p0, Pt p1, int startIdx = 0, int endIdx = 0)
    {
        P0 = p0; P1 = p1; StartIdx = startIdx; EndIdx = endIdx;
    }

    public Pt[] Points(int num)
    {
        if (num < 2) num = 2;
        var pts = new Pt[num];
        for (int i = 0; i < num; i++)
        {
            double t = i / (double)(num - 1);
            pts[i] = new Pt(P0.X + (P1.X - P0.X) * t, P0.Y + (P1.Y - P0.Y) * t);
        }
        return pts;
    }

    private Pt Dir() => (P1 - P0).Unit();
    public Pt TangentAtStart() => Dir();
    public Pt TangentAtEnd() => Dir();
}

/// <summary>Ordered list of primitives covering [0, N-1] of the source grid end-to-end.</summary>
public sealed class Chain
{
    public List<IPrimitive> Primitives { get; }

    public Chain(IEnumerable<IPrimitive>? primitives = null)
        => Primitives = primitives?.ToList() ?? new List<IPrimitive>();

    public int NArcs => Primitives.Count(p => p is Arc);
    public int NLines => Primitives.Count(p => p is Line);

    /// <summary>Max tangent mismatch (radians, [0, π]) across all interior seams — the C¹ defect.</summary>
    public double C1MaxRad()
    {
        double max = 0.0;
        for (int k = 0; k < Primitives.Count - 1; k++)
        {
            Pt tOut = Primitives[k].TangentAtEnd();
            Pt tIn = Primitives[k + 1].TangentAtStart();
            double d = Math.Clamp(tOut.Dot(tIn), -1.0, 1.0);
            double ang = Math.Acos(d);
            if (ang > max) max = ang;
        }
        return max;
    }

    /// <summary>Verify contiguous coverage of [0, nPoints-1]; throws on a gap/overlap.</summary>
    public void CheckCoverage(int nPoints)
    {
        if (Primitives.Count == 0)
        {
            if (nPoints > 1) throw new InvalidOperationException($"empty chain cannot cover {nPoints} points");
            return;
        }
        if (Primitives[0].StartIdx != 0)
            throw new InvalidOperationException($"chain does not start at index 0 (starts at {Primitives[0].StartIdx})");
        if (Primitives[^1].EndIdx != nPoints - 1)
            throw new InvalidOperationException($"chain does not end at index {nPoints - 1} (ends at {Primitives[^1].EndIdx})");
        for (int k = 0; k < Primitives.Count - 1; k++)
            if (Primitives[k].EndIdx != Primitives[k + 1].StartIdx)
                throw new InvalidOperationException(
                    $"gap/overlap: a.EndIdx={Primitives[k].EndIdx}, b.StartIdx={Primitives[k + 1].StartIdx}");
    }
}
