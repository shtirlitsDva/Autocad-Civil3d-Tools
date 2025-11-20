using NTRExport.TopologyModel;
using Autodesk.AutoCAD.Geometry;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Linq;

using static IntersectUtilities.UtilsCommon.Utils;
using NTRExport.CadExtraction;
using NTRExport.Enums;

namespace NTRExport.Routing
{
    internal sealed class Router
    {
        private readonly Topology _topo;
        private readonly double _cushionReach;

        public Router(Topology topo, double cushionReach = 0.0)
        {
            _topo = topo;
            _cushionReach = cushionReach;
        }

        public RoutedGraph Route()
        {
            var g = new RoutedGraph();
            var ctx = new RouterContext(_topo, _cushionReach);

            // Traverse subnets from roots (entryZ = 0.0) and emit members inline
            SolveElevationsAndGeometry(g, ctx);
            VerticalElbowFix(g);
            RoutedTopologyBuilder.Build(g);

            return g;
        }

        private static void VerticalElbowFix(RoutedGraph g)
        {
            if (g.Members.Count == 0) return;

            double connectionTol = CadTolerance.Tol;
            const double maxSnapDist = 0.5; // meters

            static IEnumerable<Point3d> EnumerateEndpoints(RoutedMember member)
            {
                switch (member)
                {
                    case RoutedStraight rs:
                        yield return rs.A;
                        yield return rs.B;
                        break;
                    case RoutedBend rb:
                        yield return rb.A;
                        yield return rb.B;
                        break;
                    case RoutedReducer red:
                        yield return red.P1;
                        yield return red.P2;
                        break;
                    case RoutedValve valve:
                        yield return valve.P1;
                        yield return valve.P2;
                        break;
                    case RoutedTee tee:
                        yield return tee.Ph1;
                        yield return tee.Ph2;
                        yield return tee.Pa1;
                        yield return tee.Pa2;
                        break;
                }
            }

            bool HasConnection(RoutedBend elbow, Point3d pt)
            {
                foreach (var member in g.Members)
                {
                    if (ReferenceEquals(member, elbow)) continue;
                    if (member.FlowRole != elbow.FlowRole) continue;
                    foreach (var candidate in EnumerateEndpoints(member))
                    {
                        if (pt.DistanceTo(candidate) <= connectionTol)
                            return true;
                    }
                }
                return false;
            }

            (RoutedStraight? straight, bool isA, double dist) FindNearestStraight(FlowRole role, Point3d target)
            {
                RoutedStraight? best = null;
                bool bestIsA = true;
                double bestDist = double.MaxValue;
                foreach (var member in g.Members)
                {
                    if (member is not RoutedStraight rs) continue;
                    if (rs.FlowRole != role) continue;

                    var dA = rs.A.DistanceTo(target);
                    if (dA < bestDist)
                    {
                        best = rs;
                        bestIsA = true;
                        bestDist = dA;
                    }
                    var dB = rs.B.DistanceTo(target);
                    if (dB < bestDist)
                    {
                        best = rs;
                        bestIsA = false;
                        bestDist = dB;
                    }
                }
                return (best, bestIsA, bestDist);
            }

            void SnapEndpoint(RoutedBend elbow, Point3d pt)
            {
                if (HasConnection(elbow, pt)) return;
                var (straight, isA, dist) = FindNearestStraight(elbow.FlowRole, pt);
                if (straight == null) return;
                if (dist > maxSnapDist) return;

                if (isA)
                    straight.A = pt;
                else
                    straight.B = pt;
            }

            foreach (var member in g.Members)
            {
                if (member is not RoutedBend bend) continue;
                if (bend.Emitter is not ElbowVertical ev) continue;
                if (!ev.Variant.IsTwin) continue;

                SnapEndpoint(bend, bend.A);
                SnapEndpoint(bend, bend.B);
            }
        }

        private void SolveElevationsAndGeometry(RoutedGraph g, RouterContext ctx)
        {
            // Build port-level adjacency
            var nodeAdj = new Dictionary<TNode, List<(ElementBase el, TPort port)>>(new RefEq<TNode>());
            foreach (var el in _topo.Elements)
            {
                foreach (var p in el.Ports)
                {
                    if (!nodeAdj.TryGetValue(p.Node, out var list)) nodeAdj[p.Node] = list = new();
                    list.Add((el, p));
                }
            }

            var visitedPairs = new HashSet<(ElementBase, TPort)>(new ElPortPairEq());
            var visitedElements = new HashSet<ElementBase>(new RefEq<ElementBase>());

            while (true)
            {
                var (rootEl, rootPort) = PickRoot(_topo, nodeAdj, visitedElements);
                if (rootEl == null || rootPort == null) break;

                prdDbg($"Root found: {rootEl.DotLabelForTest()}");

                var stack = new Stack<(ElementBase el, TPort entryPort, double entryZ, double entrySlope)>();
                stack.Push((rootEl, rootPort, 0.0, 0.0));

                while (stack.Count > 0)
                {
                    var (el, entry, entryZ, entrySlope) = stack.Pop();
                    if (visitedElements.Contains(el)) continue;
                    if (!visitedPairs.Add((el, entry))) continue;
                    visitedElements.Add(el);

                    var exits = el.Route(g, _topo, ctx, entry, entryZ, entrySlope);

                    foreach (var (exitPort, exitZ, exitSlope) in exits)
                    {
                        if (!nodeAdj.TryGetValue(exitPort.Node, out var neighbors)) continue;
                        foreach (var (nel, nport) in neighbors)
                        {
                            if (ReferenceEquals(nel, el)) continue;
                            if (visitedElements.Contains(nel)) continue;
                            stack.Push((nel, nport, exitZ, exitSlope));
                        }
                    }
                }
            }
        }

        private static (ElementBase? el, TPort? port) PickRoot(Topology topo, Dictionary<TNode, List<(ElementBase el, TPort port)>> nodeAdj, HashSet<ElementBase> visitedElements)
        {
            ElementBase? bestEl = null;
            TPort? bestPort = null;
            int bestDn = -1;
            foreach (var el in topo.Elements)
            {
                if (visitedElements.Contains(el)) continue;
                // Leaf by port
                bool isLeafByPort = false;
                foreach (var port in el.Ports)
                {
                    if (!nodeAdj.TryGetValue(port.Node, out var list)) { isLeafByPort = true; break; }
                    if (DegreeExcluding(list, el) == 0) { isLeafByPort = true; break; }
                }
                if (!isLeafByPort) continue;
                int dn = 0;
                try { dn = el.DN; } catch { dn = 0; }
                if (dn > bestDn || (dn == bestDn && bestEl != null && string.CompareOrdinal(el.Source.ToString(), bestEl.Source.ToString()) < 0))
                {
                    bestDn = dn;
                    bestEl = el;
                    bestPort = el.Ports.FirstOrDefault(p =>
                    {
                        if (!nodeAdj.TryGetValue(p.Node, out var list)) return true;
                        return DegreeExcluding(list, el) == 0;
                    }) ?? el.Ports.FirstOrDefault();
                }
            }
            if (bestEl != null && bestPort != null) return (bestEl, bestPort);

            // Fallback: pick any unvisited element with largest DN
            bestEl = null; bestPort = null; bestDn = -1;
            foreach (var e in topo.Elements)
            {
                if (visitedElements.Contains(e)) continue;
                int dn = 0; try { dn = e.DN; } catch { dn = 0; }
                if (dn > bestDn || (dn == bestDn && bestEl != null && string.CompareOrdinal(e.Source.ToString(), bestEl.Source.ToString()) < 0))
                {
                    bestDn = dn;
                    bestEl = e;
                    bestPort = e.Ports.FirstOrDefault();
                }
            }
            return (bestEl, bestPort);
        }

        private static int DegreeExcluding(List<(ElementBase el, TPort port)> items, ElementBase exclude)
        {
            var set = new HashSet<ElementBase>(new RefEq<ElementBase>());
            foreach (var t in items)
            {
                if (ReferenceEquals(t.el, exclude)) continue;
                set.Add(t.el);
            }
            return set.Count;
        }

        private sealed class RefEq<T> : IEqualityComparer<T> where T : class
        {
            public bool Equals(T? x, T? y) => ReferenceEquals(x, y);
            public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
        }
        private sealed class ElPortPairEq : IEqualityComparer<(ElementBase, TPort)>
        {
            public bool Equals((ElementBase, TPort) x, (ElementBase, TPort) y) =>
                ReferenceEquals(x.Item1, y.Item1) && ReferenceEquals(x.Item2, y.Item2);
            public int GetHashCode((ElementBase, TPort) obj)
            {
                unchecked
                {
                    int h1 = RuntimeHelpers.GetHashCode(obj.Item1);
                    int h2 = RuntimeHelpers.GetHashCode(obj.Item2);
                    return (h1 * 397) ^ h2;
                }
            }
        }
    }

    internal sealed class RouterContext
    {
        public Topology Topology { get; }
        public double CushionReach { get; }
        public RouterContext(Topology topo, double cushionReach)
        {
            Topology = topo;
            CushionReach = cushionReach;
        }

        // Compatibility helper for legacy Route() code paths that still call ctx.GetZ.
        // Our current traversal emits inline and does not rely on this.
        public double GetZ(ElementBase element, Point3d a, Point3d b, double t)
        {
            var za = a.Z;
            var zb = b.Z;
            return za + t * (zb - za);
        }
    }
}