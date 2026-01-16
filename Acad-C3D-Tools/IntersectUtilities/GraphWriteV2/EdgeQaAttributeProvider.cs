using Autodesk.AutoCAD.DatabaseServices;

using IntersectUtilities.DynamicBlocks;
using IntersectUtilities;
using IntersectUtilities.UtilsCommon.Enums;
using IntersectUtilities.UtilsCommon.Graphs;
using IntersectUtilities.UtilsCommon.DataManager.CsvData;

using System;
using System.Collections.Generic;
using System.Linq;

namespace IntersectUtilities.GraphWriteV2
{
    internal static class EdgeQaAttributeProvider
    {
        /// <summary>
        /// Topology-aware QA over a tree-shaped graph. Computes edge attributes for DOT export
        /// by propagating DN assignments across components (reducers/tees) and validating
        /// PipeType consistency per connection end.
        /// </summary>
        /// <remarks>
        /// Returns a map of directed edge (parentHandle -> childHandle) to a DOT edge attribute string
        /// like: [ label="DN, T/E", color="red" ]. Empty/absent implies no issues.
        /// </remarks>
        public static Dictionary<(string fromHandle, string toHandle), string> BuildEdgeAttributes(
            Graph<GraphEntity> graph,
            FjvDynamicComponents fjvTable)
        {
            var result = new Dictionary<(string fromHandle, string toHandle), string>();
            if (graph == null || graph.Root == null) return result;

            // Index nodes by handle for fast lookup
            var handleToNode = new Dictionary<string, Node<GraphEntity>>(StringComparer.Ordinal);
            var allNodes = new List<Node<GraphEntity>>();
            var q = new Queue<Node<GraphEntity>>();
            q.Enqueue(graph.Root);
            while (q.Count > 0)
            {
                var n = q.Dequeue();
                var h = n.Value.OwnerHandle.ToString();
                if (!handleToNode.ContainsKey(h))
                {
                    handleToNode[h] = n;
                    allNodes.Add(n);
                    foreach (var c in n.Children) q.Enqueue(c);
                }
            }

            // Build undirected edge states for DN propagation
            var edgeStates = new Dictionary<(string a, string b), EdgeState>(TuplePairComparer.Instance);
            foreach (var node in allNodes)
            {
                var h = node.Value.OwnerHandle.ToString();
                foreach (var con in node.Value.Cons)
                {
                    var nh = con.ConHandle.ToString();
                    if (!handleToNode.ContainsKey(nh)) continue; // outside of this component
                    var key = SortPair(h, nh);
                    if (!edgeStates.ContainsKey(key)) edgeStates[key] = new EdgeState(key.a, key.b);
                }
            }

            // Seed DN from ROOT: for one-size root use that DN; for two-size root use the lesser DN
            var rootHandle = graph.Root.Value.OwnerHandle.ToString();
            var rootDnSet = GetNodeDnSet(graph.Root.Value, fjvTable);
            if (rootDnSet.Count > 0)
            {
                int rootChosenDn = rootDnSet.Count == 1 ? rootDnSet.First() : rootDnSet.Min();
                var incidentAtRoot = GetIncidentEdges(rootHandle, graph.Root.Value, handleToNode, edgeStates);
                foreach (var e in incidentAtRoot)
                {
                    if (!e.State.AssignedDn.HasValue)
                        e.State.AssignedDn = rootChosenDn;
                }
            }

            // Seed DN from pipes on their incident edges
            foreach (var kvp in edgeStates)
            {
                var (a, b) = kvp.Key;
                var nodeA = handleToNode[a].Value;
                var nodeB = handleToNode[b].Value;
                int? dnA = GetPipeDnIfPolyline(nodeA);
                int? dnB = GetPipeDnIfPolyline(nodeB);

                if (dnA.HasValue && dnB.HasValue)
                {
                    if (dnA.Value == dnB.Value)
                        kvp.Value.AssignedDn = dnA.Value;
                    else
                    {
                        kvp.Value.Errors.Add("DN");
                        // Prefer not to choose; leave unassigned to continue propagation
                    }
                    continue;
                }

                if (dnA.HasValue) kvp.Value.AssignedDn = dnA.Value;
                else if (dnB.HasValue) kvp.Value.AssignedDn = dnB.Value;
            }

            // Iteratively propagate DN through components (reducers/tees)
            bool changed = true;
            int maxIters = Math.Max(1, edgeStates.Count * 2);
            int iter = 0;
            while (changed && iter++ < maxIters)
            {
                changed = false;
                foreach (var node in allNodes)
                {
                    var nodeHandle = node.Value.OwnerHandle.ToString();
                    var incident = GetIncidentEdges(nodeHandle, node.Value, handleToNode, edgeStates);
                    if (incident.Count == 0) continue;

                    var dnSet = GetNodeDnSet(node.Value, fjvTable);
                    if (dnSet.Count == 0) continue;

                    if (dnSet.Count == 1)
                    {
                        int onlyDn = dnSet.First();
                        foreach (var e in incident)
                        {
                            if (!e.State.AssignedDn.HasValue)
                            {
                                e.State.AssignedDn = onlyDn;
                                changed = true;
                            }
                            else if (e.State.AssignedDn.Value != onlyDn)
                            {
                                e.State.Errors.Add("DN");
                            }
                        }
                        continue;
                    }

                    // Two distinct DNs (reducer or tee). Use degree to infer assignments.
                    int dn1 = dnSet.Min();
                    int dn2 = dnSet.Max();
                    int deg = incident.Count;

                    // Count current assignments
                    int countDn1 = incident.Count(e => e.State.AssignedDn == dn1);
                    int countDn2 = incident.Count(e => e.State.AssignedDn == dn2);

                    if (deg == 2)
                    {
                        // Reducer: edges must be dn1 and dn2 one each
                        foreach (var e in incident)
                        {
                            if (!e.State.AssignedDn.HasValue) continue;
                            var v = e.State.AssignedDn.Value;
                            if (v != dn1 && v != dn2) e.State.Errors.Add("DN-RED");
                        }
                        if (incident.Any(e => !e.State.AssignedDn.HasValue))
                        {
                            // If one known, set the other to the remaining value
                            if (countDn1 == 1 && countDn2 == 0)
                            {
                                foreach (var e in incident)
                                {
                                    if (!e.State.AssignedDn.HasValue)
                                    {
                                        e.State.AssignedDn = dn2; changed = true;
                                    }
                                }
                            }
                            else if (countDn2 == 1 && countDn1 == 0)
                            {
                                foreach (var e in incident)
                                {
                                    if (!e.State.AssignedDn.HasValue)
                                    {
                                        e.State.AssignedDn = dn1; changed = true;
                                    }
                                }
                            }
                        }
                        // If both assigned to same DN -> error
                        if (countDn1 == 2 || countDn2 == 2)
                        {
                            foreach (var e in incident) e.State.Errors.Add("DN-RED");
                        }
                    }
                    else if (deg == 3)
                    {
                        // Tee: typically two ports with larger DN and one with smaller DN
                        foreach (var e in incident)
                        {
                            if (!e.State.AssignedDn.HasValue) continue;
                            var v = e.State.AssignedDn.Value;
                            if (v != dn1 && v != dn2) e.State.Errors.Add("DN-TEE");
                        }

                        // Enforce counts: at most one small, at most two large
                        int small = dn1; int large = dn2;
                        int countSmall = incident.Count(e => e.State.AssignedDn == small);
                        int countLarge = incident.Count(e => e.State.AssignedDn == large);
                        if (countSmall > 1 || countLarge > 2)
                        {
                            foreach (var e in incident) e.State.Errors.Add("DN-TEE");
                        }

                        // If exactly one small already, remaining unknown edges are large
                        if (countSmall == 1)
                        {
                            foreach (var e in incident)
                            {
                                if (!e.State.AssignedDn.HasValue)
                                {
                                    e.State.AssignedDn = large; changed = true;
                                }
                            }
                        }
                        // If two large already, remaining unknown edge is small
                        else if (countLarge == 2)
                        {
                            foreach (var e in incident)
                            {
                                if (!e.State.AssignedDn.HasValue)
                                {
                                    e.State.AssignedDn = small; changed = true;
                                }
                            }
                        }
                    }
                }
            }

            // After propagation, finalize attributes for directed edges present in the graph (parent -> child)
            var q2 = new Queue<Node<GraphEntity>>();
            q2.Enqueue(graph.Root);
            while (q2.Count > 0)
            {
                var parent = q2.Dequeue();
                foreach (var child in parent.Children)
                {
                    var from = parent.Value;
                    var to = child.Value;
                    var fromH = from.OwnerHandle.ToString();
                    var toH = to.OwnerHandle.ToString();
                    var undirected = SortPair(fromH, toH);
                    edgeStates.TryGetValue(undirected, out var es);

                    // Build error list for this directed edge
                    var errors = new HashSet<string>(StringComparer.Ordinal);
                    if (es != null)
                    {
                        foreach (var e in es.Errors) errors.Add(e);
                        // Validate DN against both sides' local constraints if assigned
                        if (es.AssignedDn.HasValue)
                        {
                            // from end must be compatible
                            if (!DnIsAllowedOnNodeEdge(from, toH, es.AssignedDn.Value, fjvTable)) errors.Add("DN");
                            // to end must be compatible
                            if (!DnIsAllowedOnNodeEdge(to, fromH, es.AssignedDn.Value, fjvTable)) errors.Add("DN");
                        }
                    }

                    // PipeType (system) consistency per edge ends
                    var con = from.Cons.FirstOrDefault(c => c.ConHandle == to.OwnerHandle);
                    if (con != null)
                    {
                        // Ignore type mismatches if either side is an F-Model or Y-Model
                        var fromKind = from.ElementType;
                        var toKind = to.ElementType;
                        if (!IsSpecialTransitionNode(fromKind) && !IsSpecialTransitionNode(toKind))
                        {
                            var typeFrom = GetPipeTypeOnEnd(from.Owner, con.OwnEndType, fjvTable);
                            var typeTo = GetPipeTypeOnEnd(to.Owner, con.ConEndType, fjvTable);
                            if (typeFrom != default && typeTo != default && typeFrom != typeTo)
                            {
                                // Allow Enkelt <-> Frem/Retur transitions (components report Enkelt)
                                if (!IsAllowedTypeTransition(typeFrom, typeTo))
                                    errors.Add("T/E");
                            }
                        }
                    }

                    if (errors.Count > 0)
                    {
                        var label = string.Join(", ", errors.OrderBy(x => x, StringComparer.Ordinal));
                        result[(fromH, toH)] = $"[ label=\"{label}\", color=\"red\" ]";
                    }

                    q2.Enqueue(child);
                }
            }

            // Include non-tree (cycle) edges as well
            foreach (var (a, b) in graph.ExtraEdges)
            {
                var from = a.Value; var to = b.Value;
                var fromH = from.OwnerHandle.ToString();
                var toH = to.OwnerHandle.ToString();
                var undirected = SortPair(fromH, toH);
                edgeStates.TryGetValue(undirected, out var es);

                var errors = new HashSet<string>(StringComparer.Ordinal);
                if (es != null)
                {
                    foreach (var e in es.Errors) errors.Add(e);
                    if (es.AssignedDn.HasValue)
                    {
                        if (!DnIsAllowedOnNodeEdge(from, toH, es.AssignedDn.Value, fjvTable)) errors.Add("DN");
                        if (!DnIsAllowedOnNodeEdge(to, fromH, es.AssignedDn.Value, fjvTable)) errors.Add("DN");
                    }
                }

                var con = from.Cons.FirstOrDefault(c => c.ConHandle == to.OwnerHandle) ??
                          to.Cons.FirstOrDefault(c => c.ConHandle == from.OwnerHandle);
                if (con != null)
                {
                    var typeFrom = GetPipeTypeOnEnd(from.Owner, con.OwnEndType, fjvTable);
                    var typeTo = GetPipeTypeOnEnd(to.Owner, con.ConEndType, fjvTable);
                    // If we pulled con from the reverse direction, ends might be swapped; do a defensive second attempt
                    if (typeFrom == default || typeTo == default)
                    {
                        typeFrom = GetPipeTypeOnEnd(from.Owner, con.ConEndType, fjvTable);
                        typeTo = GetPipeTypeOnEnd(to.Owner, con.OwnEndType, fjvTable);
                    }

                    var fromKind = from.ElementType;
                    var toKind = to.ElementType;
                    if (!IsSpecialTransitionNode(fromKind) && !IsSpecialTransitionNode(toKind))
                    {
                        if (typeFrom != default && typeTo != default && typeFrom != typeTo)
                        {
                            if (!IsAllowedTypeTransition(typeFrom, typeTo))
                                errors.Add("T/E");
                        }
                    }
                }

                if (errors.Count > 0)
                {
                    var label = string.Join(", ", errors.OrderBy(x => x, StringComparer.Ordinal));
                    result[(fromH, toH)] = $"[ label=\"{label}\", color=\"red\" ]";
                }
            }

            return result;
        }

        private static bool DnIsAllowedOnNodeEdge(GraphEntity node, string neighborHandleStr, int dn, FjvDynamicComponents fjvTable)
        {
            // If node is a pipe, DN must equal pipe DN
            if (node.Owner is Polyline pl)
            {
                int p = PipeScheduleV2.PipeScheduleV2.GetPipeDN(pl);
                return p == 0 || p == dn;
            }
            if (node.Owner is BlockReference br)
            {
                // Component: DN must be one of DN1/DN2 (if DN2 exists)
                int dn1 = PropertyReader.ReadComponentDN1Int(br, fjvTable);
                int dn2 = PropertyReader.ReadComponentDN2Int(br, fjvTable);
                if (dn2 == 0) return dn1 == 0 || dn1 == dn;
                if (dn1 == 0) return dn2 == dn;
                if (dn1 == dn || dn2 == dn) return true;
                return false;
            }
            return true;
        }

        private static int? GetPipeDnIfPolyline(GraphEntity ge)
        {
            if (ge.Owner is Polyline pl)
            {
                int d = PipeScheduleV2.PipeScheduleV2.GetPipeDN(pl);
                return d == 0 ? null : d;
            }
            return null;
        }

        private static List<IncidentEdge> GetIncidentEdges(
            string nodeHandle,
            GraphEntity node,
            Dictionary<string, Node<GraphEntity>> handleToNode,
            Dictionary<(string a, string b), EdgeState> edgeStates)
        {
            var result = new List<IncidentEdge>();
            foreach (var con in node.Cons)
            {
                var nh = con.ConHandle.ToString();
                if (!handleToNode.ContainsKey(nh)) continue;
                var key = SortPair(nodeHandle, nh);
                if (edgeStates.TryGetValue(key, out var state))
                {
                    result.Add(new IncidentEdge(nh, con.OwnEndType, con.ConEndType, state));
                }
            }
            return result;
        }

        private static HashSet<int> GetNodeDnSet(GraphEntity node, FjvDynamicComponents fjvTable)
        {
            var set = new HashSet<int>();
            switch (node.Owner)
            {
                case Polyline pl:
                    {
                        int dn = PipeScheduleV2.PipeScheduleV2.GetPipeDN(pl);
                        if (dn != 0) set.Add(dn);
                        break;
                    }
                case BlockReference br:
                    {
                        int dn1 = PropertyReader.ReadComponentDN1Int(br, fjvTable);
                        int dn2 = PropertyReader.ReadComponentDN2Int(br, fjvTable);
                        if (dn1 != 0) set.Add(dn1);
                        if (dn2 != 0) set.Add(dn2);
                        break;
                    }
            }
            return set;
        }

        private static (string a, string b) SortPair(string a, string b)
        {
            return string.CompareOrdinal(a, b) <= 0 ? (a, b) : (b, a);
        }

        private static PipeTypeEnum GetPipeTypeOnEnd(Entity ent, EndType endType, FjvDynamicComponents fjvTable)
        {
            if (ent is Polyline pl)
            {
                return PipeScheduleV2.PipeScheduleV2.GetPipeType(pl);
            }
            else if (ent is BlockReference br)
            {
                var sys = PropertyReader.ReadComponentSystem(br, fjvTable, endType);
                if (Enum.TryParse(sys, out PipeTypeEnum type)) return type;
            }
            return default;
        }
        private static bool IsSpecialTransitionNode(PipelineElementType kind)
        {
            return kind == PipelineElementType.F_Model || kind == PipelineElementType.Y_Model;
        }
        private static bool IsFremOrRetur(PipeTypeEnum t)
        {
            return t == PipeTypeEnum.Frem || t == PipeTypeEnum.Retur;
        }
        private static bool IsAllowedTypeTransition(PipeTypeEnum a, PipeTypeEnum b)
        {
            // Components typically report Enkelt. Accept Enkelt <-> Frem/Retur transitions.
            if (a == PipeTypeEnum.Enkelt && IsFremOrRetur(b)) return true;
            if (b == PipeTypeEnum.Enkelt && IsFremOrRetur(a)) return true;
            return false;
        }
        

        private sealed class EdgeState
        {
            public string A { get; }
            public string B { get; }
            public int? AssignedDn { get; set; }
            public HashSet<string> Errors { get; } = new HashSet<string>(StringComparer.Ordinal);
            public EdgeState(string a, string b) { A = a; B = b; }
        }

        private readonly struct IncidentEdge
        {
            public string NeighborHandle { get; }
            public EndType OwnEnd { get; }
            public EndType ConEnd { get; }
            public EdgeState State { get; }
            public IncidentEdge(string nh, EndType own, EndType con, EdgeState state)
            {
                NeighborHandle = nh; OwnEnd = own; ConEnd = con; State = state;
            }
        }

        private sealed class TuplePairComparer : IEqualityComparer<(string a, string b)>
        {
            public static TuplePairComparer Instance { get; } = new TuplePairComparer();
            public bool Equals((string a, string b) x, (string a, string b) y)
            {
                return string.Equals(x.a, y.a, StringComparison.Ordinal) &&
                       string.Equals(x.b, y.b, StringComparison.Ordinal);
            }
            public int GetHashCode((string a, string b) obj)
            {
                unchecked
                {
                    int h1 = obj.a?.GetHashCode(StringComparison.Ordinal) ?? 0;
                    int h2 = obj.b?.GetHashCode(StringComparison.Ordinal) ?? 0;
                    return (h1 * 397) ^ h2;
                }
            }
        }

        public static string? GetAttributes(
            Entity ent1, EndType endType1,
            Entity ent2, EndType endType2,
            FjvDynamicComponents fjvTable)
        {
            var errors = new List<string>();

            try
            {
                Qa_System(ent1, endType1, ent2, endType2, fjvTable, errors);
                Qa_Dn(ent1, endType1, ent2, endType2, fjvTable, errors);
            }
            catch
            {
                // if QA fails unexpectedly, prefer not to block output
            }

            if (errors.Count == 0) return null;
            var label = string.Join(", ", errors);
            return $"[ label=\"{label}\", color=\"red\" ]";
        }

        private static void Qa_System(
            Entity ent1, EndType endType1,
            Entity ent2, EndType endType2,
            FjvDynamicComponents fjvTable,
            List<string> errors)
        {
            PipeTypeEnum type1 = default;
            PipeTypeEnum type2 = default;

            if (ent1 is Polyline pl1)
                type1 = PipeScheduleV2.PipeScheduleV2.GetPipeType(pl1);
            else if (ent1 is BlockReference br1)
            {
                var sys = PropertyReader.ReadComponentSystem(br1, fjvTable, endType1);
                Enum.TryParse(sys, out type1);
                // Ignore mismatches if F-Model/Y-Model present on this side
                var kind1 = br1.GetPipelineType();
                if (IsSpecialTransitionNode(kind1)) return;
            }

            if (ent2 is Polyline pl2)
                type2 = PipeScheduleV2.PipeScheduleV2.GetPipeType(pl2);
            else if (ent2 is BlockReference br2)
            {
                var sys = PropertyReader.ReadComponentSystem(br2, fjvTable, endType2);
                Enum.TryParse(sys, out type2);
                // Ignore mismatches if F-Model/Y-Model present on this side
                var kind2 = br2.GetPipelineType();
                if (IsSpecialTransitionNode(kind2)) return;
            }

            if (type1 != default && type2 != default && type1 != type2)
            {
                // Allow Enkelt <-> Frem/Retur transitions
                if (!IsAllowedTypeTransition(type1, type2))
                    errors.Add("T/E");
            }
        }

        private static void Qa_Dn(
            Entity ent1, EndType endType1,
            Entity ent2, EndType endType2,
            FjvDynamicComponents fjvTable,
            List<string> errors)
        {
            var dnSet1 = new HashSet<int>();
            var dnSet2 = new HashSet<int>();

            if (ent1 is Polyline pl1)
            {
                var dn = PipeScheduleV2.PipeScheduleV2.GetPipeDN(pl1);
                if (dn != 0) dnSet1.Add(dn);
            }
            else if (ent1 is BlockReference br1)
            {
                var dn1 = SafeParse(PropertyReader.ReadComponentDN1Str(br1, fjvTable));
                var dn2 = SafeParse(PropertyReader.ReadComponentDN2Str(br1, fjvTable));
                if (dn1 != 0) dnSet1.Add(dn1);
                if (dn2 != 0) dnSet1.Add(dn2);
            }

            if (ent2 is Polyline pl2)
            {
                var dn = PipeScheduleV2.PipeScheduleV2.GetPipeDN(pl2);
                if (dn != 0) dnSet2.Add(dn);
            }
            else if (ent2 is BlockReference br2)
            {
                var dn1 = SafeParse(PropertyReader.ReadComponentDN1Str(br2, fjvTable));
                var dn2 = SafeParse(PropertyReader.ReadComponentDN2Str(br2, fjvTable));
                if (dn1 != 0) dnSet2.Add(dn1);
                if (dn2 != 0) dnSet2.Add(dn2);
            }

            if (dnSet1.Count == 0 || dnSet2.Count == 0)
                return; // skip if insufficient data

            if (dnSet1.Count == 1 && dnSet2.Count == 1)
            {
                var a = First(dnSet1);
                var b = First(dnSet2);
                if (a != b) errors.Add("DN");
                return;
            }

            if (dnSet1.Count == 2 && dnSet2.Count == 2)
            {
                errors.Add("DN forvirring");
                return;
            }

            if (dnSet1.Count == 2)
            {
                var single = First(dnSet2);
                if (!dnSet1.Contains(single)) errors.Add("DN");
                return;
            }
            if (dnSet2.Count == 2)
            {
                var single = First(dnSet1);
                if (!dnSet2.Contains(single)) errors.Add("DN");
            }
        }

        private static int SafeParse(string s)
        {
            if (int.TryParse(s, out var v)) return v;
            return 0;
        }

        private static int First(HashSet<int> set)
        {
            foreach (var v in set) return v;
            return 0;
        }
    }
}