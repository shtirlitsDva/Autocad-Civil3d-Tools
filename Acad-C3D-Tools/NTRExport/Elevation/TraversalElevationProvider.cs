using Autodesk.AutoCAD.Geometry;

using NTRExport.TopologyModel;

namespace NTRExport.Elevation
{
    internal sealed class TraversalElevationProvider : IElevationProvider
    {
        private readonly Topology _topology;
        // Map element -> node-pos (by reference) -> Z
        private readonly Dictionary<ElementBase, Dictionary<TNode, double>> _elementNodeZ =
            new(new ReferenceEqualityComparer<ElementBase>());

        public TraversalElevationProvider(Topology topology)
        {
            _topology = topology;
            SolveFromRoot();
        }

        public double GetZ(ElementBase element, Point3d a, Point3d b, double t)
        {
            // Try to resolve Z at endpoints via precomputed map, then interpolate
            if (_elementNodeZ.TryGetValue(element, out var nodeZ))
            {
                var (na, nb) = GetEndpoints(element);
                if (na != null && nb != null && nodeZ.TryGetValue(na, out var za) && nodeZ.TryGetValue(nb, out var zb))
                {
                    return za + t * (zb - za);
                }
            }
            // Fallback: keep plan Z
            var z0 = a.Z;
            var z1 = b.Z;
            return z0 + t * (z1 - z0);
        }

        private void SolveFromRoot()
        {
            // Build adjacency: node -> (element, node)
            var nodeAdj = new Dictionary<TNode, List<(ElementBase el, TNode node)>>(new ReferenceEqualityComparer<TNode>());
            foreach (var el in _topology.Elements)
            {
                foreach (var p in el.Ports)
                {
                    if (!nodeAdj.TryGetValue(p.Node, out var list)) nodeAdj[p.Node] = list = new();
                    list.Add((el, p.Node));
                }
            }

            // Pick root: degree-1 supply pipe with largest DN (heuristic)
            var root = PickRoot(_topology, nodeAdj, out ElementBase? rootElement, out TNode? rootNode);
            if (root == null || rootElement == null || rootNode == null)
                return;

            var stack = new Stack<(ElementBase el, TNode entryNode, double entryZ)>();
            stack.Push((rootElement, rootNode, 0.0));

            var visited = new HashSet<(ElementBase el, TNode entry)>(new ElementNodePairComparer());

            while (stack.Count > 0)
            {
                var (el, entry, entryZ) = stack.Pop();
                if (!visited.Add((el, entry))) continue;

                var exits = SolveElement(el, entry, entryZ);
                foreach (var (exitNode, exitZ) in exits)
                {
                    if (!nodeAdj.TryGetValue(exitNode, out var neighbors)) continue;
                    foreach (var (nel, nnode) in neighbors)
                    {
                        if (ReferenceEquals(nel, el)) continue;
                        stack.Push((nel, exitNode, exitZ));
                    }
                }
            }
        }

        private List<(TNode exitNode, double exitZ)> SolveElement(ElementBase el, TNode entryNode, double entryZ)
        {
            // Default: pass-through elevation to all ports; passives inherit Z
            if (!_elementNodeZ.TryGetValue(el, out var nodeZ))
            {
                nodeZ = new Dictionary<TNode, double>(new ReferenceEqualityComparer<TNode>());
                _elementNodeZ[el] = nodeZ;
            }

            nodeZ[entryNode] = entryZ;
            var exits = new List<(TNode exitNode, double exitZ)>();

            // Push the same Z to all connected ports by default
            foreach (var p in el.Ports)
            {
                if (ReferenceEquals(p.Node, entryNode)) continue;
                nodeZ[p.Node] = entryZ;
                exits.Add((p.Node, entryZ));
            }
            return exits;
        }

        private static (ElementBase? el, TNode? node) PickRoot(Topology topo,
            Dictionary<TNode, List<(ElementBase el, TNode node)>> nodeAdj,
            out ElementBase? rootElement, out TNode? rootNode)
        {
            rootElement = null;
            rootNode = null;

            // Candidates: pipes with at least one leaf end (degree 1), prefer supply, largest DN
            ElementBase? bestEl = null;
            TNode? bestNode = null;
            int bestDn = -1;
            int bestScore = -1; // prefer supply

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
                    bestNode = aLeaf ? p.A.Node : p.B.Node;
                }
            }

            if (bestEl != null && bestNode != null)
            {
                rootElement = bestEl;
                rootNode = bestNode;
                return (bestEl, bestNode);
            }

            return (null, null);
        }

        private static (TNode? a, TNode? b) GetEndpoints(ElementBase el)
        {
            if (el is TPipe p) return (p.A.Node, p.B.Node);
            // For fittings, return first two ports if present
            var ends = el.Ports.Take(2).Select(pp => pp.Node).ToArray();
            if (ends.Length == 2) return (ends[0], ends[1]);
            return (null, null);
        }

        private sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T>
            where T : class
        {
            public bool Equals(T? x, T? y) => ReferenceEquals(x, y);
            public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }

        private sealed class ElementNodePairComparer : IEqualityComparer<(ElementBase el, TNode entry)>
        {
            public bool Equals((ElementBase el, TNode entry) x, (ElementBase el, TNode entry) y) =>
                ReferenceEquals(x.el, y.el) && ReferenceEquals(x.entry, y.entry);
            public int GetHashCode((ElementBase el, TNode entry) obj)
            {
                unchecked
                {
                    int h1 = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj.el);
                    int h2 = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj.entry);
                    return (h1 * 397) ^ h2;
                }
            }
        }
    }
}


