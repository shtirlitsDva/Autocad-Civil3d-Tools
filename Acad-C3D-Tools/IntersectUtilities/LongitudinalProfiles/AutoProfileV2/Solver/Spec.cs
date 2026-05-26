using System;
using System.Collections.Generic;
using System.Linq;

namespace AutoProfileSolver.Alignment;

/// <summary>Per-station-range pipe parameters.</summary>
public sealed record PipeSize(double SLo, double SHi, double RMinM, double JodM);

/// <summary>
/// Axis-aligned immovable-utility box in (station, elevation). The centerline must
/// avoid the interior. <paramref name="TopologyKind"/> is "below_only" or "either".
/// </summary>
public sealed record UtilityBox(double SLo, double SHi, double YLo, double YHi,
                                string TopologyKind = "either");

/// <summary>
/// Read-only case bundle with the evaluators the solver consumes
/// (surface, target, r_min, jod, forbidden). Port of spec.py CaseSpec.
/// </summary>
public sealed class CaseSpec
{
    public const double DefaultCoverM = 0.6;

    public string Name { get; }
    public double[] SurfaceS { get; }
    public double[] SurfaceY { get; }
    public IReadOnlyList<PipeSize> PipeSizes { get; }
    public IReadOnlyList<UtilityBox> Utilities { get; }
    public IReadOnlyList<(double Lo, double Hi)> Forbidden { get; }

    public CaseSpec(string name, double[] surfaceS, double[] surfaceY,
                    IReadOnlyList<PipeSize> pipeSizes, IReadOnlyList<UtilityBox> utilities,
                    IReadOnlyList<(double, double)> forbidden)
    {
        if (surfaceS.Length != surfaceY.Length || surfaceS.Length < 2)
            throw new ArgumentException("surface needs matching s/y arrays of length >= 2");
        Name = name; SurfaceS = surfaceS; SurfaceY = surfaceY;
        PipeSizes = pipeSizes; Utilities = utilities; Forbidden = forbidden;
    }

    public double SMin => SurfaceS[0];
    public double SMax => SurfaceS[^1];

    public double SurfaceAt(double s) => Interp(s, SurfaceS, SurfaceY);
    public double[] Surface(double[] s) => s.Select(SurfaceAt).ToArray();

    public double JodMAt(double s) => StepLookup(s, p => p.JodM);
    public double[] JodM(double[] s) => s.Select(JodMAt).ToArray();

    public double RMinAt(double s) => StepLookup(s, p => p.RMinM);
    public double[] RMin(double[] s) => s.Select(RMinAt).ToArray();

    public double TargetAt(double s, double coverM = DefaultCoverM)
        => SurfaceAt(s) - coverM - JodMAt(s) / 2.0;
    public double[] Target(double[] s, double coverM = DefaultCoverM)
        => s.Select(x => TargetAt(x, coverM)).ToArray();

    public bool[] ForbiddenMask(double[] s)
    {
        var m = new bool[s.Length];
        for (int i = 0; i < s.Length; i++)
            foreach (var (lo, hi) in Forbidden)
                if (s[i] >= lo && s[i] <= hi) { m[i] = true; break; }
        return m;
    }

    /// <summary>Step-function lookup, with nearest-range fallback outside all ranges
    /// (matches spec.py _step_lookup).</summary>
    private double StepLookup(double s, Func<PipeSize, double> sel)
    {
        foreach (var ps in PipeSizes)
            if (s >= ps.SLo && s <= ps.SHi) return sel(ps);
        PipeSize nearest = PipeSizes
            .OrderBy(p => Math.Min(Math.Abs(s - p.SLo), Math.Abs(s - p.SHi)))
            .First();
        return sel(nearest);
    }

    /// <summary>Linear interpolation with endpoint clamping (numpy.interp semantics),
    /// xs assumed ascending.</summary>
    public static double Interp(double x, double[] xs, double[] ys)
    {
        int n = xs.Length;
        if (x <= xs[0]) return ys[0];
        if (x >= xs[n - 1]) return ys[n - 1];
        int lo = 0, hi = n - 1;            // binary search for the bracketing interval
        while (hi - lo > 1)
        {
            int mid = (lo + hi) >> 1;
            if (xs[mid] <= x) lo = mid; else hi = mid;
        }
        double t = (x - xs[lo]) / (xs[hi] - xs[lo]);
        return ys[lo] + t * (ys[hi] - ys[lo]);
    }

    /// <summary>Uniform station grid over [SMin, SMax] at step ~ds (endpoints exact).</summary>
    public double[] MakeStationGrid(double ds = 0.5)
    {
        double s0 = SMin, s1 = SMax;
        int n = Math.Max(2, (int)Math.Round((s1 - s0) / ds) + 1);
        var g = new double[n];
        for (int i = 0; i < n; i++) g[i] = s0 + (s1 - s0) * (i / (double)(n - 1));
        return g;
    }
}
