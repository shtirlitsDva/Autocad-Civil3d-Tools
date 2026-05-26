using System;
using Google.OrTools.LinearSolver;
using OrSolver = Google.OrTools.LinearSolver.Solver;   // disambiguate from our Solver orchestrator

namespace AutoProfileSolver.Alignment;

/// <summary>Result of the profile LP: the elevation per station, feasibility, and the
/// stations whose curvature had to be relaxed (genuinely over-constrained).</summary>
public sealed record ProfileResult(double[] Y, bool Feasible, bool[] Flagged);

/// <summary>
/// Shallowest cover-respecting, R_min-compliant profile as a Linear Program (GLOP), the
/// C# port of solver.py solve_profile_lp. Maximises Σy subject to
/// floor ≤ y ≤ ceiling(+slack), |y_{i-1}−2y_i+y_{i+1}| ≤ ds²/R_min (κ bound), and
/// second-difference = 0 in forbidden bands. Optional over-route slack ``coverSlack`` lets
/// y rise above the ceiling (penalised) to hump over a utility.
/// </summary>
public static class ProfileLp
{
    public static ProfileResult Solve(
        double[] sGrid, double[] ceiling, double[] rMin, bool[] forbidden,
        double[] floor, int slopeRefineIters = 1,
        double[]? coverSlack = null, double overPenalty = 80.0)
    {
        int n = sGrid.Length;
        double ds = sGrid[1] - sGrid[0];
        bool overRoute = coverSlack != null && Max(coverSlack) > 1e-9;

        double[] y = new double[n];
        for (int i = 0; i < n; i++) y[i] = Math.Min(ceiling[i], floor[i]);
        var flagged = new bool[n];
        bool feasible = false;

        int iters = Math.Max(1, slopeRefineIters + 1);
        for (int it = 0; it < iters; it++)
        {
            double[] bCurv = CurvatureBound(y, sGrid, rMin, ds, it == 0);
            var (solved, yNew) = SolveOnce(n, ds, ceiling, floor, forbidden, bCurv,
                                           overRoute ? coverSlack : null, overPenalty);
            if (solved)
            {
                y = yNew;
                feasible = true;
                continue;
            }
            // Infeasible at strict R_min → minimal curvature-slack relaxation that flags
            // exactly the over-constrained stations (mirrors the Python fallback).
            var (relaxedY, relaxFlags) = SolveRelaxed(n, ds, ceiling, floor, forbidden, bCurv);
            y = relaxedY;
            flagged = relaxFlags;
            feasible = false;
            break;
        }
        return new ProfileResult(y, feasible, flagged);
    }

    private static (bool, double[]) SolveOnce(
        int n, double ds, double[] ceiling, double[] floor, bool[] forbidden,
        double[] bCurv, double[]? coverSlack, double overPenalty)
    {
        OrSolver solver = OrSolver.CreateSolver("GLOP")
            ?? throw new InvalidOperationException("GLOP solver unavailable");
        double inf = double.PositiveInfinity;

        var y = new Variable[n];
        var u = coverSlack != null ? new Variable[n] : null;
        for (int i = 0; i < n; i++)
        {
            double ub = ceiling[i] + (coverSlack?[i] ?? 0.0);
            y[i] = solver.MakeNumVar(floor[i], ub, $"y{i}");
            if (u != null) u[i] = solver.MakeNumVar(0.0, coverSlack![i], $"u{i}");
        }

        Objective obj = solver.Objective();
        for (int i = 0; i < n; i++) obj.SetCoefficient(y[i], 1.0);     // maximise Σy
        if (u != null) for (int i = 0; i < n; i++) obj.SetCoefficient(u[i], -overPenalty);
        obj.SetMaximization();

        for (int i = 1; i < n - 1; i++)                               // second-difference rows
        {
            if (forbidden[i])
            {
                Constraint c = solver.MakeConstraint(0.0, 0.0);       // straight in forbidden band
                c.SetCoefficient(y[i - 1], 1); c.SetCoefficient(y[i], -2); c.SetCoefficient(y[i + 1], 1);
            }
            else
            {
                double b = bCurv[i];
                Constraint c = solver.MakeConstraint(-b, b);          // |κ| ≤ 1/R_min
                c.SetCoefficient(y[i - 1], 1); c.SetCoefficient(y[i], -2); c.SetCoefficient(y[i + 1], 1);
            }
        }
        if (u != null)
            for (int i = 0; i < n; i++)                               // y − u ≤ ceiling
            {
                Constraint c = solver.MakeConstraint(-inf, ceiling[i]);
                c.SetCoefficient(y[i], 1); c.SetCoefficient(u[i], -1);
            }

        OrSolver.ResultStatus status = solver.Solve();
        if (status != OrSolver.ResultStatus.OPTIMAL && status != OrSolver.ResultStatus.FEASIBLE)
            return (false, Array.Empty<double>());
        var outY = new double[n];
        for (int i = 0; i < n; i++) outY[i] = y[i].SolutionValue();
        return (true, outY);
    }

    private static (double[], bool[]) SolveRelaxed(
        int n, double ds, double[] ceiling, double[] floor, bool[] forbidden, double[] bCurv)
    {
        OrSolver solver = OrSolver.CreateSolver("GLOP")
            ?? throw new InvalidOperationException("GLOP solver unavailable");
        double inf = double.PositiveInfinity;
        var y = new Variable[n];
        var slack = new Variable[n];                                  // per-interior curvature slack
        for (int i = 0; i < n; i++)
        {
            y[i] = solver.MakeNumVar(floor[i], ceiling[i], $"y{i}");
            slack[i] = solver.MakeNumVar(0.0, inf, $"sl{i}");
        }
        Objective obj = solver.Objective();
        for (int i = 0; i < n; i++) obj.SetCoefficient(slack[i], 1.0);  // minimise total slack
        obj.SetMinimization();

        for (int i = 1; i < n - 1; i++)
        {
            if (forbidden[i])
            {
                Constraint c = solver.MakeConstraint(0.0, 0.0);
                c.SetCoefficient(y[i - 1], 1); c.SetCoefficient(y[i], -2); c.SetCoefficient(y[i + 1], 1);
            }
            else
            {
                double b = bCurv[i];
                // |D2 y| ≤ b + slack_i  →  two one-sided rows
                Constraint cHi = solver.MakeConstraint(-inf, b);
                cHi.SetCoefficient(y[i - 1], 1); cHi.SetCoefficient(y[i], -2); cHi.SetCoefficient(y[i + 1], 1);
                cHi.SetCoefficient(slack[i], -1);
                Constraint cLo = solver.MakeConstraint(-inf, b);
                cLo.SetCoefficient(y[i - 1], -1); cLo.SetCoefficient(y[i], 2); cLo.SetCoefficient(y[i + 1], -1);
                cLo.SetCoefficient(slack[i], -1);
            }
        }
        solver.Solve();
        var outY = new double[n];
        var flags = new bool[n];
        for (int i = 0; i < n; i++)
        {
            outY[i] = y[i].SolutionValue();
            if (slack[i].SolutionValue() > 1e-6) flags[i] = true;
        }
        return (outY, flags);
    }

    /// <summary>Curvature bound ds²/R_min (it 0) or slope-corrected ds²·(1+y'²)^1.5/R_min.</summary>
    private static double[] CurvatureBound(double[] y, double[] s, double[] rMin, double ds, bool first)
    {
        int n = y.Length;
        var b = new double[n];
        for (int i = 1; i < n - 1; i++)
        {
            double slope = first ? 0.0 : (y[i + 1] - y[i - 1]) / (s[i + 1] - s[i - 1]);
            double factor = first ? 1.0 : Math.Pow(1.0 + slope * slope, 1.5);
            b[i] = ds * ds * factor / Math.Max(rMin[i], 1e-9);
        }
        return b;
    }

    private static double Max(double[] a)
    {
        double m = double.NegativeInfinity;
        foreach (double x in a) if (x > m) m = x;
        return m;
    }
}
