// NPPLPlacement — the de-cluttering algorithm for NorsynProjectionProfileLabel.
//
// Many CogoPoints project onto a ProfileView at clustered stations. Placing each
// label's vertical text directly above its own projection piles them into an
// illegible heap. This module spreads the text anchors (P3) along the station
// axis and routes the leaders so the result is readable and traceable:
//
//   * a label with room              -> STRAIGHT leader (text directly above its
//                                       point, no shoulder jog),
//   * a cluster jammed against an edge-> fanned INWARD into the free space,
//   * a real cluster                 -> shoulder LADDER: every leader's horizontal
//                                       shoulder sits at its own height so each
//                                       label is traceable back to its point and
//                                       no two shoulders overlap.
//
// X placement is order-preserving minimum-displacement (isotonic / PAVA) with a
// feasible-envelope clamp (so interior labels stay exactly above their point and
// edge clusters fan inward instead of overflowing). Shoulder heights come from a
// GLOBAL ordering over ALL leaders in the view, not per cluster: if one leader's
// horizontal span passes over another leader's POINT (but not its text) it must
// sit ABOVE it; over the text only, below. Resolving every such constraint
// together (longest-path levels) coordinates neighbouring clusters, so wide
// spanning leaders dip low, narrow local ones ride near the shelf, and no two
// leaders cross. See docs/norsyn-projection-label-placement.md (NorsynDrawingTools).
using System;
using System.Collections.Generic;

namespace IntersectUtilities.LongitudinalProfiles
{
    internal static class NPPLPlacement
    {
        // Little final riser left above the top ladder rung (text enters from just
        // above the highest shoulder).
        private const double TopRiser = 0.2;

        // Result for one label (in the station-sorted order the caller passes in).
        public struct Slot
        {
            public double X;        // text anchor x (P3x). Equals the point x when Straight.
            public double GripY;    // shoulder elevation (ignored when Straight).
            public bool Straight;   // text directly above the point -> single-segment leader.
        }

        // Full placement for one ProfileView. dSorted = projection x of each label,
        // sorted ascending by station. shelf = common text baseline; projTop = top
        // of the projection band (highest P0.y). Returns one Slot per label, in the
        // same sorted order.
        public static Slot[] Place(double[] dSorted, double gap, double lo, double hi,
                                   double shelf, double projTop, double straightEps = 0.02)
        {
            int n = dSorted.Length;
            var slots = new Slot[n];
            if (n == 0) return slots;

            double[] x = OrderedMinGap(dSorted, gap, lo, hi);
            var a = new double[n];
            var b = new double[n];
            for (int i = 0; i < n; i++)
            {
                a[i] = Math.Min(dSorted[i], x[i]);
                b[i] = Math.Max(dSorted[i], x[i]);
            }
            const double eps = 1e-6;
            bool Spans(int r, double xv) => a[r] + eps < xv && xv < b[r] - eps;

            // A label is straight only if its run ~ 0 AND no other leader's span
            // passes over its x (else its full-height riser would be crossed).
            var spanned = new bool[n];
            for (int r = 0; r < n; r++)
                for (int i = 0; i < n; i++)
                    if (i != r && Spans(i, dSorted[r])) { spanned[r] = true; break; }
            var straight = new bool[n];
            for (int r = 0; r < n; r++)
                straight[r] = Math.Abs(x[r] - dSorted[r]) < straightEps && !spanned[r];

            // GLOBAL shoulder ordering. Constraint: if leader i's horizontal span
            // passes over leader j's POINT (but not its text), i must sit ABOVE j;
            // over j's TEXT only, below. adj[u] lists the leaders that must be above
            // u. Longest-path level(u) then ranks how many leaders u sits above, so a
            // wide/spanning leader gets a HIGH level -> a LOW shoulder, and narrow /
            // local ones ride near the shelf. This coordinates ACROSS clusters, so
            // neighbouring fans no longer cross.
            var adj = new List<int>[n];
            for (int i = 0; i < n; i++) adj[i] = new List<int>();
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    if (i == j) continue;
                    bool ip = Spans(i, dSorted[j]);
                    bool it = Spans(i, x[j]);
                    if (ip && !it) adj[j].Add(i);          // i above j
                    else if (it && !ip) adj[i].Add(j);     // j above i
                }

            var level = new int[n];
            var state = new byte[n];                       // 0 unvisited, 1 visiting, 2 done
            for (int u = 0; u < n; u++)
                if (state[u] != 2) LongestPath(u, adj, level, state);
            int maxLevel = 0;
            for (int i = 0; i < n; i++) maxLevel = Math.Max(maxLevel, level[i]);

            double top = shelf - TopRiser;                 // top rung: as high as possible
            double bot = projTop + 0.5;
            if (top <= bot) top = bot + 1.0;
            double step = maxLevel > 0 ? (top - bot) / (3 * maxLevel) : 0.0;   // compact
            if (step * maxLevel > top - bot) step = (top - bot) / maxLevel;    // fit band

            for (int r = 0; r < n; r++)
            {
                slots[r] = straight[r]
                    ? new Slot { X = dSorted[r], GripY = shelf, Straight = true }
                    : new Slot { X = x[r], GripY = top - level[r] * step, Straight = false };
            }
            return slots;
        }

        // Longest path out of u over the "must be above" graph, with a visiting guard
        // so a constraint cycle (rare) degrades gracefully instead of looping.
        private static int LongestPath(int u, List<int>[] adj, int[] level, byte[] state)
        {
            if (state[u] == 1) return 0;
            if (state[u] == 2) return level[u];
            state[u] = 1;
            int best = 0;
            foreach (int v in adj[u]) best = Math.Max(best, 1 + LongestPath(v, adj, level, state));
            level[u] = best;
            state[u] = 2;
            return best;
        }

        // L2-optimal order-preserving minimum-gap positions WITHOUT a rigid shift.
        // Minimise sum (x_i - targets_i)^2 s.t. x_{i+1} >= x_i + gap and the view
        // bounds. Each target is first clamped to its feasible position envelope
        // [lo+i*gap, hi-(n-1-i)*gap]; PAVA then pools only genuine conflicts. So an
        // isolated label keeps its exact target (straight leader), and a cluster
        // against an edge is pushed inward rather than overflowing or skewing the
        // rest of the view.
        public static double[] OrderedMinGap(double[] targets, double gap, double lo, double hi)
        {
            int n = targets.Length;
            if (n == 0) return Array.Empty<double>();
            if (n == 1) return new[] { Clamp(targets[0], lo, hi) };
            if ((n - 1) * gap > hi - lo)                       // physically can't fit
            {
                var even = new double[n];
                double step = (hi - lo) / (n - 1);
                for (int i = 0; i < n; i++) even[i] = lo + i * step;
                return even;
            }

            var d = new double[n];
            for (int i = 0; i < n; i++)
                d[i] = Clamp(targets[i], lo + i * gap, hi - (n - 1 - i) * gap);

            var sum = new List<double>();
            var cnt = new List<int>();
            var val = new List<double>();
            for (int i = 0; i < n; i++)
            {
                double y = d[i] - i * gap;
                sum.Add(y); cnt.Add(1); val.Add(y);
                while (val.Count > 1 && val[^2] > val[^1])
                {
                    double s = sum[^1] + sum[^2];
                    int c = cnt[^1] + cnt[^2];
                    sum.RemoveRange(sum.Count - 2, 2);
                    cnt.RemoveRange(cnt.Count - 2, 2);
                    val.RemoveRange(val.Count - 2, 2);
                    sum.Add(s); cnt.Add(c); val.Add(s / c);
                }
            }
            var x = new double[n];
            int idx = 0;
            for (int bb = 0; bb < val.Count; bb++)
                for (int k = 0; k < cnt[bb]; k++, idx++)
                    x[idx] = val[bb] + idx * gap;
            return x;
        }

        private static double Clamp(double v, double lo, double hi)
            => v < lo ? lo : (v > hi ? hi : v);
    }
}
