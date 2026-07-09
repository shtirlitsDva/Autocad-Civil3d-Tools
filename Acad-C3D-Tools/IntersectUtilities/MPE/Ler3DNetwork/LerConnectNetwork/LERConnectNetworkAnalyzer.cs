using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using CadColor = Autodesk.AutoCAD.Colors.Color;
using static IntersectUtilities.UtilsCommon.Utils;

namespace IntersectUtilities.MPE.Ler3DNetwork.LerConnectNetwork
{
    // Pure geometry/analysis for LERConnectNetwork. Everything operates on the
    // point lists snapshotted at Gather time, so no transaction or document is
    // required here — the only AutoCAD types used are value-type geometry
    // (Point2d/Point3d/Vector2d) and Color.
    internal static class LERConnectNetworkAnalyzer
    {
        private const double Epsilon = 1e-9;

        // Endpoint match tolerance for joining touching 3D lines into networks.
        private const double EndpointTolerance = 0.01;

        // Distinct ACI indices cycled to give each network its own preview colour.
        private static readonly short[] NetworkAciColors =
            { 1, 2, 3, 4, 5, 6, 30, 40, 50, 90, 130, 170, 210, 11, 21, 31 };

        // ---- Network grouping (connected components over touching endpoints) -

        public static List<LERNetwork> BuildNetworks(IReadOnlyList<LerClassifiedLine> threeDLines)
        {
            int n = threeDLines.Count;
            int[] parent = new int[n];
            for (int i = 0; i < n; i++) parent[i] = i;

            // Spatial hash keyed by a cell of side = tolerance. Two endpoints
            // within tolerance are at most one cell apart per axis, so scanning
            // the 3x3x3 neighbourhood and verifying real Euclidean distance is a
            // true tolerance join — not the previous single-cell exact match,
            // which split pairs straddling a cell boundary and merged distinct
            // endpoints that happened to round into the same cell.
            double tol2 = EndpointTolerance * EndpointTolerance;
            Dictionary<(long, long, long), List<(int Index, Point3d Point)>> grid = new();
            for (int i = 0; i < n; i++)
            {
                IReadOnlyList<Point3d> pts = threeDLines[i].Points;
                if (pts.Count == 0) continue;
                RegisterEndpoint(pts[0], i, grid, parent, tol2);
                RegisterEndpoint(pts[pts.Count - 1], i, grid, parent, tol2);
            }

            // T-junction merge. Endpoint hashing above only joins lines that touch
            // end-to-end. District-heating mains routinely branch as a tee, where a
            // branch's endpoint lands on the MIDDLE of another line's segment. Union
            // those too, so both a star (shared end node) and a tee collapse into one
            // network. Without this, "how many mains does this stub touch" miscounts a
            // tee as two mains and wrongly skips a valid single-main connection.
            // Cost is O(n^2 x segments) worst case, but the Find short-circuit skips
            // any pair already in the same component, which collapses fast on real,
            // well-connected mains. This runs once per "Opdater forhåndsvisning".
            for (int i = 0; i < n; i++)
            {
                IReadOnlyList<Point3d> pi = threeDLines[i].Points;
                if (pi.Count == 0) continue;
                Point3d iStart = pi[0];
                Point3d iEnd = pi[pi.Count - 1];
                for (int j = 0; j < n; j++)
                {
                    if (i == j) continue;
                    if (Find(parent, i) == Find(parent, j)) continue; // already one network
                    IReadOnlyList<Point3d> pj = threeDLines[j].Points;
                    for (int s = 0; s < pj.Count - 1; s++)
                    {
                        if (SquaredPointToSegment(iStart, pj[s], pj[s + 1]) <= tol2
                            || SquaredPointToSegment(iEnd, pj[s], pj[s + 1]) <= tol2)
                        {
                            Union(parent, i, j);
                            break;
                        }
                    }
                }
            }

            // Materialise components in first-seen order so colours are stable.
            Dictionary<int, LERNetwork> byRoot = new();
            List<LERNetwork> networks = new();
            for (int i = 0; i < n; i++)
            {
                int root = Find(parent, i);
                if (!byRoot.TryGetValue(root, out LERNetwork? network))
                {
                    network = new LERNetwork(networks.Count, ColorForIndex(networks.Count));
                    byRoot[root] = network;
                    networks.Add(network);
                }
                network.MemberIds.Add(threeDLines[i].Id);
                network.MemberPoints.Add(threeDLines[i].Points);
            }
            return networks;
        }

        private static void RegisterEndpoint(
            Point3d endpoint,
            int lineIndex,
            Dictionary<(long, long, long), List<(int Index, Point3d Point)>> grid,
            int[] parent,
            double tol2)
        {
            (long X, long Y, long Z) baseCell = Cell(endpoint);

            // Union with any already-registered endpoint within true distance.
            for (long dx = -1; dx <= 1; dx++)
            {
                for (long dy = -1; dy <= 1; dy++)
                {
                    for (long dz = -1; dz <= 1; dz++)
                    {
                        (long, long, long) key = (baseCell.X + dx, baseCell.Y + dy, baseCell.Z + dz);
                        if (!grid.TryGetValue(key, out List<(int Index, Point3d Point)>? bucket)) continue;
                        foreach ((int Index, Point3d Point) other in bucket)
                        {
                            if (SquaredDistance(endpoint, other.Point) <= tol2)
                            {
                                Union(parent, lineIndex, other.Index);
                            }
                        }
                    }
                }
            }

            (long, long, long) ownKey = baseCell;
            if (!grid.TryGetValue(ownKey, out List<(int Index, Point3d Point)>? own))
            {
                own = new List<(int, Point3d)>();
                grid[ownKey] = own;
            }
            own.Add((lineIndex, endpoint));
        }

        // Cell of side = tolerance, using floor so any two points within the
        // tolerance fall in cells at most one step apart on each axis.
        private static (long X, long Y, long Z) Cell(Point3d p)
        {
            return (
                (long)Math.Floor(p.X / EndpointTolerance),
                (long)Math.Floor(p.Y / EndpointTolerance),
                (long)Math.Floor(p.Z / EndpointTolerance));
        }

        private static double SquaredDistance(Point3d a, Point3d b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            double dz = a.Z - b.Z;
            return (dx * dx) + (dy * dy) + (dz * dz);
        }

        // Squared 3D distance from point q to segment a->b (clamped to the span).
        private static double SquaredPointToSegment(Point3d q, Point3d a, Point3d b)
        {
            Vector3d ab = b - a;
            double len2 = ab.DotProduct(ab);
            if (len2 < Epsilon) return SquaredDistance(q, a);
            double t = Math.Clamp((q - a).DotProduct(ab) / len2, 0.0, 1.0);
            Point3d closest = a + (ab * t);
            return SquaredDistance(q, closest);
        }

        private static int Find(int[] parent, int i)
        {
            while (parent[i] != i)
            {
                parent[i] = parent[parent[i]];
                i = parent[i];
            }
            return i;
        }

        private static void Union(int[] parent, int a, int b)
        {
            int ra = Find(parent, a);
            int rb = Find(parent, b);
            if (ra != rb) parent[rb] = ra;
        }

        public static CadColor ColorForIndex(int index)
        {
            short aci = NetworkAciColors[index % NetworkAciColors.Length];
            return CadColor.FromColorIndex(ColorMethod.ByAci, aci);
        }

        // ---- Main assignment ------------------------------------------------

        // For one 2D line, find the nearest network in XY measured from either
        // endpoint. A stub that truly crosses a network counts as touching it
        // (distance 0), so mid-span crossings are assigned even when both
        // endpoints sit farther than maxDistance. Returns null when nothing is
        // within maxDistance.
        public static LERMainAssignment? AssignMain(
            IReadOnlyList<Point3d> twoDPoints,
            IReadOnlyList<LERNetwork> networks,
            double maxDistance)
        {
            if (twoDPoints.Count < 2 || networks.Count == 0)
            {
                return null;
            }

            Point2d startXy = Xy(twoDPoints[0]);
            Point2d endXy = Xy(twoDPoints[twoDPoints.Count - 1]);

            double bestDistance = double.MaxValue;
            int bestNetwork = -1;
            bool bestConnectAtEnd = false;

            for (int ni = 0; ni < networks.Count; ni++)
            {
                double startDist = MinXyDistanceToNetwork(startXy, networks[ni]);
                double endDist = MinXyDistanceToNetwork(endXy, networks[ni]);

                // A real crossing means the stub touches the main; treat both ends
                // as distance 0 so the crossing is always assigned.
                if (StubCrossesNetwork(twoDPoints, networks[ni]))
                {
                    startDist = 0.0;
                    endDist = 0.0;
                }

                if (startDist < bestDistance)
                {
                    bestDistance = startDist;
                    bestNetwork = ni;
                    bestConnectAtEnd = false;
                }

                if (endDist < bestDistance)
                {
                    bestDistance = endDist;
                    bestNetwork = ni;
                    bestConnectAtEnd = true;
                }
            }

            if (bestNetwork < 0 || bestDistance > maxDistance)
            {
                return null;
            }

            return new LERMainAssignment(networks[bestNetwork].Id, bestConnectAtEnd, bestDistance);
        }

        // True when any segment of the stub truly crosses (segment-segment XY
        // intersection within both spans) any member segment of the network.
        private static bool StubCrossesNetwork(IReadOnlyList<Point3d> twoDPoints, LERNetwork network)
        {
            for (int i = 0; i < twoDPoints.Count - 1; i++)
            {
                Point2d p0 = Xy(twoDPoints[i]);
                Point2d p1 = Xy(twoDPoints[i + 1]);
                foreach (IReadOnlyList<Point3d> member in network.MemberPoints)
                {
                    for (int j = 0; j < member.Count - 1; j++)
                    {
                        if (TrySegmentIntersection(p0, p1, Xy(member[j]), Xy(member[j + 1]), out _, out _))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        // How many DISTINCT networks (mains) this stub touches. A stub "touches" a
        // network when it either crosses the network in XY OR any point along the
        // stub lies within `threshold` of the network in XY. One touched main is a
        // normal connection; two or more is ambiguous and gets flagged for manual
        // review. Branches meeting at a junction are already one network (see
        // BuildNetworks), so a stub near a star/tee junction still counts as one.
        private static int CountNetworksInvolved(
            IReadOnlyList<Point3d> twoDPoints,
            IReadOnlyList<LERNetwork> networks,
            double threshold)
        {
            int count = 0;
            foreach (LERNetwork network in networks)
            {
                if (StubCrossesNetwork(twoDPoints, network)
                    || StubWithinThresholdOfNetwork(twoDPoints, network, threshold))
                {
                    count++;
                }
            }
            return count;
        }

        // True when any part of the stub lies within `threshold` of any member of
        // the network in XY (true segment-to-segment distance, not just endpoints).
        private static bool StubWithinThresholdOfNetwork(
            IReadOnlyList<Point3d> twoDPoints,
            LERNetwork network,
            double threshold)
        {
            for (int i = 0; i < twoDPoints.Count - 1; i++)
            {
                Point2d s0 = Xy(twoDPoints[i]);
                Point2d s1 = Xy(twoDPoints[i + 1]);
                foreach (IReadOnlyList<Point3d> member in network.MemberPoints)
                {
                    for (int j = 0; j < member.Count - 1; j++)
                    {
                        if (SegmentSegmentXyDistance(s0, s1, Xy(member[j]), Xy(member[j + 1])) <= threshold)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        // Shortest XY distance between two segments: 0 if they cross, else the
        // minimum of the four endpoint-to-opposite-segment distances.
        private static double SegmentSegmentXyDistance(Point2d p0, Point2d p1, Point2d q0, Point2d q1)
        {
            if (TrySegmentIntersection(p0, p1, q0, q1, out _, out _))
            {
                return 0.0;
            }

            double d0 = XyDistanceToSegment(p0, q0, q1, out _, out _);
            double d1 = XyDistanceToSegment(p1, q0, q1, out _, out _);
            double d2 = XyDistanceToSegment(q0, p0, p1, out _, out _);
            double d3 = XyDistanceToSegment(q1, p0, p1, out _, out _);
            return Math.Min(Math.Min(d0, d1), Math.Min(d2, d3));
        }

        private static double MinXyDistanceToNetwork(Point2d q, LERNetwork network)
        {
            double best = double.MaxValue;
            foreach (IReadOnlyList<Point3d> member in network.MemberPoints)
            {
                for (int i = 0; i < member.Count - 1; i++)
                {
                    double d = XyDistanceToSegment(q, Xy(member[i]), Xy(member[i + 1]), out _, out _);
                    if (d < best) best = d;
                }
            }
            return best;
        }

        // ---- Connection solve -----------------------------------------------

        // A connector whose extension (the added segment from the connecting end to
        // the main) exceeds this multiple of the check distance is flagged TooLong,
        // so Apply skips it and the operator resolves it by hand. Keeps the tool from
        // extending unreasonably far when a stub grazes its main at a shallow angle.
        private const double TooLongFactor = 2.0;

        // Two modes. CROSSING: if the stik already crosses the main, pivot at that
        // crossing X1 (at the main's real elevation, the lowest point) and mutate the
        // stik in place — a V where BOTH endpoints rise away from the pivot at the
        // slope. EXTEND (this comment's path): otherwise extend the connecting end C
        // along its tangent, intersect the main in XY, lift to the main's real Z, then
        // slope the rebuilt line upward away from that pivot, appending X (A..C,X).
        // Connectors are always built when a local main exists; problematic ones
        // (forward extension misses the main, stub > 20x the check distance, or the
        // stub crosses two or more mains) are still Connected but carry an Error flag.
        public static LERConnectionResult Solve(
            IReadOnlyList<Point3d> twoDPoints,
            ObjectId sourceId,
            LERNetwork main,
            bool connectAtEnd,
            double permille,
            double distance,
            IReadOnlyList<LERNetwork> allNetworks)
        {
            int n = twoDPoints.Count;
            if (n < 2)
            {
                return new LERConnectionResult(sourceId, null, main.Id, LERConnectionStatus.Degenerate);
            }

            // A stub that touches two or more DISTINCT mains — by crossing them or by
            // running within the check distance of them — is ambiguous: still built
            // against its assigned main, but flagged so Apply skips it for manual
            // review. Junction branches count as one main (BuildNetworks merges them).
            LERConnectionError baseError = CountNetworksInvolved(twoDPoints, allNetworks, distance) >= 2
                ? LERConnectionError.MultipleMains
                : LERConnectionError.None;

            int cIndex = connectAtEnd ? n - 1 : 0;
            int bIndex = connectAtEnd ? n - 2 : 1;

            Point2d c = Xy(twoDPoints[cIndex]);
            Point2d b = Xy(twoDPoints[bIndex]);

            // Crossing mode: if the stik already crosses the main at X1, pivot there
            // and mutate the stik in place — a straight V through X1 (at the main's
            // elevation), with BOTH endpoints rising from the pivot at the slope. No
            // extension, no vertex inserted, so the rebuilt point count matches the
            // original (an in-place Z move on apply).
            if (TryFindCrossing(twoDPoints, c, main, out Point2d x1, out double x1z, out double arcAtX1, out _))
            {
                double f = permille / 1000.0;
                List<Point3d> mutated = new(n);
                double cum = 0.0;
                for (int i = 0; i < n; i++)
                {
                    if (i > 0) cum += Xy(twoDPoints[i - 1]).GetDistanceTo(Xy(twoDPoints[i]));
                    double zCross = x1z + (f * Math.Abs(cum - arcAtX1));
                    mutated.Add(new Point3d(twoDPoints[i].X, twoDPoints[i].Y, zCross));
                }
                return new LERConnectionResult(sourceId, mutated, main.Id, LERConnectionStatus.Connected, baseError);
            }

            Vector2d tangent = c - b;
            if (tangent.Length < Epsilon)
            {
                return new LERConnectionResult(sourceId, null, main.Id, LERConnectionStatus.Degenerate);
            }
            tangent = tangent / tangent.Length;

            if (!TryFindLocalIntersection(c, tangent, main, out Point2d hitXy, out double hitZ, out bool cleanHit))
            {
                return new LERConnectionResult(sourceId, null, main.Id, LERConnectionStatus.NoIntersection);
            }

            // Flag (but still build) problematic connectors: a forward extension that
            // never truly reaches the main, or a stub far longer than the check distance.
            LERConnectionError error = baseError;
            if (!cleanHit) error |= LERConnectionError.MissesMain;
            if (c.GetDistanceTo(hitXy) > TooLongFactor * distance) error |= LERConnectionError.TooLong;

            Point3d x = new Point3d(hitXy.X, hitXy.Y, hitZ);

            // Walk from the pivot X outward through C and on toward the far end,
            // accumulating horizontal length; Z rises by permille per metre.
            double factor = permille / 1000.0;
            double[] z = new double[n];

            int[] order = BuildOutwardOrder(n, connectAtEnd);
            double cumulative = c.GetDistanceTo(hitXy);
            z[order[0]] = hitZ + (factor * cumulative);
            for (int k = 1; k < order.Length; k++)
            {
                Point2d prev = Xy(twoDPoints[order[k - 1]]);
                Point2d curr = Xy(twoDPoints[order[k]]);
                cumulative += prev.GetDistanceTo(curr);
                z[order[k]] = hitZ + (factor * cumulative);
            }

            List<Point3d> rebuilt = new(n + 1);
            if (connectAtEnd)
            {
                for (int i = 0; i < n; i++)
                {
                    rebuilt.Add(new Point3d(twoDPoints[i].X, twoDPoints[i].Y, z[i]));
                }
                rebuilt.Add(x);
            }
            else
            {
                rebuilt.Add(x);
                for (int i = 0; i < n; i++)
                {
                    rebuilt.Add(new Point3d(twoDPoints[i].X, twoDPoints[i].Y, z[i]));
                }
            }

            return new LERConnectionResult(sourceId, rebuilt, main.Id, LERConnectionStatus.Connected, error);
        }

        // Original-index order starting at the connecting end C and walking to
        // the far end (used to accumulate length from the pivot).
        private static int[] BuildOutwardOrder(int n, bool connectAtEnd)
        {
            int[] order = new int[n];
            if (connectAtEnd)
            {
                for (int k = 0; k < n; k++) order[k] = n - 1 - k;
            }
            else
            {
                for (int k = 0; k < n; k++) order[k] = k;
            }
            return order;
        }

        // True when the stik truly crosses the main (a real segment-segment XY
        // intersection within both spans). Returns the crossing X1 nearest to `nearTo`
        // (the connecting endpoint), its main-interpolated Z, X1's plan arc-length
        // along the stik, and the stik's total plan length.
        private static bool TryFindCrossing(
            IReadOnlyList<Point3d> twoDPoints,
            Point2d nearTo,
            LERNetwork main,
            out Point2d x1,
            out double x1z,
            out double arcAtX1,
            out double totalLen)
        {
            x1 = default;
            x1z = 0.0;
            arcAtX1 = 0.0;

            int n = twoDPoints.Count;
            double[] cum = new double[n];
            for (int i = 1; i < n; i++)
            {
                cum[i] = cum[i - 1] + Xy(twoDPoints[i - 1]).GetDistanceTo(Xy(twoDPoints[i]));
            }
            totalLen = cum[n - 1];

            bool found = false;
            double bestDist = double.MaxValue;
            for (int i = 0; i < n - 1; i++)
            {
                Point2d p0 = Xy(twoDPoints[i]);
                Point2d p1 = Xy(twoDPoints[i + 1]);
                foreach (IReadOnlyList<Point3d> member in main.MemberPoints)
                {
                    for (int j = 0; j < member.Count - 1; j++)
                    {
                        if (!TrySegmentIntersection(p0, p1, Xy(member[j]), Xy(member[j + 1]),
                                out double tP, out double tQ))
                        {
                            continue;
                        }

                        Point2d pt = new Point2d(p0.X + (tP * (p1.X - p0.X)), p0.Y + (tP * (p1.Y - p0.Y)));
                        double dist = nearTo.GetDistanceTo(pt);
                        if (dist < bestDist)
                        {
                            bestDist = dist;
                            x1 = pt;
                            x1z = member[j].Z + (tQ * (member[j + 1].Z - member[j].Z));
                            arcAtX1 = cum[i] + (tP * p0.GetDistanceTo(p1));
                            found = true;
                        }
                    }
                }
            }
            return found;
        }

        // 2D segment-segment intersection. Outputs the parameters along each segment
        // (tP on p0->p1, tQ on q0->q1); returns true only when both lie within [0,1].
        private static bool TrySegmentIntersection(
            Point2d p0, Point2d p1, Point2d q0, Point2d q1,
            out double tP, out double tQ)
        {
            tP = 0.0;
            tQ = 0.0;
            Vector2d r = p1 - p0;
            Vector2d s = q1 - q0;
            double denom = (r.X * s.Y) - (r.Y * s.X);
            if (Math.Abs(denom) < Epsilon)
            {
                return false; // parallel or colinear
            }

            Vector2d qp = q0 - p0;
            tP = ((qp.X * s.Y) - (qp.Y * s.X)) / denom;
            tQ = ((qp.X * r.Y) - (qp.Y * r.X)) / denom;
            return tP >= -Epsilon && tP <= 1.0 + Epsilon
                && tQ >= -Epsilon && tQ <= 1.0 + Epsilon;
        }

        // Connect C to the LOCAL main by extending along the stub's tangent `dir`.
        // Searches ALL segments of the assigned main (not just the nearest one) and
        // returns the CLOSEST forward intersection that actually lands within a
        // segment span — so a stub near a bend or tee still connects even when the
        // single nearest segment happens to be parallel to the tangent. If no clean
        // forward hit exists on any segment, falls back to the nearest point on the
        // main (best-effort, cleanHit = false) so the caller can flag it. Returns
        // false only when the main has no segment at all. Z is interpolated along the
        // hit segment from its real elevations.
        private static bool TryFindLocalIntersection(
            Point2d c,
            Vector2d dir,
            LERNetwork main,
            out Point2d hitXy,
            out double hitZ,
            out bool cleanHit)
        {
            hitXy = c;
            hitZ = 0.0;
            cleanHit = false;

            // Best clean forward intersection: smallest t >= 0 that lands on a span.
            double bestT = double.MaxValue;
            bool anyClean = false;

            // Best-effort fallback: nearest point on the main by perpendicular XY.
            double bestFallbackDist = double.MaxValue;
            bool anyFallback = false;
            Point2d fallbackXy = c;
            double fallbackZ = 0.0;

            foreach (IReadOnlyList<Point3d> member in main.MemberPoints)
            {
                for (int i = 0; i < member.Count - 1; i++)
                {
                    Point3d segA = member[i];
                    Point3d segB = member[i + 1];
                    Point2d a = Xy(segA);
                    Point2d b = Xy(segB);

                    // Track the nearest point on this segment for the fallback.
                    double perp = XyDistanceToSegment(c, a, b, out double tSeg, out _);
                    if (perp < bestFallbackDist)
                    {
                        bestFallbackDist = perp;
                        fallbackXy = new Point2d(a.X + ((b.X - a.X) * tSeg), a.Y + ((b.Y - a.Y) * tSeg));
                        fallbackZ = segA.Z + (tSeg * (segB.Z - segA.Z));
                        anyFallback = true;
                    }

                    // Forward tangent-line vs. segment-line intersection.
                    Vector2d e = b - a;
                    double det = (e.X * dir.Y) - (dir.X * e.Y);
                    if (Math.Abs(det) < Epsilon)
                    {
                        continue; // tangent parallel to this segment — try the next one
                    }

                    Vector2d w = a - c;
                    double u = ((dir.X * w.Y) - (w.X * dir.Y)) / det; // param along the segment
                    double t = ((e.X * w.Y) - (w.X * e.Y)) / det;     // distance along dir to the hit
                    if (t < -Epsilon) continue;                        // behind C, not a forward extension
                    if (u < -Epsilon || u > 1.0 + Epsilon) continue;   // misses this segment's span

                    if (t < bestT)
                    {
                        bestT = t;
                        double uc = Math.Clamp(u, 0.0, 1.0);
                        hitXy = new Point2d(a.X + (e.X * uc), a.Y + (e.Y * uc));
                        hitZ = segA.Z + (uc * (segB.Z - segA.Z));
                        anyClean = true;
                    }
                }
            }

            if (anyClean)
            {
                cleanHit = true;
                return true;
            }

            if (anyFallback)
            {
                hitXy = fallbackXy;
                hitZ = fallbackZ;
                cleanHit = false; // caller flags this as MissesMain
                return true;
            }

            return false; // main had no segment
        }

        // ---- Small geometry helpers -----------------------------------------

        private static Point2d Xy(Point3d p) => new Point2d(p.X, p.Y);

        private static double XyDistanceToSegment(Point2d q, Point2d a, Point2d b, out double t, out Point2d closest)
        {
            Vector2d ab = b - a;
            double len2 = ab.DotProduct(ab);
            if (len2 < Epsilon)
            {
                t = 0.0;
                closest = a;
                return q.GetDistanceTo(a);
            }

            t = Math.Clamp((q - a).DotProduct(ab) / len2, 0.0, 1.0);
            closest = a + (ab * t);
            return q.GetDistanceTo(closest);
        }
    }
}
