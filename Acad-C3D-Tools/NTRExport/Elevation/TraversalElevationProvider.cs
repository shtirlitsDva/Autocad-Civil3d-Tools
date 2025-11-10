using Autodesk.AutoCAD.Geometry;
using System.Runtime.CompilerServices;
using static IntersectUtilities.UtilsCommon.Utils;

using NTRExport.TopologyModel;

namespace NTRExport.Elevation
{
    internal sealed class TraversalElevationProvider : IElevationProvider
    {
        private readonly Topology _topology;
        private readonly ElevationRegistry _registry = new();
        private readonly Dictionary<Type, IElementElevationSolver> _solvers = new();

        public TraversalElevationProvider(Topology topology)
        {
            _topology = topology;
            SolveFromRoot();
        }

        private static readonly IReadOnlyDictionary<TPort, double> EmptyEndpointMap =
            new Dictionary<TPort, double>(new RefEq<TPort>());

        public double GetZ(ElementBase element, Point3d a, Point3d b, double t)
        {
            if (_registry.TryGetEndpointZ(element, out var endpointsTmp))
            {
                IReadOnlyDictionary<TPort, double> endpoints = endpointsTmp ?? EmptyEndpointMap;
                var ports = element.Ports;

                if (ports.Count >= 2)
                {
                    var p0 = ports[0];
                    var p1 = ports[1];
                    if (p0 != null && p1 != null &&
                        endpoints.TryGetValue(p0, out var z0) &&
                        endpoints.TryGetValue(p1, out var z1))
                    {
                        return z0 + t * (z1 - z0);
                    }
                }
            }
            // Fallback to geometry Z
            var za = a.Z;
            var zb = b.Z;
            return za + t * (zb - za);
        }

        private void SolveFromRoot()
        {
            // Build adjacency by node to find neighbors
            var nodeAdj = new Dictionary<TNode, List<(ElementBase el, TPort port)>>(new RefEq<TNode>());
            foreach (var el in _topology.Elements)
            {
                foreach (var p in el.Ports)
                {
                    if (!nodeAdj.TryGetValue(p.Node, out var list)) nodeAdj[p.Node] = list = new();
                    list.Add((el, p));
                }
            }
            prdDbg($"[TRAV] Adjacency built: nodes={nodeAdj.Count}, elements={_topology.Elements.Count()}");

            // Handle disjoint subnets: iterate until all elements are visited
            var visitedPairs = new HashSet<(ElementBase, TPort)>(new ElPortPairEq());
            var visitedElements = new HashSet<ElementBase>(new RefEq<ElementBase>());

            while (true)
            {
                prdDbg($"[TRAV] Root selection pass. visitedElements={visitedElements.Count}");
                var (rootEl, rootPort) = PickRoot(_topology, nodeAdj, visitedElements);
                // Break when no more unvisited subnets remain (all elements processed)
                if (rootEl == null || rootPort == null) break;
                prdDbg($"[TRAV] Root chosen: {EId(rootEl)} at node={NodeId(rootPort.Node)}. entryZ=0.000");

                // Start this subnet at entryZ = 0.0
                var stack = new Stack<(ElementBase el, TPort entryPort, double entryZ)>();
                stack.Push((rootEl, rootPort, 0.0));

                while (stack.Count > 0)
                {
                    var (el, entry, entryZ) = stack.Pop();
                    if (visitedElements.Contains(el))
                    {
                        prdDbg($"[TRAV] Skip element (already visited): {EId(el)}");
                        continue;
                    }
                    if (!visitedPairs.Add((el, entry))) continue;
                    visitedElements.Add(el);
                    prdDbg($"[TRAV] Visit element: {EId(el)} via node={NodeId(entry.Node)} entryZ={entryZ:0.###}");

                    var ctx = new SolverContext(_topology, _registry);
                    var exits = el.SolveElevation(entry, entryZ, ctx);
                    prdDbg($"[TRAV] Solver exits: count={exits.Count}");

                    // Continue from exits to connected neighbors
                    foreach (var (exitPort, exitZ) in exits)
                    {
                        prdDbg($"[TRAV]  -> exit node={NodeId(exitPort.Node)} propagateZ={exitZ:0.###}");
                        if (!nodeAdj.TryGetValue(exitPort.Node, out var neighbors)) continue;
                        foreach (var (nel, nport) in neighbors)
                        {
                            if (ReferenceEquals(nel, el)) continue;
                            if (visitedElements.Contains(nel))
                            {
                                prdDbg($"[TRAV]     neighbor already visited, skip: {EId(nel)}");
                                continue;
                            }
                            prdDbg($"[TRAV]     neighbor: {EId(nel)} via node={NodeId(nport.Node)}");
                            stack.Push((nel, nport, exitZ));
                        }
                    }
                }
            }
        }

        private static (ElementBase? el, TPort? port) PickRoot(Topology topo, Dictionary<TNode, List<(ElementBase el, TPort port)>> nodeAdj, HashSet<ElementBase> visitedElements)
        {
            prdDbg("[ROOT] Begin selection (LEAFS only; pick largest DN; tie-break by id).");
            ElementBase? bestEl = null;
            TPort? bestPort = null;
            int bestDn = -1;
            foreach (var el in topo.Elements)
            {
                if (visitedElements.Contains(el)) continue;
                int dn = 0;
                try { dn = el.DN; } catch { dn = 0; }
                // Leaf by port: at least one port has no neighbor (excluding the element itself)
                bool isLeafByPort = false;
                foreach (var port in el.Ports)
                {
                    if (!nodeAdj.TryGetValue(port.Node, out var list)) { isLeafByPort = true; break; }
                    if (DegreeExcluding(list, el) == 0) { isLeafByPort = true; break; }
                }
                if (!isLeafByPort) continue;
                prdDbg($"[ROOT] Consider {EId(el)}: leafByPort=True dn={dn} currentBestDn={bestDn}");
                if (dn > bestDn || (dn == bestDn && bestEl != null && string.CompareOrdinal(el.Source.ToString(), bestEl.Source.ToString()) < 0))
                {
                    bestDn = dn;
                    bestEl = el;
                    // Prefer a truly unconnected port as entry
                    bestPort = el.Ports.FirstOrDefault(p =>
                    {
                        if (!nodeAdj.TryGetValue(p.Node, out var list)) return true;
                        return DegreeExcluding(list, el) == 0;
                    }) ?? el.Ports.FirstOrDefault();
                    if (bestPort != null)
                        prdDbg($"[ROOT]  -> update best to {EId(el)} at node={NodeId(bestPort.Node)}");
                }
            }
            // Log how many were filtered out (optional aggregate log for transparency)
            int filteredOut = topo.Elements.Count(e =>
            {
                if (visitedElements.Contains(e)) return false;
                foreach (var port in e.Ports)
                {
                    if (!nodeAdj.TryGetValue(port.Node, out var list)) return true;
                    if (DegreeExcluding(list, e) == 0) return true;
                }
                return false;
            });
            var considered = topo.Elements.Count(e => !visitedElements.Contains(e)) - filteredOut;
            prdDbg($"[ROOT] {filteredOut} component(s) with leafByPort=False were not considered. consideredLeafs={considered}");
            if (bestEl != null && bestPort != null) return (bestEl, bestPort);

            // Fallback: pick any unvisited element with largest DN (choose first port)
            bestEl = null;
            bestPort = null;
            bestDn = -1;
            prdDbg("[ROOT] Fallback: no leaf found, choose largest DN unvisited element.");
            foreach (var e in topo.Elements)
            {
                if (visitedElements.Contains(e)) continue;
                int dn = 0;
                try { dn = e.DN; } catch { dn = 0; }
                if (dn > bestDn || (dn == bestDn && bestEl != null && string.CompareOrdinal(e.Source.ToString(), bestEl.Source.ToString()) < 0))
                {
                    bestDn = dn;
                    bestEl = e;
                    bestPort = e.Ports.FirstOrDefault();
                    if (bestPort != null)
                        prdDbg($"[ROOT]  -> fallback candidate {EId(e)} at node={NodeId(bestPort.Node)}");
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
            public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
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

        private static string EId(ElementBase el)
        {            
            return $"{el.Source} / {el.GetType().Name} / {el.DnLabel()}";
        }
        private static string NodeId(TNode node)
        {
            var name = string.IsNullOrWhiteSpace(node.Name) ? null : node.Name;
            return name ?? $"@{RuntimeHelpers.GetHashCode(node)}";
        }
    }
}





