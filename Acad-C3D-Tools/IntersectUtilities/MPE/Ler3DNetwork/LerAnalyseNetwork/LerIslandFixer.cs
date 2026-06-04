using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace IntersectUtilities.MPE.Ler3DNetwork.LerAnalyseNetwork
{
    // The lifted geometry for one selected 2D polyline.
    internal sealed record LerIslandFix(ObjectId Id, IReadOnlyList<Point3d> NewPoints);

    // Case 2 island lift: the selected 2D drainage polylines form one or more
    // non-branching chains whose two ends each touch a 3D pipe endpoint in plan.
    // Each end snaps to that 3D endpoint's elevation; every interior vertex
    // interpolates its height linearly by cumulative plan length between the two
    // ends. Pure geometry — the caller performs all database I/O. Selections that
    // branch, that are not a simple two-ended chain, or whose ends do not both
    // touch a 3D pipe are skipped with a warning.
    internal static class LerIslandFixer
    {
        // XY coincidence tolerance (chain joins and end↔3D-endpoint touch).
        private const double Tolerance = 0.000001;

        public static List<LerIslandFix> Solve(
            IReadOnlyList<(ObjectId Id, IReadOnlyList<Point3d> Points)> selected,
            IReadOnlyList<(Point2d Xy, double Z)> anchors3D,
            out List<string> warnings)
        {
            warnings = new List<string>();
            List<LerIslandFix> fixes = new();
            if (selected.Count == 0) return fixes;

            LerNodeIndexer nodes = new(Tolerance);

            // Per selected polyline: the node id of each vertex (to rebuild Z later).
            List<int[]> vertexNodes = new(selected.Count);
            List<(int A, int B, double Len)> edges = new();

            foreach ((ObjectId Id, IReadOnlyList<Point3d> Points) sel in selected)
            {
                IReadOnlyList<Point3d> pts = sel.Points;
                int[] vn = new int[pts.Count];
                for (int i = 0; i < pts.Count; i++) vn[i] = nodes.GetOrAdd(Xy(pts[i]));
                vertexNodes.Add(vn);
                for (int i = 0; i < pts.Count - 1; i++)
                {
                    edges.Add((vn[i], vn[i + 1], Xy(pts[i]).GetDistanceTo(Xy(pts[i + 1]))));
                }
            }

            int n = nodes.Count;
            int[] parent = new int[n];
            for (int i = 0; i < n; i++) parent[i] = i;
            foreach ((int A, int B, _) in edges) Union(parent, A, B);

            Dictionary<int, List<(int Nb, double Len)>> adj = new();
            int[] degree = new int[n];
            foreach ((int A, int B, double Len) e in edges)
            {
                AddAdj(adj, e.A, e.B, e.Len);
                AddAdj(adj, e.B, e.A, e.Len);
                degree[e.A]++;
                degree[e.B]++;
            }

            // Map each 3D pipe endpoint onto a chain node when they coincide; that
            // node's elevation becomes known. Only the chain ends are read below.
            Dictionary<int, double> anchorZ = new();
            foreach ((Point2d Xy, double Z) a in anchors3D)
            {
                int id = nodes.Find(a.Xy);
                if (id >= 0 && !anchorZ.ContainsKey(id)) anchorZ[id] = a.Z;
            }

            // Group nodes by connected component.
            Dictionary<int, List<int>> compNodes = new();
            for (int v = 0; v < n; v++)
            {
                int r = Find(parent, v);
                if (!compNodes.TryGetValue(r, out List<int>? list)) { list = new(); compNodes[r] = list; }
                list.Add(v);
            }

            Dictionary<int, double> z = new();      // global node id -> solved Z
            HashSet<int> solvedRoots = new();
            foreach (KeyValuePair<int, List<int>> comp in compNodes)
            {
                List<int> cn = comp.Value;
                if (cn.All(v => degree[v] == 0)) continue; // lone nodes, no edges

                if (cn.Any(v => degree[v] > 2))
                {
                    warnings.Add("Forgrening i markering – springer kæden over.");
                    continue;
                }

                List<int> ends = cn.Where(v => degree[v] == 1).ToList();
                if (ends.Count != 2)
                {
                    warnings.Add("Markering er ikke en simpel kæde med to ender – springer over.");
                    continue;
                }

                if (!anchorZ.TryGetValue(ends[0], out double z0) ||
                    !anchorZ.TryGetValue(ends[1], out double z1))
                {
                    warnings.Add("En eller begge ender rører ikke et 3D-rør – springer over.");
                    continue;
                }

                if (!TryWalk(ends[0], ends[1], adj, out List<int> order, out List<double> cum, out double total))
                {
                    warnings.Add("Kunne ikke følge kæden – springer over.");
                    continue;
                }

                for (int k = 0; k < order.Count; k++)
                {
                    double frac = total > 1e-12 ? cum[k] / total : 0.0;
                    z[order[k]] = z0 + (frac * (z1 - z0));
                }
                solvedRoots.Add(comp.Key);
            }

            // Rebuild every selected polyline whose component solved.
            for (int s = 0; s < selected.Count; s++)
            {
                int[] vn = vertexNodes[s];
                if (vn.Length == 0) continue;
                if (!solvedRoots.Contains(Find(parent, vn[0]))) continue;

                IReadOnlyList<Point3d> pts = selected[s].Points;
                List<Point3d> newPts = new(pts.Count);
                bool ok = true;
                for (int i = 0; i < pts.Count; i++)
                {
                    if (!z.TryGetValue(vn[i], out double zz)) { ok = false; break; }
                    newPts.Add(new Point3d(pts[i].X, pts[i].Y, zz));
                }
                if (ok) fixes.Add(new LerIslandFix(selected[s].Id, newPts));
            }

            return fixes;
        }

        // Walks the simple path start..end, recording visit order and the
        // cumulative plan length at each node. Fails if the path is not traversable.
        private static bool TryWalk(
            int start, int end,
            Dictionary<int, List<(int Nb, double Len)>> adj,
            out List<int> order, out List<double> cum, out double total)
        {
            order = new List<int> { start };
            cum = new List<double> { 0.0 };
            total = 0.0;

            int prev = -1, curr = start;
            double acc = 0.0;
            HashSet<int> visited = new() { start };
            while (curr != end)
            {
                if (!adj.TryGetValue(curr, out List<(int Nb, double Len)>? nbs)) return false;
                int next = -1;
                double len = 0.0;
                foreach ((int nb, double l) in nbs)
                {
                    if (nb == prev || visited.Contains(nb)) continue;
                    next = nb;
                    len = l;
                    break;
                }
                if (next < 0) return false;
                acc += len;
                order.Add(next);
                cum.Add(acc);
                visited.Add(next);
                prev = curr;
                curr = next;
            }
            total = acc;
            return true;
        }

        private static Point2d Xy(Point3d p) => new Point2d(p.X, p.Y);

        private static void AddAdj(Dictionary<int, List<(int, double)>> adj, int from, int to, double len)
        {
            if (!adj.TryGetValue(from, out List<(int, double)>? list)) { list = new(); adj[from] = list; }
            list.Add((to, len));
        }

        private static int Find(int[] p, int i) { while (p[i] != i) { p[i] = p[p[i]]; i = p[i]; } return i; }
        private static void Union(int[] p, int a, int b) { int ra = Find(p, a), rb = Find(p, b); if (ra != rb) p[rb] = ra; }
    }
}
