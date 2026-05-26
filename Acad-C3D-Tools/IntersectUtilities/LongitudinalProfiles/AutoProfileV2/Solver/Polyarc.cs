using System;
using System.Collections.Generic;
using System.Linq;

namespace AutoProfileSolver.Alignment;

/// <summary>Build a full-coverage Line/Arc chain from the LP profile
/// (port of solver.py build_polyarc + helpers).</summary>
public static class Polyarc
{
    private sealed class Grade
    {
        public double Slope, Intercept, SLo, SHi;
        public double YAt(double s) => Slope * s + Intercept;
        public Pt Dir => new Pt(1.0, Slope).Unit();
    }

    public static (Chain chain, int nFlag) Build(
        double[] sGrid, double[] y, double[] rMin, bool[] forbidden,
        AlignmentSettings st, double[] coverCeiling, IReadOnlyList<UtilityBox> boxes)
    {
        int n = sGrid.Length;
        var runs = StraightRuns(sGrid, y, forbidden, st.StraightCurvRM, st.MinGradePts);
        var prims = new List<IPrimitive>();
        int nFlag = 0;

        if (runs.Count == 0)
        {
            var (seg, fl) = FitCurvedSpan(sGrid, y, 0, n - 1,
                new Pt(sGrid[0], y[0]), LpTangent(sGrid, y, 0),
                new Pt(sGrid[^1], y[^1]), LpTangent(sGrid, y, n - 1),
                rMin, coverCeiling, st, boxes, 0);
            prims.AddRange(seg.Count > 0 ? seg
                : new List<IPrimitive> { new Line(new Pt(sGrid[0], y[0]), new Pt(sGrid[^1], y[^1]), 0, n - 1) });
            nFlag += fl;
            AssignIndices(prims, sGrid); FixIndices(prims, n);
            return (new Chain(prims), nFlag);
        }

        runs = MergeNearParallelRuns(sGrid, y, runs,
            st.MergeDeflectionDeg * Math.PI / 180.0, st.MergeDevTolM);
        var grades = runs.Select(r => FitGrade(sGrid, y, r.lo, r.hi)).ToList();

        void LineTo(Pt prev, Pt p)
        {
            if ((p - prev).Hypot > 1e-9) prims.Add(new Line(prev, p));
        }

        Pt cur;
        int lo0 = runs[0].lo;
        if (lo0 > 0)
        {
            cur = new Pt(sGrid[lo0], grades[0].YAt(sGrid[lo0]));
            var (seg, fl) = FitCurvedSpan(sGrid, y, 0, lo0,
                new Pt(sGrid[0], y[0]), LpTangent(sGrid, y, 0), cur, grades[0].Dir,
                rMin, coverCeiling, st, boxes, 0);
            prims.AddRange(seg); nFlag += fl;
        }
        else cur = new Pt(sGrid[0], grades[0].YAt(sGrid[0]));

        for (int k = 0; k < grades.Count - 1; k++)
        {
            Grade g1 = grades[k], g2 = grades[k + 1];
            var fillet = TryFillet(g1, g2, rMin, sGrid);
            if (fillet is not null)
            {
                var (p1, arc, p2, flag) = fillet.Value;
                LineTo(cur, p1);
                prims.Add(arc); nFlag += flag;
                cur = p2;
            }
            else
            {
                int b = runs[k].hi, bNext = runs[k + 1].lo;
                Pt ps = new(sGrid[b], g1.YAt(sGrid[b]));
                Pt pe = new(sGrid[bNext], g2.YAt(sGrid[bNext]));
                LineTo(cur, ps);
                var (seg, fl) = FitCurvedSpan(sGrid, y, b, bNext, ps, g1.Dir, pe, g2.Dir,
                    rMin, coverCeiling, st, boxes, 0);
                prims.AddRange(seg); nFlag += fl;
                cur = pe;
            }
        }

        Grade gL = grades[^1];
        int hiLast = runs[^1].hi;
        if (hiLast < n - 1)
        {
            Pt ps = new(sGrid[hiLast], gL.YAt(sGrid[hiLast]));
            LineTo(cur, ps);
            var (seg, fl) = FitCurvedSpan(sGrid, y, hiLast, n - 1, ps, gL.Dir,
                new Pt(sGrid[^1], y[^1]), LpTangent(sGrid, y, n - 1),
                rMin, coverCeiling, st, boxes, 0);
            prims.AddRange(seg); nFlag += fl;
        }
        else LineTo(cur, new Pt(sGrid[^1], gL.YAt(sGrid[^1])));

        AssignIndices(prims, sGrid); FixIndices(prims, n);
        return (new Chain(prims), nFlag);
    }

    // ── straight runs + grades ───────────────────────────────────────────────

    private static List<(int lo, int hi)> StraightRuns(
        double[] s, double[] y, bool[] forbidden, double straightR, int minPts)
    {
        int n = s.Length;
        double ds = s[1] - s[0];
        var curved = new bool[n];
        for (int i = 1; i < n - 1; i++)
        {
            double d2 = Math.Abs(y[i - 1] - 2 * y[i] + y[i + 1]) / (ds * ds);
            curved[i] = d2 > 1.0 / straightR;
        }
        for (int i = 0; i < n; i++) if (forbidden[i]) curved[i] = false;
        var runs = new List<(int, int)>();
        int idx = 0;
        while (idx < n)
        {
            if (curved[idx]) { idx++; continue; }
            int j = idx;
            while (j + 1 < n && !curved[j + 1]) j++;
            if (j - idx + 1 >= minPts) runs.Add((idx, j));
            idx = j + 1;
        }
        return runs;
    }

    private static Grade FitGrade(double[] s, double[] y, int a, int b)
    {
        int m = b - a + 1;
        double sx = 0, sy = 0, sxx = 0, sxy = 0;
        for (int i = a; i <= b; i++) { sx += s[i]; sy += y[i]; sxx += s[i] * s[i]; sxy += s[i] * y[i]; }
        double denom = m * sxx - sx * sx;
        double slope = Math.Abs(denom) < 1e-30 ? 0.0 : (m * sxy - sx * sy) / denom;
        double intercept = (sy - slope * sx) / m;
        return new Grade { Slope = slope, Intercept = intercept, SLo = s[a], SHi = s[b] };
    }

    private static List<(int lo, int hi)> MergeNearParallelRuns(
        double[] s, double[] y, List<(int lo, int hi)> runs, double mergeDeflRad, double mergeDevTol)
    {
        runs = new List<(int, int)>(runs);
        bool changed = true;
        while (changed && runs.Count > 1)
        {
            changed = false;
            for (int i = 0; i < runs.Count - 1; i++)
            {
                Grade g1 = FitGrade(s, y, runs[i].lo, runs[i].hi);
                Grade g2 = FitGrade(s, y, runs[i + 1].lo, runs[i + 1].hi);
                double defl = Math.Abs(Math.Atan(g2.Slope) - Math.Atan(g1.Slope));
                if (defl >= mergeDeflRad) continue;
                int a = runs[i].lo, b = runs[i + 1].hi;
                Grade gm = FitGrade(s, y, a, b);
                double dev = 0.0;
                for (int j = a; j <= b; j++) dev = Math.Max(dev, Math.Abs(y[j] - (gm.Slope * s[j] + gm.Intercept)));
                if (dev < mergeDevTol)
                {
                    var merged = new List<(int, int)>();
                    for (int k = 0; k < i; k++) merged.Add(runs[k]);
                    merged.Add((a, b));
                    for (int k = i + 2; k < runs.Count; k++) merged.Add(runs[k]);
                    runs = merged;
                    changed = true;
                    break;
                }
            }
        }
        return runs;
    }

    private static Pt? Intersect(Grade g1, Grade g2)
    {
        if (Math.Abs(g1.Slope - g2.Slope) < 1e-12) return null;
        double sx = (g2.Intercept - g1.Intercept) / (g1.Slope - g2.Slope);
        return new Pt(sx, g1.YAt(sx));
    }

    private static (Pt p1, Arc arc, Pt p2, int flag)? TryFillet(
        Grade g1, Grade g2, double[] rMin, double[] sGrid)
    {
        Pt? pviN = Intersect(g1, g2);
        if (pviN is null) return null;
        Pt pvi = pviN.Value;
        Pt d1 = g1.Dir, d2 = g2.Dir;
        double delta = Math.Atan2(d1.Cross(d2), d1.Dot(d2));
        bool inSpan = g1.SLo - 1e-9 <= pvi.X && pvi.X <= g2.SHi + 1e-9;
        if (!inSpan || Math.Abs(delta) < 1e-5) return null;
        double rminK = rMin[IdxAt(sGrid, pvi.X)];
        double half = Math.Abs(delta) / 2.0;
        double tReq = rminK * Math.Tan(half);
        double roomLo = Math.Abs(pvi.X - g1.SLo) / Math.Max(Math.Abs(d1.X), 1e-9);
        double roomHi = Math.Abs(g2.SHi - pvi.X) / Math.Max(Math.Abs(d2.X), 1e-9);
        double avail = Math.Min(roomLo, roomHi);
        int flag = 0;
        double R, T;
        if (tReq <= avail) { R = rminK; T = tReq; }
        else { T = avail; R = T / Math.Tan(half); flag = 1; }
        Pt p1 = new(pvi.X - T * d1.X, pvi.Y - T * d1.Y);
        Pt p2 = new(pvi.X + T * d2.X, pvi.Y + T * d2.Y);
        double sgn = delta > 0 ? 1.0 : -1.0;
        Pt center = new(p1.X + sgn * R * (-d1.Y), p1.Y + sgn * R * d1.X);
        double a0 = Math.Atan2(p1.Y - center.Y, p1.X - center.X);
        var arc = new Arc(center, R, a0, delta);
        return (p1, arc, p2, flag);
    }

    // ── curved span fitting ────────────────────────────────────────────────

    private static Pt LpTangent(double[] s, double[] y, int i)
    {
        int n = s.Length;
        double slope = i <= 0 ? (y[1] - y[0]) / (s[1] - s[0])
            : i >= n - 1 ? (y[^1] - y[^2]) / (s[^1] - s[^2])
            : (y[i + 1] - y[i - 1]) / (s[i + 1] - s[i - 1]);
        return new Pt(1.0, slope).Unit();
    }

    private static (double dev, bool viol, double minR) SpanQuality(
        IReadOnlyList<Arc> arcs, double[] s, double[] y, int lo, int hi,
        double[] coverCeiling, IReadOnlyList<UtilityBox> boxes)
    {
        var pts = new List<Pt>();
        foreach (var a in arcs) pts.AddRange(a.Points(48));
        pts.Sort((p, q) => p.X.CompareTo(q.X));
        double[] px = pts.Select(p => p.X).ToArray();
        double[] py = pts.Select(p => p.Y).ToArray();
        double dev = 0.0;
        for (int i = lo; i <= hi; i++)
            dev = Math.Max(dev, Math.Abs(Interp(s[i], px, py) - y[i]));
        bool viol = false;
        for (int i = 0; i < pts.Count; i++)
            if (py[i] > Interp(px[i], s, coverCeiling) + 5e-3) { viol = true; break; }
        if (!viol)
            foreach (var box in boxes)
                for (int i = 0; i < pts.Count; i++)
                    if (px[i] >= box.SLo && px[i] <= box.SHi &&
                        py[i] > box.YLo + 1e-6 && py[i] < box.YHi - 1e-6) { viol = true; break; }
        double minR = arcs.Min(a => a.Radius);
        return (dev, viol, minR);
    }

    private static (List<IPrimitive> prims, int nFlag) FitCurvedSpan(
        double[] s, double[] y, int lo, int hi, Pt pStart, Pt tStart, Pt pEnd, Pt tEnd,
        double[] rMin, double[] coverCeiling, AlignmentSettings st,
        IReadOnlyList<UtilityBox> boxes, int depth)
    {
        if ((pEnd - pStart).Hypot < 1e-9) return (new List<IPrimitive>(), 0);
        double rminSpan = 0.0;
        for (int i = lo; i <= hi; i++) rminSpan = Math.Max(rminSpan, rMin[i]);
        bool tooShort = hi - lo <= Math.Max(2, st.MinGradePts);

        Pt tEndU = tEnd.Unit();
        Arc? single = Geometry.ArcThrough(pStart, tStart, pEnd);
        if (single is not null)
        {
            double tanErr = Math.Acos(Math.Clamp(single.TangentAtEnd().Dot(tEndU), -1.0, 1.0)) * 180.0 / Math.PI;
            var (dev1, viol1, minR1) = SpanQuality(new[] { single }, s, y, lo, hi, coverCeiling, boxes);
            if (tanErr <= st.SpanTanTolDeg && dev1 <= st.SpanDevTolM && !viol1 && minR1 >= st.FitSafety * rminSpan)
                return (new List<IPrimitive> { single }, 0);
        }

        Arc[]? biarc = Geometry.Biarc(pStart, tStart, pEnd, tEnd);
        if (biarc is null)
        {
            double chordDev = 0.0;
            for (int i = lo; i <= hi; i++)
            {
                double t = (s[i] - pStart.X) / (pEnd.X - pStart.X);
                double cy = pStart.Y + t * (pEnd.Y - pStart.Y);
                chordDev = Math.Max(chordDev, Math.Abs(cy - y[i]));
            }
            if (chordDev <= st.SpanDevTolM || tooShort || depth >= st.MaxSubdivDepth)
                return (new List<IPrimitive> { new Line(pStart, pEnd, lo, hi) }, 0);
        }
        else
        {
            var (dev, viol, minR) = SpanQuality(biarc, s, y, lo, hi, coverCeiling, boxes);
            bool rOk = minR >= st.FitSafety * rminSpan;
            if ((dev <= st.SpanDevTolM && !viol && rOk) || depth >= st.MaxSubdivDepth || tooShort)
                return (new List<IPrimitive>(biarc), rOk ? 0 : 1);
        }

        int m = (lo + hi) / 2;
        Pt pm = new(s[m], y[m]);
        Pt tm = LpTangent(s, y, m);
        var (left, fl) = FitCurvedSpan(s, y, lo, m, pStart, tStart, pm, tm, rMin, coverCeiling, st, boxes, depth + 1);
        var (right, fr) = FitCurvedSpan(s, y, m, hi, pm, tm, pEnd, tEnd, rMin, coverCeiling, st, boxes, depth + 1);
        left.AddRange(right);
        return (left, fl + fr);
    }

    // ── index bookkeeping ────────────────────────────────────────────────────

    private static int IdxAt(double[] sGrid, double sVal)
        => (int)Math.Clamp(Math.Round((sVal - sGrid[0]) / (sGrid[1] - sGrid[0])), 0, sGrid.Length - 1);

    private static void AssignIndices(List<IPrimitive> prims, double[] sGrid)
    {
        foreach (var p in prims)
        {
            if (p is Line ln)
            {
                p.StartIdx = IdxAt(sGrid, ln.P0.X);
                p.EndIdx = IdxAt(sGrid, ln.P1.X);
            }
            else
            {
                var pp = p.Points(2);
                p.StartIdx = IdxAt(sGrid, pp[0].X);
                p.EndIdx = IdxAt(sGrid, pp[^1].X);
            }
        }
    }

    private static void FixIndices(List<IPrimitive> prims, int nPoints)
    {
        if (prims.Count == 0) return;
        prims[0].StartIdx = 0;
        for (int k = 0; k < prims.Count - 1; k++)
        {
            int e = Math.Max(prims[k].StartIdx + 1, Math.Min(prims[k].EndIdx, nPoints - 2));
            prims[k].EndIdx = e;
            prims[k + 1].StartIdx = e;
        }
        prims[^1].EndIdx = nPoints - 1;
    }

    /// <summary>numpy.interp-style clamped linear interpolation (xs ascending).</summary>
    private static double Interp(double x, double[] xs, double[] ys)
    {
        int n = xs.Length;
        if (x <= xs[0]) return ys[0];
        if (x >= xs[n - 1]) return ys[n - 1];
        int lo = 0, hi = n - 1;
        while (hi - lo > 1) { int mid = (lo + hi) >> 1; if (xs[mid] <= x) lo = mid; else hi = mid; }
        double t = (x - xs[lo]) / (xs[hi] - xs[lo]);
        return ys[lo] + t * (ys[hi] - ys[lo]);
    }
}
