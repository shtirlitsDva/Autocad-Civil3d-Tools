using NTRExport.TopologyModel;
using Autodesk.AutoCAD.Geometry;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Linq;

using static IntersectUtilities.UtilsCommon.Utils;

namespace NTRExport.Routing
{
    internal sealed class Router
    {
        private readonly Topology _topo;

        public Router(Topology topo)
        {
            _topo = topo;
        }

        public RoutedGraph Route()
        {
            var g = new RoutedGraph();
            var ctx = new RouterContext(_topo);

            // Traverse subnets from roots (entryZ = 0.0) and emit members inline
            SolveElevationsAndGeometry(g, ctx);

            return g;
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
        public RouterContext(Topology topo)
        {
            Topology = topo;
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