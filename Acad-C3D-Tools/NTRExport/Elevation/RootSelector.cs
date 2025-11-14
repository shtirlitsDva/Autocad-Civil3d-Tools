using NTRExport.TopologyModel;
using static IntersectUtilities.UtilsCommon.Utils;
using System.Runtime.CompilerServices;

namespace NTRExport.Elevation
{
    internal static class RootSelector
    {
        private const bool Verbose = false;

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

            if (Verbose) prdDbg($"[ROOTDBG] Adjacency built: nodes={nodeAdj.Count}, elements={topo.Elements.Count()}");

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
            int subnetIdx = 0;
            int totalLeafConsidered = 0;
            int totalFilteredOut = 0;
            while (true)
            {
                if (Verbose) prdDbg($"[ROOTDBG] Subnet {subnetIdx}: root selection pass. visitedElements={visitedElements.Count}");

                // Per-pass stats (unvisited only)
                int passLeafs = 0, passFiltered = 0;
                foreach (var el in topo.Elements)
                {
                    if (visitedElements.Contains(el)) continue;
                    bool isLeafByPort = false;
                    foreach (var port in el.Ports)
                    {
                        if (!nodeAdj.TryGetValue(port.Node, out var list)) { isLeafByPort = true; break; }
                        if (DegreeExcluding(list, el) == 0) { isLeafByPort = true; break; }
                    }
                    if (isLeafByPort) passLeafs++; else passFiltered++;
                }
                totalLeafConsidered += passLeafs;
                totalFilteredOut += passFiltered;

                var (rootEl, rootPort) = PickRoot(topo, nodeAdj, visitedElements);
                if (rootEl == null || rootPort == null)
                {
                    if (Verbose) prdDbg("[ROOTDBG] No more roots found (all elements covered).");
                    break;
                }
                if (Verbose) prdDbg($"[ROOTDBG] Subnet {subnetIdx}: Root chosen: {EId(rootEl)} at node={NodeId(rootPort.Node)}");

                // Mark the entire connected subnet visited for the next passes
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
                if (Verbose) prdDbg($"[ROOTDBG] Subnet {subnetIdx}: size={compCount} (elements).");
                subnetIdx++;
            }

            // Summary (keep)
            prdDbg($"[ROOTFIND] Summary: subnets={subnetIdx}, elements={topo.Elements.Count()}, leafsConsidered={totalLeafConsidered}, nonLeafFiltered={totalFilteredOut}");
        }

        private static (ElementBase? el, TPort? port) PickRoot(Topology topo,
            Dictionary<TNode, List<(ElementBase el, TPort port)>> nodeAdj,
            HashSet<ElementBase> visitedElements)
        {
            if (Verbose) prdDbg("[ROOTDBG] Begin selection (LEAFS only; pick largest DN; tie-break by id).");
            ElementBase? bestEl = null;
            TPort? bestPort = null;
            int bestDn = -1;

            int filteredOut = 0;
            foreach (var el in topo.Elements)
            {
                if (visitedElements.Contains(el)) continue;
                // Leaf by port: at least one port has no neighbor (excluding the element itself)
                bool isLeafByPort = false;
                foreach (var port in el.Ports)
                {
                    if (!nodeAdj.TryGetValue(port.Node, out var list)) { isLeafByPort = true; break; }
                    if (DegreeExcluding(list, el) == 0) { isLeafByPort = true; break; }
                }
                int dn = 0;
                try { dn = el.DN; } catch { dn = 0; }
                if (!isLeafByPort)
                {
                    filteredOut++;
                    continue;
                }
                if (Verbose) prdDbg($"[ROOTDBG] Consider {EId(el)}: leafByPort=True dn={dn} currentBestDn={bestDn}");
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
                    if (Verbose && bestPort != null)
                        prdDbg($"[ROOTDBG]  -> update best to {EId(el)} at node={NodeId(bestPort.Node)}");
                }
            }
            if (Verbose) prdDbg($"[ROOTDBG] {filteredOut} element(s) with leafByPort=False were not considered.");
            if (bestEl != null && bestPort != null) return (bestEl, bestPort);

            // Fallback: pick any unvisited element with largest DN (choose first port)
            bestEl = null;
            bestPort = null;
            bestDn = -1;
            if (Verbose) prdDbg("[ROOTDBG] Fallback: no leaf found, choose largest DN unvisited element.");
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
                    if (Verbose && bestPort != null)
                        prdDbg($"[ROOTDBG]  -> fallback candidate {EId(e)} at node={NodeId(bestPort.Node)}");
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


