using Autodesk.AutoCAD.Geometry;

using NTRExport.TopologyModel;
using NTRExport.Enums;

using System;
using System.Collections.Generic;
using System.Linq;
using IntersectUtilities.UtilsCommon.Enums;

namespace NTRExport.Routing
{
    /// <summary>
    /// Post-process twin systems: expand centerline members into Return (top) and Supply (bottom) lanes.
    /// Single/bonded members are copied as-is.
    /// </summary>
    internal static class TwinExpander
    {
        private const double NodeTol = 0.005; // meters, reuse topology tolerance scale

        private sealed class NodeInfo
        {
            public int Id { get; init; }
            public Point3d Pos { get; init; }
            public List<(RoutedMember member, bool isAEnd)> Incidents { get; } = new();
            public Vector3d Tangent { get; set; } = default;
            public Vector3d Normal { get; set; } = default;
        }

        public static RoutedGraph Expand(RoutedGraph g)
        {
            if (g == null) return new RoutedGraph();

            // Build node map from all endpoints of routed members
            var nodes = BuildNodeMap(g.Members);

            // Compute node tangents and normals
            ComputeNodeFrames(nodes);

            // Build expanded graph
            var result = new RoutedGraph();

            foreach (var m in g.Members)
            {
                // Non-twin members are copied as-is
                if (m.Type != PipeTypeEnum.Twin)
                {
                    result.Members.Add(m);
                    continue;
                }

                switch (m)
                {
                    case RoutedStraight s:
                        ExpandStraight(nodes, s, result);
                        break;
                    case RoutedBend b:
                        ExpandBend(nodes, b, result);
                        break;
                    default:
                        // For now: copy other routed types (reducers, tees) unchanged for twin
                        result.Members.Add(m);
                        break;
                }
            }

            return result;
        }

        #region Node map and frames

        private static List<NodeInfo> BuildNodeMap(IEnumerable<RoutedMember> members)
        {
            var nodes = new List<NodeInfo>();

            NodeInfo GetOrCreate(Point3d p)
            {
                foreach (var n in nodes)
                {
                    if (n.Pos.DistanceTo(p) <= NodeTol)
                        return n;
                }
                var created = new NodeInfo
                {
                    Id = nodes.Count,
                    Pos = p
                };
                nodes.Add(created);
                return created;
            }

            foreach (var m in members)
            {
                switch (m)
                {
                    case RoutedStraight s:
                        {
                            var nA = GetOrCreate(s.A);
                            var nB = GetOrCreate(s.B);
                            nA.Incidents.Add((m, true));
                            nB.Incidents.Add((m, false));
                            break;
                        }
                    case RoutedBend b:
                        {
                            var nA = GetOrCreate(b.A);
                            var nB = GetOrCreate(b.B);
                            nA.Incidents.Add((m, true));
                            nB.Incidents.Add((m, false));
                            break;
                        }
                    default:
                        break;
                }
            }

            return nodes;
        }

        private static void ComputeNodeFrames(List<NodeInfo> nodes)
        {
            foreach (var n in nodes)
            {
                var tangents = new List<Vector3d>();

                foreach (var (member, isAEnd) in n.Incidents)
                {
                    switch (member)
                    {
                        case RoutedStraight s:
                            {
                                var dir = (isAEnd ? (s.B - s.A) : (s.A - s.B));
                                if (dir.Length > 1e-9)
                                    tangents.Add(dir.GetNormal());
                                break;
                            }
                        case RoutedBend b:
                            {
                                Vector3d dir;
                                if (isAEnd)
                                {
                                    dir = b.T - b.A;
                                }
                                else
                                {
                                    dir = b.T - b.B;
                                }
                                if (dir.Length > 1e-9)
                                    tangents.Add(dir.GetNormal());
                                break;
                            }
                        default:
                            break;
                    }
                }

                if (tangents.Count == 0)
                {
                    n.Tangent = new Vector3d(1.0, 0.0, 0.0);
                }
                else if (tangents.Count == 1)
                {
                    n.Tangent = tangents[0];
                }
                else
                {
                    var sum = tangents.Aggregate(new Vector3d(0, 0, 0), (acc, t) => acc + t);
                    n.Tangent = sum.Length > 1e-9 ? sum.GetNormal() : tangents[0];
                }

                // Compute node normal (top direction for twin offset)
                var tvec = n.Tangent;
                var uXY = new Vector2d(tvec.X, tvec.Y);
                double lenXY = uXY.Length;
                Vector3d normal;

                if (lenXY < 1e-9)
                {
                    // Nearly vertical: fall back to world X-axis projection
                    uXY = new Vector2d(1.0, 0.0);
                    lenXY = 1.0;
                }

                var u = new Vector2d(uXY.X / lenXY, uXY.Y / lenXY);
                var alpha = Math.Atan2(tvec.Z, lenXY);
                // n = -sin(alpha) * [u,0] + cos(alpha) * Z
                normal = new Vector3d(
                    -Math.Sin(alpha) * u.X,
                    -Math.Sin(alpha) * u.Y,
                    Math.Cos(alpha));

                n.Normal = normal.Length > 1e-9 ? normal.GetNormal() : Vector3d.ZAxis;
            }
        }

        private static NodeInfo FindNode(List<NodeInfo> nodes, Point3d p)
        {
            // We assume BuildNodeMap used same tolerance, so exact references should exist.
            foreach (var n in nodes)
            {
                if (n.Pos.DistanceTo(p) <= NodeTol)
                    return n;
            }
            throw new InvalidOperationException("Endpoint node not found in TwinExpander.");
        }

        #endregion

        #region Expansion helpers

        private static void ExpandStraight(List<NodeInfo> nodes, RoutedStraight s, RoutedGraph result)
        {
            var nA = FindNode(nodes, s.A);
            var nB = FindNode(nodes, s.B);

            var d = GetTwinOffsetMagnitude(s);
            if (d <= 0.0)
            {
                // No meaningful twin offset: copy as-is
                result.Members.Add(s);
                return;
            }

            Point3d Offset(Point3d p, Vector3d n, double off) =>
                new Point3d(p.X + n.X * off, p.Y + n.Y * off, p.Z + n.Z * off);

            var aTop = Offset(s.A, nA.Normal, d);
            var bTop = Offset(s.B, nB.Normal, d);
            var aBot = Offset(s.A, nA.Normal, -d);
            var bBot = Offset(s.B, nB.Normal, -d);

            // Return (top)
            result.Members.Add(new RoutedStraight(s.Source, s.Emitter)
            {
                A = aTop,
                B = bTop,
                DN = s.DN,
                Material = s.Material,
                DnSuffix = s.DnSuffix,
                FlowRole = FlowRole.Return,
                Soil = s.Soil,
                LTG = s.LTG,
            });

            // Supply (bottom)
            result.Members.Add(new RoutedStraight(s.Source, s.Emitter)
            {
                A = aBot,
                B = bBot,
                DN = s.DN,
                Material = s.Material,
                DnSuffix = s.DnSuffix,
                FlowRole = FlowRole.Supply,
                Soil = s.Soil,
                LTG = s.LTG,
            });
        }

        private static void ExpandBend(List<NodeInfo> nodes, RoutedBend b, RoutedGraph result)
        {
            var nA = FindNode(nodes, b.A);
            var nB = FindNode(nodes, b.B);

            var d = GetTwinOffsetMagnitude(b);
            if (d <= 0.0)
            {
                result.Members.Add(b);
                return;
            }

            // Endpoint tangents for centerline
            var tA = (b.T - b.A);
            var tB = (b.T - b.B);
            if (tA.Length < 1e-9 || tB.Length < 1e-9)
            {
                result.Members.Add(b);
                return;
            }
            tA = tA.GetNormal();
            tB = tB.GetNormal();

            // Build local plane spanned by tA and tB
            var e1 = tA;
            var e2 = tB - tA.DotProduct(tB) * tA;
            if (e2.Length < 1e-9)
            {
                // Nearly straight: treat as straight with offset endpoints
                ExpandAsStraightLike(nodes, b, result, d);
                return;
            }
            e2 = e2.GetNormal();

            Point2d To2D(Point3d p)
            {
                var v = p - b.A;
                return new Point2d(v.DotProduct(e1), v.DotProduct(e2));
            }

            Point3d To3D(Point2d p)
            {
                return b.A + e1.MultiplyBy(p.X) + e2.MultiplyBy(p.Y);
            }

            // 2D coordinates of endpoints
            var a2 = To2D(b.A);
            var b2 = To2D(b.B);

            // Directions along incoming and outgoing straights in 2D from A and B
            var tA2 = new Vector2d(1.0, 0.0); // by construction: e1 is tA
            var tB3 = (b.B - b.T).GetNormal();
            var tB2 = new Vector2d(tB3.DotProduct(e1), tB3.DotProduct(e2)).GetNormal();

            // Helper: intersect two lines in 2D
            static bool IntersectLines(Point2d p1, Vector2d d1, Point2d p2, Vector2d d2, out Point2d inter)
            {
                inter = default;
                double cross(Vector2d u, Vector2d v) => u.X * v.Y - u.Y * v.X;
                var denom = cross(d1, d2);
                if (Math.Abs(denom) < 1e-12) return false;
                var w = p2 - p1;
                var t = cross(w, d2) / denom;
                inter = new Point2d(p1.X + d1.X * t, p1.Y + d1.Y * t);
                return true;
            }

            Point3d Offset(Point3d p, Vector3d n, double off) =>
                new Point3d(p.X + n.X * off, p.Y + n.Y * off, p.Z + n.Z * off);

            void EmitLane(double sign, FlowRole role)
            {
                var aLane = Offset(b.A, nA.Normal, sign * d);
                var bLane = Offset(b.B, nB.Normal, sign * d);

                // 2D positions of lane endpoints
                var aL2 = To2D(aLane);
                var bL2 = To2D(bLane);

                // Tangent directions for lane (same as centerline directions in 2D)
                var dA2 = tA2;
                var dB2 = tB2;

                if (!IntersectLines(aL2, dA2, bL2, dB2, out var pT2))
                {
                    // Fallback: midpoint in 2D between endpoints
                    pT2 = new Point2d(0.5 * (aL2.X + bL2.X), 0.5 * (aL2.Y + bL2.Y));
                }

                var tLane = To3D(pT2);

                result.Members.Add(new RoutedBend(b.Source, b.Emitter)
                {
                    A = aLane,
                    B = bLane,
                    T = tLane,
                    DN = b.DN,
                    Material = b.Material,
                    DnSuffix = b.DnSuffix,
                    FlowRole = role,
                    Norm = b.Norm,
                    ZOffsetMeters = b.ZOffsetMeters,
                    LTG = b.LTG,
                });
            }

            // Return (top) and Supply (bottom)
            EmitLane(+1.0, FlowRole.Return);
            EmitLane(-1.0, FlowRole.Supply);
        }

        private static void ExpandAsStraightLike(List<NodeInfo> nodes, RoutedBend b, RoutedGraph result, double d)
        {
            // Treat bend as straight between A and B and reuse straight expansion logic
            var fakeStraight = new RoutedStraight(b.Source, b.Emitter)
            {
                A = b.A,
                B = b.B,
                DN = b.DN,
                Material = b.Material,
                DnSuffix = b.DnSuffix,
                FlowRole = b.FlowRole,
                Soil = null,
                LTG = b.LTG,
            };

            ExpandStraight(nodes, fakeStraight, result);
        }

        private static double GetTwinOffsetMagnitude(RoutedMember m)
        {
            // Use ElementBase's twin offset helper via Emitter
            if (m.Emitter is ElementBase el)
            {
                var (zUp, zLow) = el.GetType()
                    .GetMethod("ComputeTwinOffsets", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.Invoke(el, new object[] { el.System, el.Type, m.DN }) is ValueTuple<double, double> tuple
                    ? tuple
                    : (0.0, 0.0);

                return Math.Abs(zUp);
            }
            return 0.0;
        }

        #endregion
    }
}


