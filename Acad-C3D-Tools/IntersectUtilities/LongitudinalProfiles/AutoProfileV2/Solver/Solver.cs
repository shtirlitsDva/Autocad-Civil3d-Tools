using System;
using System.Collections.Generic;
using System.Linq;

namespace AutoProfileSolver.Alignment;

/// <summary>Output of <see cref="Solver.SolveAlignment"/>.</summary>
public sealed class AlignmentResult
{
    public required Chain Chain { get; init; }
    public required double[] ProfileY { get; init; }
    public required double[] SGrid { get; init; }
    public bool Feasible { get; init; }
    public int FlaggedStations { get; init; }
    public int NFlaggedArcs { get; init; }
    public int NTall { get; init; }
    public int NOverRouted { get; init; }
    public double MaxCorridorExcessM { get; init; }
}

/// <summary>Honest guards measured on the emitted chain.</summary>
public sealed record AlignmentSummary(
    bool Success, bool Feasible, int Primitives, int Arcs, int Lines,
    double C1MaxDeg, bool RadiusOk, double RadiusDeficitM,
    double CoverViolationM, bool CoverFlagged, double CoverTolM, double ExcavationAreaM2,
    int UtilityIntrusions, double CoverageGapM, int NOverRouted, int NTall);

public static class Solver
{
    /// <summary>Solve the vertical alignment: corridor → LP profile → structural-G1 polyarc,
    /// with a ceiling-tightening repair loop (port of solver.py solve_alignment).</summary>
    public static AlignmentResult SolveAlignment(CaseSpec spec, AlignmentSettings? settings = null)
    {
        var st = settings ?? new AlignmentSettings();
        double[] sGrid = spec.MakeStationGrid(st.Ds);
        int n = sGrid.Length;
        double[] target = spec.Target(sGrid);
        double[] rMin = spec.RMin(sGrid);
        bool[] forbidden = spec.ForbiddenMask(sGrid);
        Corridor corridor = CorridorBuilder.Build(spec, sGrid, st.ClearanceM, st.CoverAllowanceM);

        double[] ceiling = corridor.Ceiling;
        double[] coverSlack = corridor.CoverSlack;
        var coverCeiling = new double[n];
        for (int i = 0; i < n; i++) coverCeiling[i] = target[i] + coverSlack[i];
        double[] surface = spec.Surface(sGrid);
        var floor = new double[n];
        for (int i = 0; i < n; i++) floor[i] = Math.Max(surface[i] - 40.0, corridor.FloorMin[i]);
        var rMinLp = rMin.Select(r => r * st.LpRminSafety).ToArray();

        const double tol = 1e-3;
        var ceilingEff = (double[])ceiling.Clone();
        double[] y = (double[])ceiling.Clone();
        bool feasible = true;
        bool[] flagged = new bool[n];
        Chain chain = new();
        int nFlag = 0;
        double maxExcess = double.PositiveInfinity;

        for (int rep = 0; rep <= st.MaxRepairIters; rep++)
        {
            ProfileResult pr = ProfileLp.Solve(sGrid, ceilingEff, rMinLp, forbidden, floor,
                                               st.SlopeRefineIters, coverSlack);
            y = pr.Y; feasible = pr.Feasible; flagged = pr.Flagged;
            (chain, nFlag) = Polyarc.Build(sGrid, y, rMin, forbidden, st, coverCeiling, spec.Utilities);
            double[] polyY = ChainYOnGrid(chain, sGrid);
            double[] excess = CorridorExcess(spec, sGrid, polyY, coverCeiling);
            maxExcess = excess.Length > 0 ? excess.Max() : 0.0;
            if (maxExcess <= tol || rep == st.MaxRepairIters) break;
            for (int i = 0; i < n; i++)
                if (excess[i] > tol) ceilingEff[i] -= excess[i] + st.RepairMarginM;
        }

        return new AlignmentResult
        {
            Chain = chain, ProfileY = y, SGrid = sGrid, Feasible = feasible,
            FlaggedStations = flagged.Count(f => f), NFlaggedArcs = nFlag,
            NTall = corridor.TallCount, NOverRouted = corridor.OverRoutedCount,
            MaxCorridorExcessM = maxExcess,
        };
    }

    /// <summary>Guards measured on the emitted chain's own points (+ coverage check).</summary>
    public static AlignmentSummary Summarize(CaseSpec spec, AlignmentResult res, double coverTolM)
    {
        Chain chain = res.Chain;
        double[] sGrid = res.SGrid;
        Pt[] pts = ChainOwnPoints(chain);

        double headGap = pts.Length > 0 ? Math.Max(0.0, pts[0].X - sGrid[0]) : sGrid[^1] - sGrid[0];
        double tailGap = pts.Length > 0 ? Math.Max(0.0, sGrid[^1] - pts[^1].X) : sGrid[^1] - sGrid[0];
        double coverageGap = headGap + tailGap;

        double cover = 0.0, area = 0.0;
        int intrusions = 0;
        if (pts.Length > 0)
        {
            foreach (var p in pts) cover = Math.Max(cover, p.Y - spec.TargetAt(p.X));
            foreach (var box in spec.Utilities)
            {
                bool inside = pts.Any(p => p.X >= box.SLo && p.X <= box.SHi
                                           && p.Y > box.YLo + 1e-6 && p.Y < box.YHi - 1e-6);
                if (inside) intrusions++;
            }
            for (int i = 1; i < pts.Length; i++)
            {
                double dx = pts[i].X - pts[i - 1].X;
                double a0 = Math.Max(spec.SurfaceAt(pts[i - 1].X) - pts[i - 1].Y, 0.0);
                double a1 = Math.Max(spec.SurfaceAt(pts[i].X) - pts[i].Y, 0.0);
                area += 0.5 * (a0 + a1) * dx;
            }
        }
        cover = Math.Max(cover, 0.0);

        double c1Deg = chain.C1MaxRad() * 180.0 / Math.PI;
        var (radOk, radDef) = CheckRadiusCompliance(chain, spec, sGrid);
        bool clean = res.Feasible && radOk && cover <= coverTolM && intrusions == 0 && coverageGap <= 1e-3;
        return new AlignmentSummary(clean, res.Feasible, chain.Primitives.Count, chain.NArcs, chain.NLines,
            c1Deg, radOk, radDef, cover, cover > 1e-3, coverTolM, area, intrusions, coverageGap,
            res.NOverRouted, res.NTall);
    }

    public static (bool ok, double maxDeficit) CheckRadiusCompliance(
        Chain chain, CaseSpec spec, double[] sGrid, double toleranceM = 1e-3)
    {
        double maxDeficit = 0.0;
        bool ok = true;
        int n = sGrid.Length;
        foreach (var prim in chain.Primitives)
        {
            if (prim is not Arc arc) continue;
            int iLo = Math.Max(0, arc.StartIdx);
            int iHi = Math.Min(n - 1, arc.EndIdx);
            if (iHi < iLo) continue;
            double rMinLocal = 0.0;
            for (int i = iLo; i <= iHi; i++) rMinLocal = Math.Max(rMinLocal, spec.RMinAt(sGrid[i]));
            double deficit = rMinLocal - arc.Radius;
            if (deficit > toleranceM) { ok = false; maxDeficit = Math.Max(maxDeficit, deficit); }
        }
        return (ok, maxDeficit);
    }

    // ── sampling helpers ─────────────────────────────────────────────────────

    private static Pt[] ChainOwnPoints(Chain chain)
    {
        var pts = new List<Pt>();
        foreach (var p in chain.Primitives) pts.AddRange(p.Points(p is Arc ? 200 : 2));
        return pts.OrderBy(p => p.X).ToArray();
    }

    private static double[] ChainYOnGrid(Chain chain, double[] sGrid)
    {
        Pt[] pts = ChainOwnPoints(chain);
        if (pts.Length == 0) return (double[])sGrid.Clone();
        double[] px = pts.Select(p => p.X).ToArray();
        double[] py = pts.Select(p => p.Y).ToArray();
        return sGrid.Select(s => CaseSpec.Interp(s, px, py)).ToArray();
    }

    private static double[] CorridorExcess(CaseSpec spec, double[] sGrid, double[] polyY, double[] coverCeiling)
    {
        int n = sGrid.Length;
        var excess = new double[n];
        for (int i = 0; i < n; i++) excess[i] = Math.Max(polyY[i] - coverCeiling[i], 0.0);
        foreach (var box in spec.Utilities)
            for (int i = 0; i < n; i++)
                if (sGrid[i] >= box.SLo && sGrid[i] <= box.SHi
                    && polyY[i] > box.YLo && polyY[i] < box.YHi)
                    excess[i] = Math.Max(excess[i], polyY[i] - box.YLo);
        return excess;
    }
}
