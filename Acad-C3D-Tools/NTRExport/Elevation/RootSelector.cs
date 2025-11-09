using NTRExport.TopologyModel;
using static IntersectUtilities.UtilsCommon.Utils;
using System.Runtime.CompilerServices;

namespace NTRExport.Elevation
{
    internal static class RootSelector
    {
        public static void DebugPickRoots(Topology topo)
        {
            // Build port-level adjacency (node -> (element, port))
            var nodeAdj = new Dictionary<TNode, List<(ElementBase el, TPort port)>>(new RefEq<TNode>());
            foreach (var el in topo.Elements)
            {
                foreach (var p in el.Ports)
                {
                    if (!nodeAdj.TryGetValue(p.Node, out var list)) nodeAdj[p.Node] = list = new();
                    list.Add((el, p));
                }
            }

            prdDbg($"[ROOTDBG] Adjacency built: nodes={nodeAdj.Count}, elements={topo.Elements.Count()}");

            // Build element adjacency (undirected) by shared nodes
            var elAdj = new Dictionary<ElementBase, HashSet<ElementBase>>(new RefEq<ElementBase>());
            foreach (var kv in nodeAdj)
            {
                var items = kv.Value;
                for (int i = 0; i < items.Count; i++)
                {
                    for (int j = i + 1; j < items.Count; j++)
                    {
                        var a = items[i].el;
                        var b = items[j].el;
                        if (ReferenceEquals(a, b)) continue;
                        if (!elAdj.TryGetValue(a, out var setA)) elAdj[a] = setA = new(new RefEq<ElementBase>());
                        if (!elAdj.TryGetValue(b, out var setB)) elAdj[b] = setB = new(new RefEq<ElementBase>());
                        setA.Add(b);
                        setB.Add(a);
                    }
                }
            }

            var visitedElements = new HashSet<ElementBase>(new RefEq<ElementBase>());
            int compIdx = 0;
            while (true)
            {
                prdDbg($"[ROOTDBG] Component {compIdx}: root selection pass. visitedElements={visitedElements.Count}");
                var (rootEl, rootPort) = PickRoot(topo, nodeAdj, visitedElements);
                if (rootEl == null || rootPort == null)
                {
                    prdDbg("[ROOTDBG] No more roots found (all elements covered).");
                    break;
                }
                prdDbg($"[ROOTDBG] Component {compIdx}: Root chosen: {EId(rootEl)} at node={NodeId(rootPort.Node)}");

                // Mark the entire connected component visited for the next passes
                var compCount = 0;
                var q = new Queue<ElementBase>();
                if (visitedElements.Add(rootEl)) { compCount++; q.Enqueue(rootEl); }
                while (q.Count > 0)
                {
                    var cur = q.Dequeue();
                    if (!elAdj.TryGetValue(cur, out var nbrs)) continue;
                    foreach (var n in nbrs)
                    {
                        if (visitedElements.Add(n)) { compCount++; q.Enqueue(n); }
                    }
                }
                prdDbg($"[ROOTDBG] Component {compIdx}: size={compCount} (elements).");
                compIdx++;
            }
        }

        private static (ElementBase? el, TPort? port) PickRoot(Topology topo,
            Dictionary<TNode, List<(ElementBase el, TPort port)>> nodeAdj,
            HashSet<ElementBase> visitedElements)
        {
            prdDbg("[ROOTDBG] Begin selection (prefer supply leaf with largest DN among unvisited).");
            ElementBase? bestEl = null;
            TPort? bestPort = null;
            int bestDn = -1;
            int bestScore = -1;

            foreach (var el in topo.Elements)
            {
                if (visitedElements.Contains(el)) continue;
                int elemDeg = ElementDegree(el, nodeAdj);
                bool isLeaf = elemDeg == 1;
                int score = el.Type == IntersectUtilities.UtilsCommon.Enums.PipeTypeEnum.Frem ? 1 : 0; // prefer supply when known
                int dn = 0;
                try { dn = el.DN; } catch { dn = 0; }
                prdDbg($"[ROOTDBG] Consider {EId(el)}: elemDeg={elemDeg} score={score} dn={dn} currentBestScore={bestScore} bestDn={bestDn}");
                if (isLeaf && (score > bestScore || (score == bestScore && dn > bestDn)))
                {
                    bestScore = score;
                    bestDn = dn;
                    bestEl = el;
                    bestPort = ChooseLeafPort(el, nodeAdj) ?? el.Ports.FirstOrDefault();
                    if (bestPort != null)
                        prdDbg($"[ROOTDBG]  -> update best to {EId(el)} at node={NodeId(bestPort.Node)}");
                }
            }
            if (bestEl != null && bestPort != null) return (bestEl, bestPort);

            // Fallback: pick any unvisited pipe with largest DN (choose A port)
            bestEl = null;
            bestPort = null;
            bestDn = -1;
            prdDbg("[ROOTDBG] Fallback: no supply leaf found, choose largest DN unvisited pipe.");
            foreach (var p in topo.Pipes)
            {
                if (visitedElements.Contains(p)) continue;
                if (p.DN > bestDn)
                {
                    bestDn = p.DN;
                    bestEl = p;
                    bestPort = p.A;
                    prdDbg($"[ROOTDBG]  -> fallback candidate {EId(p)} at node={NodeId(bestPort.Node)}");
                }
            }
            return (bestEl, bestPort);
        }

        private static int DegreeExcluding(List<(ElementBase el, TPort port)> items, ElementBase exclude)
        {
            // Count distinct elements at this node excluding the candidate itself
            var set = new HashSet<ElementBase>(new RefEq<ElementBase>());
            foreach (var t in items)
            {
                if (ReferenceEquals(t.el, exclude)) continue;
                set.Add(t.el);
            }
            return set.Count;
        }

        private static int ElementDegree(ElementBase el, Dictionary<TNode, List<(ElementBase el, TPort port)>> nodeAdj)
        {
            var set = new HashSet<ElementBase>(new RefEq<ElementBase>());
            foreach (var port in el.Ports)
            {
                if (!nodeAdj.TryGetValue(port.Node, out var list)) continue;
                foreach (var t in list)
                {
                    if (ReferenceEquals(t.el, el)) continue;
                    set.Add(t.el);
                }
            }
            return set.Count;
        }

        private static TPort? ChooseLeafPort(ElementBase el, Dictionary<TNode, List<(ElementBase el, TPort port)>> nodeAdj)
        {
            foreach (var port in el.Ports)
            {
                if (!nodeAdj.TryGetValue(port.Node, out var list)) continue;
                foreach (var t in list)
                {
                    if (!ReferenceEquals(t.el, el))
                        return port;
                }
            }
            return el.Ports.FirstOrDefault();
        }

        private sealed class RefEq<T> : IEqualityComparer<T> where T : class
        {
            public bool Equals(T? x, T? y) => ReferenceEquals(x, y);
            public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
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


