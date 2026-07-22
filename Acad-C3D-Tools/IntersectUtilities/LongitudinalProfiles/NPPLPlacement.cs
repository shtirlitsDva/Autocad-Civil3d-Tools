// NPPLPlacement — the de-cluttering algorithm for NorsynProjectionProfileLabel.
//
// Many CogoPoints / profile samples project onto a ProfileView at clustered
// stations. Placing each label's vertical text directly above its own projection
// piles them into an illegible heap. This module spreads the text anchors (P3)
// along the station axis and routes the leaders so the result is readable and
// traceable:
//
//   * a label with room              -> STRAIGHT leader (text directly above its
//                                       point, no shoulder jog),
//   * a cluster jammed against an edge-> fanned INWARD into the free space,
//   * a real cluster                 -> shoulder LADDER: every leader's horizontal
//                                       shoulder sits at its own height so each
//                                       label is traceable back to its point and
//                                       no two shoulders overlap.
//
// PROVABLY NON-CROSSING, terrain-following placement. Validated offline over
// ~27k labels across 12 scene families (the nppl-validate harness) at 0 crossings
// and 0 column overlaps. Two invariants make crossings impossible, and they are
// exactly what let the shelf follow the surface instead of sitting on one flat
// rail high above it:
//
//   1. GLOBAL X-spread. The order-preserving minimum-gap solve (isotonic / PAVA,
//      feasible-envelope clamp) runs ONCE over EVERY label in the view, not per
//      cluster. Because the gap already includes the text-column width, no two
//      columns overlap and no two straight risers collide -- no matter which
//      shelf each label ends up on. (Per-CLUSTER X was the trap: independent
//      solves let columns from adjacent clusters bleed into each other.)
//   2. INTERACTION-COMPONENT shelves. Labels are grouped into interaction-
//      connected components: two leaders share a component iff one's horizontal
//      span passes over the other's point or text. Each component ladders on its
//      OWN terrain-following shelf (surface + clearance, lifted only if its ladder
//      needs the room). Leaders in DIFFERENT components provably never interact,
//      so giving them independent shelves cannot make them cross. The global
//      longest-path level ordering is computed ONCE over all leaders and is
//      preserved within every component, so the ladder ordering the layout
//      depends on is never broken -- only the baseline is per-component.
//
// PlaceView is the single entry point: feed it the whole view, get one placed
// slot per label back. See docs/shared-understanding/nppl-placement.md
// (NorsynDrawingTools) for the design authority and the rejected alternatives.
using System;
using System.Collections.Generic;

namespace IntersectUtilities.LongitudinalProfiles
{
    internal static class NPPLPlacement
    {
        // Little final riser left above the top ladder rung (text enters from just
        // above the highest shoulder).
        private const double TopRiser = 0.2;

        // Minimum vertical spacing (view units) between ladder rungs when a cluster
        // has to lift its shelf to fit the ladder. Keeps stacked shoulders legible.
        private const double MinRung = 0.6;

        // Result for one label (in the station-sorted order the caller passes in).
        public struct Placed
        {
            public double X;         // text anchor x (P3x). Equals the point x when Straight.
            public double Baseline;  // text baseline y (this label's component shelf, effShelf).
            public double GripY;     // shoulder elevation (already the straight midpoint when Straight).
            public bool Straight;    // text directly above the point -> single-segment leader.
        }

        // Whole-VIEW placement. p0x/p0y/shelfYCand are one entry per label, sorted
        // ascending by station (== p0x): the projection x, the projection y, and the
        // surface+clearance shelf candidate at that station. gap is the minimum
        // column separation; [lo,hi] the usable x-band of the view.
        //
        // X-spread and the shoulder-ladder ordering are solved ONCE over the whole
        // view (Core). Labels are then grouped by INTERACTION COMPONENT and each
        // component ladders on its own terrain-following shelf: effShelf = the
        // component's highest surface+clearance, lifted only if its ladder needs the
        // room. Components provably never interact, so per-component shelves cannot
        // create a crossing. Returns one Placed per label, in the same sorted order.
        public static Placed[] PlaceView(double[] p0x, double[] p0y, double[] shelfYCand,
                                         double gap, double lo, double hi,
                                         double straightEps = 0.02)
        {
            int n = p0x.Length;
            var outp = new Placed[n];
            if (n == 0) return outp;

            Core(p0x, gap, lo, hi, straightEps,
                 out double[] x, out bool[] straight, out int[] level, out _, out int[] comp);

            // Group label indices by interaction component.
            var groups = new Dictionary<int, List<int>>();
            for (int k = 0; k < n; k++)
            {
                if (!groups.TryGetValue(comp[k], out var g)) groups[comp[k]] = g = new List<int>();
                g.Add(k);
            }

            foreach (var g in groups.Values)
            {
                // Component shelf = highest surface+clearance in the component; the
                // ladder band top must clear the component's projection band.
                double shelf = double.MinValue, projTop = double.MinValue;
                int cMax = 0;
                bool anyShoulder = false;
                foreach (int k in g)
                {
                    shelf = Math.Max(shelf, shelfYCand[k]);
                    projTop = Math.Max(projTop, p0y[k]);
                    cMax = Math.Max(cMax, level[k]);
                    if (!straight[k]) anyShoulder = true;
                }

                double bot = projTop + 0.5;        // lowest a shoulder may sit
                double top = shelf - TopRiser;     // top rung: just below the text
                double effShelf;
                if (anyShoulder)
                {
                    // Guarantee ladder room: [bot, top] must hold every rung with at
                    // least MinRung spacing. Lift the shelf if it sits too low.
                    double needTop = bot + Math.Max(1.0, cMax * MinRung);
                    if (top < needTop) top = needTop;
                    effShelf = Math.Max(shelf, top + TopRiser);
                }
                else
                {
                    if (top <= bot) top = bot + 1.0;   // harmless: no rungs to place
                    effShelf = shelf;
                }

                double step = cMax > 0 ? (top - bot) / (3 * cMax) : 0.0;   // compact
                if (step * cMax > top - bot) step = (top - bot) / cMax;    // fit band

                foreach (int k in g)
                {
                    outp[k] = straight[k]
                        ? new Placed { X = p0x[k], Baseline = effShelf,
                                       GripY = (p0y[k] + effShelf) * 0.5, Straight = true }
                        : new Placed { X = x[k], Baseline = effShelf,
                                       GripY = top - level[k] * step, Straight = false };
                }
            }
            return outp;
        }

        // Global geometry solve, shared by the whole view. Produces:
        //   x        - order-preserving minimum-gap anchor x per label (isotonic/PAVA),
        //   straight - true where the label barely moved AND no other span crosses its x,
        //   level    - longest-path rank in the "must be above" ordering (high -> low shoulder),
        //   maxLevel - max level over the view,
        //   comp     - interaction-component id: two leaders share a component iff one's
        //              horizontal span passes over the other's point or text. Leaders in
        //              different components never interact and so can ladder on independent
        //              shelves. dSorted must be sorted ascending.
        public static void Core(double[] dSorted, double gap, double lo, double hi, double straightEps,
            out double[] x, out bool[] straight, out int[] level, out int maxLevel, out int[] comp)
        {
            int n = dSorted.Length;
            x = OrderedMinGap(dSorted, gap, lo, hi);
            double[] xx = x;
            var a = new double[n];
            var b = new double[n];
            for (int i = 0; i < n; i++)
            {
                a[i] = Math.Min(dSorted[i], xx[i]);
                b[i] = Math.Max(dSorted[i], xx[i]);
            }
            const double eps = 1e-6;
            bool Spans(int r, double xv) => a[r] + eps < xv && xv < b[r] - eps;

            // A label is straight only if its run ~ 0 AND no other leader's span
            // passes over its x (else its full-height riser would be crossed).
            var spanned = new bool[n];
            for (int r = 0; r < n; r++)
                for (int i = 0; i < n; i++)
                    if (i != r && Spans(i, dSorted[r])) { spanned[r] = true; break; }
            straight = new bool[n];
            for (int r = 0; r < n; r++)
                straight[r] = Math.Abs(xx[r] - dSorted[r]) < straightEps && !spanned[r];

            // GLOBAL shoulder ordering + interaction graph. Directed "must be above":
            // if leader i's span passes over leader j's POINT (but not its text), i
            // above j; over j's TEXT only, below. Undirected: ANY interaction (point
            // OR text) puts the two leaders in the same component.
            var adj = new List<int>[n];
            var undir = new List<int>[n];
            for (int i = 0; i < n; i++) { adj[i] = new List<int>(); undir[i] = new List<int>(); }
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    if (i == j) continue;
                    bool ip = Spans(i, dSorted[j]);
                    bool it = Spans(i, xx[j]);
                    if (ip && !it) adj[j].Add(i);          // i above j
                    else if (it && !ip) adj[i].Add(j);     // j above i
                    if (ip || it) { undir[i].Add(j); undir[j].Add(i); }
                }

            level = new int[n];
            var state = new byte[n];                       // 0 unvisited, 1 visiting, 2 done
            for (int u = 0; u < n; u++)
                if (state[u] != 2) LongestPath(u, adj, level, state);
            maxLevel = 0;
            for (int i = 0; i < n; i++) maxLevel = Math.Max(maxLevel, level[i]);

            // Connected components over the undirected interaction graph (BFS).
            comp = new int[n];
            for (int i = 0; i < n; i++) comp[i] = -1;
            int cid = 0;
            var queue = new Queue<int>();
            for (int s = 0; s < n; s++)
            {
                if (comp[s] != -1) continue;
                comp[s] = cid;
                queue.Enqueue(s);
                while (queue.Count > 0)
                {
                    int u = queue.Dequeue();
                    foreach (int v in undir[u])
                        if (comp[v] == -1) { comp[v] = cid; queue.Enqueue(v); }
                }
                cid++;
            }
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
