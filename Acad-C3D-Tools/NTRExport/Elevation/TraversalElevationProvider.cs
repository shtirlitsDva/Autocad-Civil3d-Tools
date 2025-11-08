using Autodesk.AutoCAD.Geometry;

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

            var (rootEl, rootPort) = PickRoot(_topology, nodeAdj);
            if (rootEl == null || rootPort == null) return;

            var stack = new Stack<(ElementBase el, TPort entryPort, double entryZ)>();
            stack.Push((rootEl, rootPort, 0.0));

            var visited = new HashSet<(ElementBase, TPort)>(new ElPortPairEq());

            while (stack.Count > 0)
            {
                var (el, entry, entryZ) = stack.Pop();
                if (!visited.Add((el, entry))) continue;

                var solver = ResolveSolver(el);
                var exits = solver.Solve(el, entry, entryZ, _registry);

                // Continue from exits to connected neighbors
                foreach (var (exitPort, exitZ) in exits)
                {
                    if (!nodeAdj.TryGetValue(exitPort.Node, out var neighbors)) continue;
                    foreach (var (nel, nport) in neighbors)
                    {
                        if (ReferenceEquals(nel, el)) continue;
                        stack.Push((nel, nport, exitZ));
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

        private static (ElementBase? el, TPort? port) PickRoot(Topology topo, Dictionary<TNode, List<(ElementBase el, TPort port)>> nodeAdj)
        {
            ElementBase? bestEl = null;
            TPort? bestPort = null;
            int bestDn = -1;
            int bestScore = -1;
            foreach (var p in topo.Pipes)
            {
                bool aLeaf = nodeAdj.TryGetValue(p.A.Node, out var la) && la.Count <= 1;
                bool bLeaf = nodeAdj.TryGetValue(p.B.Node, out var lb) && lb.Count <= 1;
                if (!aLeaf && !bLeaf) continue;
                int score = p.Type == IntersectUtilities.UtilsCommon.Enums.PipeTypeEnum.Frem ? 1 : 0;
                int dn = p.DN;
                if (score > bestScore || (score == bestScore && dn > bestDn))
                {
                    bestScore = score;
                    bestDn = dn;
                    bestEl = p;
                    bestPort = aLeaf ? p.A : p.B;
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
                    int h2 = System.Runtime.CompilerServices.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj.Item2);
                    return (h1 * 397) ^ h2;
                }
            }
        }
    }
}



