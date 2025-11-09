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
        private readonly IElementElevationSolver _default = new DefaultElementElevationSolver();

        public TraversalElevationProvider(Topology topology)
        {
            _topology = topology;
            RegisterSolvers();
            SolveFromRoot();
        }

        public double GetZ(ElementBase element, Point3d a, Point3d b, double t)
        {
            if (_registry.TryGetEndpointZ(element, out var endpoints))
            {
                var ports = element.Ports;
                if (ports.Count >= 2 && endpoints.TryGetValue(ports[0], out var z0) && endpoints.TryGetValue(ports[1], out var z1))
                {
                    return z0 + t * (z1 - z0);
                }
            }
            // Fallback to geometry Z
            var za = a.Z;
            var zb = b.Z;
            return za + t * (zb - za);
        }

        private void RegisterSolvers()
        {
            // Register element-specific solvers here (stubs for now).
            // Example:
            // _solvers[typeof(AfgreningMedSpring)] = new AfgreningMedSpringSolver();
            // _solvers[typeof(PreinsulatedElbowAbove45deg)] = new VerticalElbowSolver();
            // _solvers[typeof(PreinsulatedElbowAtOrBelow45deg)] = new VerticalElbowSolver();
            // Plane elbows would also have a solver interpreting roll Near/Far.
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

            // Handle disjoint networks: iterate until all elements are visited
            var visitedPairs = new HashSet<(ElementBase, TPort)>(new ElPortPairEq());
            var visitedElements = new HashSet<ElementBase>(new RefEq<ElementBase>());

            while (true)
            {
                prdDbg($"[TRAV] Root selection pass. visitedElements={visitedElements.Count}");
                var (rootEl, rootPort) = PickRoot(_topology, nodeAdj, visitedElements);
                if (rootEl == null || rootPort == null) break;
                prdDbg($"[TRAV] Root chosen: {EId(rootEl)} at node={NodeId(rootPort.Node)}. entryZ=0.000");

                // Start this component at entryZ = 0.0
                var stack = new Stack<(ElementBase el, TPort entryPort, double entryZ)>();
                stack.Push((rootEl, rootPort, 0.0));

                while (stack.Count > 0)
                {
                    var (el, entry, entryZ) = stack.Pop();
                    if (!visitedPairs.Add((el, entry))) continue;
                    visitedElements.Add(el);
                    prdDbg($"[TRAV] Visit element: {EId(el)} via node={NodeId(entry.Node)} entryZ={entryZ:0.###}");

                    var solver = ResolveSolver(el);
                    var exits = solver.Solve(el, entry, entryZ, _registry);
                    prdDbg($"[TRAV] Solver exits: count={exits.Count}");

                    // Continue from exits to connected neighbors
                    foreach (var (exitPort, exitZ) in exits)
                    {
                        prdDbg($"[TRAV]  -> exit node={NodeId(exitPort.Node)} propagateZ={exitZ:0.###}");
                        if (!nodeAdj.TryGetValue(exitPort.Node, out var neighbors)) continue;
                        foreach (var (nel, nport) in neighbors)
                        {
                            if (ReferenceEquals(nel, el)) continue;
                            prdDbg($"[TRAV]     neighbor: {EId(nel)} via node={NodeId(nport.Node)}");
                            stack.Push((nel, nport, exitZ));
                        }
                    }
                }
            }
        }

        private IElementElevationSolver ResolveSolver(ElementBase el)
        {
            var t = el.GetType();
            if (_solvers.TryGetValue(t, out var s)) return s;
            return _default;
        }

        private static (ElementBase? el, TPort? port) PickRoot(Topology topo, Dictionary<TNode, List<(ElementBase el, TPort port)>> nodeAdj, HashSet<ElementBase> visitedElements)
        {
            prdDbg("[ROOT] Begin selection (prefer supply leaf with largest DN among unvisited).");
            ElementBase? bestEl = null;
            TPort? bestPort = null;
            int bestDn = -1;
            int bestScore = -1;
            foreach (var p in topo.Pipes)
            {
                if (visitedElements.Contains(p)) continue;
                bool aLeaf = nodeAdj.TryGetValue(p.A.Node, out var la) && la.Count <= 1;
                bool bLeaf = nodeAdj.TryGetValue(p.B.Node, out var lb) && lb.Count <= 1;
                int score = p.Type == IntersectUtilities.UtilsCommon.Enums.PipeTypeEnum.Frem ? 1 : 0; // prefer supply
                int dn = p.DN; // prefer larger DN
                prdDbg($"[ROOT] Consider {EId(p)}: aLeaf={aLeaf} bLeaf={bLeaf} score={score} dn={dn} currentBestScore={bestScore} bestDn={bestDn}");
                if ((aLeaf || bLeaf) && (score > bestScore || (score == bestScore && dn > bestDn)))
                {
                    bestScore = score;
                    bestDn = dn;
                    bestEl = p;
                    bestPort = aLeaf ? p.A : p.B;
                    prdDbg($"[ROOT]  -> update best to {EId(p)} at node={NodeId(bestPort.Node)}");
                }
            }
            if (bestEl != null && bestPort != null) return (bestEl, bestPort);

            // Fallback: pick any unvisited pipe with largest DN (choose A port)
            bestEl = null;
            bestPort = null;
            bestDn = -1;
            prdDbg("[ROOT] Fallback: no supply leaf found, choose largest DN unvisited pipe.");
            foreach (var p in topo.Pipes)
            {
                if (visitedElements.Contains(p)) continue;
                if (p.DN > bestDn)
                {
                    bestDn = p.DN;
                    bestEl = p;
                    bestPort = p.A;
                    prdDbg($"[ROOT]  -> fallback candidate {EId(p)} at node={NodeId(bestPort.Node)}");
                }
            }
            return (bestEl, bestPort);
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
                    int h1 = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj.Item1);
                    int h2 = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj.Item2);
                    return (h1 * 397) ^ h2;
                }
            }
        }

        private static string EId(ElementBase el)
        {
            int dn = 0;
            try { dn = el.DN; } catch { dn = 0; }
            return $"{el.Source} / {el.GetType().Name} / DN={dn}";
        }
        private static string NodeId(TNode node)
        {
            var name = string.IsNullOrWhiteSpace(node.Name) ? null : node.Name;
            return name ?? $"@{RuntimeHelpers.GetHashCode(node)}";
        }
    }
}





