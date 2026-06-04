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

        // ---- Parent assignment ----------------------------------------------

        // For one 2D line, find the nearest network in XY measured from either
        // endpoint. Returns null when nothing is within maxDistance.
        public static LERParentAssignment? AssignParent(
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
                if (startDist < bestDistance)
                {
                    bestDistance = startDist;
                    bestNetwork = ni;
                    bestConnectAtEnd = false;
                }

                double endDist = MinXyDistanceToNetwork(endXy, networks[ni]);
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

            return new LERParentAssignment(networks[bestNetwork].Id, bestConnectAtEnd, bestDistance);
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

        // Extend the connecting end C along its tangent, intersect the parent in
        // XY, lift to the parent's real Z, then slope the rebuilt line upward
        // away from that pivot. Returns the rebuilt point list (A..C,X) or a
        // non-Connected status.
        public static LERConnectionResult Solve(
            IReadOnlyList<Point3d> twoDPoints,
            ObjectId sourceId,
            LERNetwork parent,
            bool connectAtEnd,
            double permille,
            double maxConnect)
        {
            int n = twoDPoints.Count;
            if (n < 2)
            {
                return new LERConnectionResult(sourceId, null, parent.Id, LERConnectionStatus.Degenerate);
            }

            int cIndex = connectAtEnd ? n - 1 : 0;
            int bIndex = connectAtEnd ? n - 2 : 1;

            Point2d c = Xy(twoDPoints[cIndex]);
            Point2d b = Xy(twoDPoints[bIndex]);

            Vector2d tangent = c - b;
            if (tangent.Length < Epsilon)
            {
                return new LERConnectionResult(sourceId, null, parent.Id, LERConnectionStatus.Degenerate);
            }
            tangent = tangent / tangent.Length;

            if (!TryFindLocalIntersection(c, tangent, parent, maxConnect, out Point2d hitXy, out double hitZ))
            {
                return new LERConnectionResult(sourceId, null, parent.Id, LERConnectionStatus.NoIntersection);
            }

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

            return new LERConnectionResult(sourceId, rebuilt, parent.Id, LERConnectionStatus.Connected);
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

        // Connect C to the LOCAL main: find the parent segment nearest to C in XY,
        // then intersect the tangent (as a full line, so a slightly overshot C
        // still connects to its adjacent main) with that one segment's line. The
        // hit is clamped onto the segment span and rejected if it lands further
        // than maxConnect from C — that scoping is what prevents the tangent from
        // striking a distant segment elsewhere in a large connected network.
        // Z is interpolated along the nearest segment from its real elevations.
        private static bool TryFindLocalIntersection(
            Point2d c,
            Vector2d dir,
            LERNetwork parent,
            double maxConnect,
            out Point2d hitXy,
            out double hitZ)
        {
            hitXy = c;
            hitZ = 0.0;

            // 1. Nearest parent segment to C (by perpendicular XY distance).
            double bestDist = double.MaxValue;
            Point3d nearA = default;
            Point3d nearB = default;
            bool any = false;
            foreach (IReadOnlyList<Point3d> member in parent.MemberPoints)
            {
                for (int i = 0; i < member.Count - 1; i++)
                {
                    double d = XyDistanceToSegment(c, Xy(member[i]), Xy(member[i + 1]), out _, out _);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        nearA = member[i];
                        nearB = member[i + 1];
                        any = true;
                    }
                }
            }
            if (!any)
            {
                return false;
            }

            // 2. Intersect the tangent line through C with the nearest segment's line.
            Point2d a = Xy(nearA);
            Point2d b = Xy(nearB);
            Vector2d e = b - a;
            double det = (e.X * dir.Y) - (dir.X * e.Y);
            if (Math.Abs(det) < Epsilon)
            {
                return false; // tangent parallel to the local main
            }

            Vector2d w = a - c;
            double u = ((dir.X * w.Y) - (w.X * dir.Y)) / det; // param along the segment
            double uc = Math.Clamp(u, 0.0, 1.0);

            hitXy = new Point2d(a.X + (e.X * uc), a.Y + (e.Y * uc));
            hitZ = nearA.Z + (uc * (nearB.Z - nearA.Z));

            // 3. Reject pathological far connections (e.g. near-parallel tangents).
            return c.GetDistanceTo(hitXy) <= maxConnect;
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
