using System;
using System.Linq;

namespace AutoProfileSolver.Alignment;

/// <summary>The vertical corridor the LP profile lives in (port of solver.py build_ceiling).</summary>
public sealed record Corridor(
    double[] Ceiling, double[] CoverSlack, double[] FloorMin,
    int TallCount, int OverRoutedCount);

public static class CorridorBuilder
{
    /// <summary>
    /// Decide, per immovable utility, UNDER (default, respects cover) vs OVER (a local rise
    /// above cover, only when the rise fits <paramref name="coverAllowanceM"/>). Over-routing
    /// reduces excavation area, so within budget it is preferred.
    /// </summary>
    public static Corridor Build(CaseSpec spec, double[] sGrid,
                                 double clearanceM = 0.05, double coverAllowanceM = 0.0)
    {
        int n = sGrid.Length;
        double[] target = spec.Target(sGrid);
        double[] rMin = spec.RMin(sGrid);
        var ceiling = (double[])target.Clone();
        var coverSlack = new double[n];
        var floorMin = new double[n];
        for (int i = 0; i < n; i++) floorMin[i] = double.NegativeInfinity;
        int tall = 0, over = 0;

        foreach (var box in spec.Utilities)
        {
            // indices the box spans
            int lo = -1, hi = -1;
            for (int i = 0; i < n; i++)
                if (sGrid[i] >= box.SLo && sGrid[i] <= box.SHi) { if (lo < 0) lo = i; hi = i; }
            if (lo < 0) continue;

            double tgtMin = double.PositiveInfinity;
            for (int i = lo; i <= hi; i++) tgtMin = Math.Min(tgtMin, target[i]);
            if (box.YHi < tgtMin) continue;                  // short → pipe at cover clears it

            double rise = (box.YHi + clearanceM) - tgtMin;   // rise above cover to clear the top
            if (rise <= coverAllowanceM + 1e-9)
            {
                for (int i = lo; i <= hi; i++)
                    floorMin[i] = Math.Max(floorMin[i], box.YHi + clearanceM);
                double rk = 0.0;
                for (int i = lo; i <= hi; i++) rk = Math.Max(rk, rMin[i]);
                double halfWindow = Math.Sqrt(Math.Max(2.0 * rk * rise, 0.0)) * 1.5 + 2.0;
                for (int i = 0; i < n; i++)
                    if (sGrid[i] >= box.SLo - halfWindow && sGrid[i] <= box.SHi + halfWindow)
                        coverSlack[i] = Math.Max(coverSlack[i], coverAllowanceM);
                over++;
            }
            else
            {
                for (int i = lo; i <= hi; i++)
                    ceiling[i] = Math.Min(ceiling[i], box.YLo - clearanceM);
                tall++;
            }
        }
        return new Corridor(ceiling, coverSlack, floorMin, tall, over);
    }
}
