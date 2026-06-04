using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace IntersectUtilities.MPE.Ler3DNetwork.LerAnalyseNetwork
{
    // The scanner. Builds the 2D chain graph (each polyline is one edge between its
    // endpoint nodes), marks the nodes where a pivot — a 3D pipe, projected to XY —
    // lands, then runs a multi-source BFS out from every pivot. For each polyline it
    // records two depth-independent distances: PivotDepth (fewest polylines from the
    // nearest pivot) and BridgeCost (fewest polylines on a SIMPLE path joining two
    // different pivots through it). The caller thresholds these against the scan
    // depth to label each polyline bridge / floating / out-of-range.
    //
    // Bridges are tested directionally: for an edge (u,v) the pivot reached from u
    // must leave u by a neighbour other than v, and the pivot from v must leave by a
    // neighbour other than u. That keeps the two halves vertex-disjoint, so a stub
    // hanging off a bridge node (a free dangling end) is NOT mistaken for a bridge.
    internal static class LerAnalyseNetworkAnalyzer
    {
        // Coincidence tolerance (projected 3D endpoint vs a 2D polyline endpoint, and
        // 2D endpoints joining into the chain graph). Tiny; not user-facing.
        public const double Tolerance = 0.000001;

        // The BFS is capped here so a pathological network can't flood unbounded; far
        // beyond any sane drainage bridge length.
        private const int MaxScan = 128;

        public static List<LerScannedPolyline> Analyze(
            IReadOnlyList<LerClassifiedLine> targets3D,
            IReadOnlyList<LerClassifiedLine> subjects2D,
            out int maxBridgeDepth)
        {
            maxBridgeDepth = 1;
            List<LerScannedPolyline> result = new();
            if (subjects2D.Count == 0) return result;

            // Nodes = 2D polyline endpoints; edge i = polyline i between node a[i],b[i].
            LerNodeIndexer nodes = new(Tolerance);
            int m = subjects2D.Count;
            int[] a = new int[m], b = new int[m];
            for (int i = 0; i < m; i++)
            {
                IReadOnlyList<Point3d> p = subjects2D[i].Points;
                a[i] = nodes.GetOrAdd(Xy(p[0]));
                b[i] = nodes.GetOrAdd(Xy(p[p.Count - 1]));
            }

            int n = nodes.Count;
            Dictionary<int, List<int>> adj = new();
            for (int i = 0; i < m; i++)
            {
                AddAdj(adj, a[i], b[i]);
                AddAdj(adj, b[i], a[i]);
            }

            // Pivot anchors: a 2D node coincident with a 3D pipe (pivot) endpoint.
            List<(int Node, int Pivot)> anchors = new();
            for (int pi = 0; pi < targets3D.Count; pi++)
            {
                IReadOnlyList<Point3d> p = targets3D[pi].Points;
                AddAnchor(nodes, anchors, p[0], pi);
                AddAnchor(nodes, anchors, p[p.Count - 1], pi);
            }

            // Per node, the two nearest pivots that arrive via DISTINCT first-hop
            // neighbours: (pivot, distance, hop). hop is the neighbour the pivot's
            // shortest path leaves this node by (-1 = the pivot is at this node).
            int[] pv1 = new int[n], pv2 = new int[n];
            int[] dd1 = new int[n], dd2 = new int[n];
            int[] hop1 = new int[n], hop2 = new int[n];
            for (int i = 0; i < n; i++) { pv1[i] = pv2[i] = -1; dd1[i] = dd2[i] = int.MaxValue; }

            // Multi-source BFS. Each (node,pivot) settles once at its shortest distance
            // (BFS order), carrying the neighbour it arrived from as the node's hop.
            Queue<(int Node, int Pivot, int Dist, int Hop)> queue = new();
            HashSet<(int, int)> visited = new();
            foreach ((int node, int pivot) in anchors) queue.Enqueue((node, pivot, 0, -1));
            while (queue.Count > 0)
            {
                (int nd, int pv, int dist, int hop) = queue.Dequeue();
                if (dist > MaxScan) continue;
                if (!visited.Add((nd, pv))) continue;

                if (pv1[nd] == -1) { pv1[nd] = pv; dd1[nd] = dist; hop1[nd] = hop; }
                else if (pv2[nd] == -1 && hop != hop1[nd]) { pv2[nd] = pv; dd2[nd] = dist; hop2[nd] = hop; }

                if (adj.TryGetValue(nd, out List<int>? nbs))
                {
                    foreach (int nb in nbs)
                    {
                        if (dist + 1 <= MaxScan) queue.Enqueue((nb, pv, dist + 1, nd));
                    }
                }
            }

            // Per polyline: PivotDepth (nearest pivot to either end) and BridgeCost
            // (cheapest different-pivot simple path through it).
            for (int i = 0; i < m; i++)
            {
                int u = a[i], v = b[i];

                int near = Math.Min(dd1[u], dd1[v]);
                int pivotDepth = near == int.MaxValue ? int.MaxValue : near + 1;

                int bridgeCost = BridgeCost(u, v, pv1, dd1, hop1, pv2, dd2, hop2);
                if (bridgeCost != int.MaxValue && bridgeCost > maxBridgeDepth) maxBridgeDepth = bridgeCost;

                result.Add(new LerScannedPolyline(subjects2D[i].Id, subjects2D[i].Points, bridgeCost, pivotDepth));
            }

            return result;
        }

        // Cheapest pivot_A .. u — v .. pivot_B with A != B, where A is reached from u
        // leaving by a neighbour other than v, and B from v leaving by other than u.
        private static int BridgeCost(
            int u, int v,
            int[] pv1, int[] dd1, int[] hop1,
            int[] pv2, int[] dd2, int[] hop2)
        {
            Span<(int Pipe, int Dist)> us = stackalloc (int Pipe, int Dist)[2];
            Span<(int Pipe, int Dist)> vs = stackalloc (int Pipe, int Dist)[2];
            int nu = SideCandidates(u, v, pv1, dd1, hop1, pv2, dd2, hop2, us);
            int nv = SideCandidates(v, u, pv1, dd1, hop1, pv2, dd2, hop2, vs);

            int best = int.MaxValue;
            for (int i = 0; i < nu; i++)
            {
                for (int j = 0; j < nv; j++)
                {
                    if (us[i].Pipe == vs[j].Pipe) continue;
                    int cost = us[i].Dist + vs[j].Dist + 1;
                    if (cost < best) best = cost;
                }
            }
            return best;
        }

        // The pivots reachable from `node` without leaving toward `avoid`, taken from
        // its two distinct-first-hop entries. Returns the count written into `into`.
        private static int SideCandidates(
            int node, int avoid,
            int[] pv1, int[] dd1, int[] hop1,
            int[] pv2, int[] dd2, int[] hop2,
            Span<(int Pipe, int Dist)> into)
        {
            int k = 0;
            if (pv1[node] != -1 && hop1[node] != avoid) into[k++] = (pv1[node], dd1[node]);
            if (pv2[node] != -1 && hop2[node] != avoid) into[k++] = (pv2[node], dd2[node]);
            return k;
        }

        private static void AddAnchor(LerNodeIndexer nodes, List<(int, int)> anchors, Point3d endpoint, int pivot)
        {
            int id = nodes.Find(Xy(endpoint));
            if (id >= 0) anchors.Add((id, pivot));
        }

        private static void AddAdj(Dictionary<int, List<int>> adj, int from, int to)
        {
            if (!adj.TryGetValue(from, out List<int>? list)) { list = new(); adj[from] = list; }
            list.Add(to);
        }

        private static Point2d Xy(Point3d p) => new Point2d(p.X, p.Y);
    }
}
