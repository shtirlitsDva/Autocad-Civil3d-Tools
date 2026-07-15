using Autodesk.AutoCAD.Geometry;

namespace IntersectUtilities.MPE.PipePlan;

/// <summary>
/// Resolves a maximal run of consecutive crowded corners (segments shorter than the
/// fillets' combined tangent lengths) into a single G1 tangent arc chain that keeps
/// every bend radius, instead of rejecting it as "segment too short".
///
/// The run is solved as a sequence of arcs parameterised by signed turn angle α_i
/// (AutoCAD bulge = tan(α_i/4)). The chain enters tangent to the leg before the run
/// and exits tangent to the leg after it; consecutive arcs are tangent by construction
/// (each starts where the previous ended, in the accumulated direction). The remaining
/// freedom is pinned by keeping each turn angle close to the corner's original
/// deflection and the entry point close to the un-crowded fillet's tangent point, so a
/// two-corner run reproduces the ordinary biarc and the solution converges back to the
/// plain fillets as the run un-crowds (no visible pop at the threshold).
///
/// Because it works in turn angles rather than centre distances, it handles any number
/// of arcs and never divides by the centre gap — the equal-radius, same-sense case that
/// breaks a pairwise biarc is just α_i of the same sign here.
/// </summary>
internal static class PipePlanCrowdedCornerSolver
{
    private const double DistanceTolerance = 1e-6;
    private const double AngleTolerance = 1e-6;

    internal readonly record struct RunResult(
        List<Point2d> Points,   // n+1 points: entry tangent point, interior joins…, exit tangent point
        List<double> Bulges,    // n+1 bulges aligned to Points (last entry unused / 0)
        double EntryRetreat,    // distance from the first corner back along the incoming leg to the entry point
        double ExitAdvance);    // distance from the last corner forward along the outgoing leg to the exit point

    /// <param name="prev">Control point before the first crowded corner (defines the incoming leg).</param>
    /// <param name="corners">The crowded corners, in order (n ≥ 2).</param>
    /// <param name="radii">Bend radius per corner, aligned to <paramref name="corners"/>.</param>
    /// <param name="next">Control point after the last crowded corner (defines the outgoing leg).</param>
    public static bool TrySolveRun(
        Point3d prev,
        IReadOnlyList<Point3d> corners,
        IReadOnlyList<double> radii,
        Point3d next,
        out RunResult result,
        out string error)
    {
        result = default;
        error = string.Empty;

        int n = corners.Count;
        if (n < 2 || radii.Count != n)
        {
            error = "Ugyldig bue-bue kæde.";
            return false;
        }

        List<Point2d> seq = [To2D(prev)];
        for (int i = 0; i < n; i++)
        {
            seq.Add(To2D(corners[i]));
        }
        seq.Add(To2D(next));

        Vector2d dIn = seq[1] - seq[0];
        Vector2d dOut = seq[n + 1] - seq[n];
        if (dIn.Length < DistanceTolerance || dOut.Length < DistanceTolerance)
        {
            error = "Nul-længde ben.";
            return false;
        }
        dIn = dIn.GetNormal();
        dOut = dOut.GetNormal();

        // Original signed deflection at each corner, from the un-merged polyline directions.
        double[] beta = new double[n];
        for (int i = 0; i < n; i++)
        {
            Vector2d incoming = (seq[i + 1] - seq[i]).GetNormal();
            Vector2d outgoing = (seq[i + 2] - seq[i + 1]).GetNormal();
            beta[i] = Math.Atan2(Cross(incoming, outgoing), incoming.DotProduct(outgoing));
        }
        double sumBeta = beta.Sum();
        double e0 = -radii[0] * Math.Tan(Math.Abs(beta[0]) / 2.0); // original entry tangent-point retreat
        const double weight = 1.0;                                 // how hard to hold the entry point near original

        Point2d firstCorner = seq[1];
        Point2d lastCorner = seq[n];
        Vector2d nOut = new(-dOut.Y, dOut.X);
        double phi0 = Math.Atan2(dIn.Y, dIn.X);

        // Forward map: entry slide + turn angles -> exit point of the chain.
        Point2d ForwardExit(double e, double[] alpha)
        {
            double phi = phi0;
            Point2d q = firstCorner + dIn * e;
            for (int i = 0; i < n; i++)
            {
                double a = alpha[i];
                double chordDir = phi + (a / 2.0);
                double chordLen = 2.0 * radii[i] * Math.Sin(Math.Abs(a) / 2.0);
                q = new Point2d(q.X + (chordLen * Math.Cos(chordDir)), q.Y + (chordLen * Math.Sin(chordDir)));
                phi += a;
            }
            return q;
        }
        double G2(double e, double[] alpha) => nOut.DotProduct(ForwardExit(e, alpha) - lastCorner);

        // Unknown vector y = [e, alpha_0..alpha_{n-1}, lambda1, lambda2].
        int m = n + 3;
        double[] y = new double[m];
        y[0] = e0;
        for (int i = 0; i < n; i++)
        {
            y[1 + i] = beta[i];
        }

        double[] Residual(double[] yy)
        {
            double e = yy[0];
            double[] al = new double[n];
            for (int i = 0; i < n; i++)
            {
                al[i] = yy[1 + i];
            }
            double lam1 = yy[n + 1];
            double lam2 = yy[n + 2];

            const double h = 1e-6;
            double g2 = G2(e, al);
            double dg2de = (G2(e + h, al) - G2(e - h, al)) / (2.0 * h);
            double[] dg2da = new double[n];
            for (int i = 0; i < n; i++)
            {
                al[i] += h; double gp = G2(e, al);
                al[i] -= 2.0 * h; double gm = G2(e, al);
                al[i] += h;
                dg2da[i] = (gp - gm) / (2.0 * h);
            }

            double[] h_ = new double[m];
            // ∂L/∂e : objective weight·(e-e0)² plus g2 (g1 has no e term).
            h_[0] = (2.0 * weight * (e - e0)) - (lam2 * dg2de);
            for (int i = 0; i < n; i++)
            {
                h_[1 + i] = (2.0 * (al[i] - beta[i])) - lam1 - (lam2 * dg2da[i]);
            }
            h_[n + 1] = al.Sum() - sumBeta; // g1: total turning unchanged
            h_[n + 2] = g2;                 // g2: exit lands on the outgoing leg line
            return h_;
        }

        // Track the iterate with the smallest CONSTRAINT residual (g1: total turning,
        // g2: exit on the outgoing leg). Validity of the preview depends only on those
        // two closing — not on the full optimality gradient vanishing. Accepting the
        // best-constraint iterate keeps the preview green through the transition where a
        // corner gets absorbed, instead of flickering red at the 1e-9 optimality edge.
        double[] best = (double[])y.Clone();
        double bestConstraint = double.MaxValue;
        for (int iter = 0; iter < 80; iter++)
        {
            double[] residual = Residual(y);
            double constraint = Math.Abs(residual[n + 1]) + Math.Abs(residual[n + 2]);
            if (constraint < bestConstraint)
            {
                bestConstraint = constraint;
                best = (double[])y.Clone();
            }
            if (residual.Sum(Math.Abs) < 1e-11)
            {
                break;
            }

            double[,] jac = new double[m, m];
            const double hj = 1e-7;
            for (int j = 0; j < m; j++)
            {
                double save = y[j];
                y[j] = save + hj; double[] hp = Residual(y);
                y[j] = save - hj; double[] hm = Residual(y);
                y[j] = save;
                for (int i = 0; i < m; i++)
                {
                    jac[i, j] = (hp[i] - hm[i]) / (2.0 * hj);
                }
            }

            if (!SolveLinear(jac, residual, out double[] delta))
            {
                break;
            }

            // Damped step: don't let a near-singular Jacobian (common right at the
            // absorption transition) throw the iterate off — backtrack until the residual
            // doesn't grow.
            double baseNorm = residual.Sum(Math.Abs);
            double[] trial = new double[m];
            double stepScale = 1.0;
            for (int bt = 0; bt < 10; bt++)
            {
                for (int i = 0; i < m; i++)
                {
                    trial[i] = y[i] - (stepScale * delta[i]);
                }
                if (Residual(trial).Sum(Math.Abs) <= baseNorm || bt == 9)
                {
                    break;
                }
                stepScale *= 0.5;
            }
            Array.Copy(trial, y, m);
        }

        if (bestConstraint > 1e-6)
        {
            error = "Bue-bue kæde konvergerede ikke.";
            return false;
        }
        y = best;

        double eFinal = y[0];
        List<Point2d> points = [];
        List<double> bulges = [];
        double phiF = phi0;
        Point2d cursor = firstCorner + dIn * eFinal;
        points.Add(cursor);
        for (int i = 0; i < n; i++)
        {
            double a = y[1 + i];
            if (Math.Abs(a) >= Math.PI - AngleTolerance)
            {
                error = "Bue over 180°.";
                return false;
            }
            // A turn angle driven to ~0 means this corner has been absorbed: the arc
            // collapses to nothing and its neighbours meet directly. Skip it rather than
            // reject, so the preview stays green as corners get eaten instead of flashing
            // red at the zero-crossing.
            if (Math.Abs(a) <= AngleTolerance)
            {
                phiF += a;
                continue;
            }
            double chordDir = phiF + (a / 2.0);
            double chordLen = 2.0 * radii[i] * Math.Sin(Math.Abs(a) / 2.0);
            Point2d nextPoint = new(cursor.X + (chordLen * Math.Cos(chordDir)), cursor.Y + (chordLen * Math.Sin(chordDir)));
            bulges.Add(Math.Tan(a / 4.0)); // bulge leaving the current last point
            points.Add(nextPoint);
            cursor = nextPoint;
            phiF += a;
        }
        bulges.Add(0.0);

        Point2d exit = points[^1];
        double entryRetreat = -eFinal;
        double exitAdvance = dOut.DotProduct(exit - lastCorner);
        if (entryRetreat < -DistanceTolerance || exitAdvance < -DistanceTolerance)
        {
            error = "Bue-bue tangentpunkt falder uden for segmentet.";
            return false;
        }
        if (entryRetreat > seq[0].GetDistanceTo(firstCorner) + DistanceTolerance ||
            exitAdvance > lastCorner.GetDistanceTo(seq[n + 1]) + DistanceTolerance)
        {
            error = "Segment for kort til bue-bue.";
            return false;
        }

        result = new RunResult(points, bulges, entryRetreat, exitAdvance);
        return true;
    }

    private static double Cross(Vector2d a, Vector2d b) => (a.X * b.Y) - (a.Y * b.X);

    private static Point2d To2D(Point3d p) => new(p.X, p.Y);

    // Gaussian elimination with partial pivoting; solves A·x = b in place. Small dense
    // KKT systems (size n+3, n = arcs in the run), so O(m³) is negligible.
    private static bool SolveLinear(double[,] a, double[] b, out double[] x)
    {
        int m = b.Length;
        double[,] mat = (double[,])a.Clone();
        double[] rhs = (double[])b.Clone();
        x = new double[m];

        for (int col = 0; col < m; col++)
        {
            int pivot = col;
            double best = Math.Abs(mat[col, col]);
            for (int row = col + 1; row < m; row++)
            {
                double v = Math.Abs(mat[row, col]);
                if (v > best)
                {
                    best = v;
                    pivot = row;
                }
            }
            if (best < 1e-14)
            {
                return false;
            }
            if (pivot != col)
            {
                for (int k = 0; k < m; k++)
                {
                    (mat[col, k], mat[pivot, k]) = (mat[pivot, k], mat[col, k]);
                }
                (rhs[col], rhs[pivot]) = (rhs[pivot], rhs[col]);
            }

            for (int row = col + 1; row < m; row++)
            {
                double factor = mat[row, col] / mat[col, col];
                for (int k = col; k < m; k++)
                {
                    mat[row, k] -= factor * mat[col, k];
                }
                rhs[row] -= factor * rhs[col];
            }
        }

        for (int row = m - 1; row >= 0; row--)
        {
            double sum = rhs[row];
            for (int k = row + 1; k < m; k++)
            {
                sum -= mat[row, k] * x[k];
            }
            x[row] = sum / mat[row, row];
        }
        return true;
    }
}
