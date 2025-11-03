using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;

using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.Enums;
using IntersectUtilities.UtilsCommon.Graphs;

using System;
using System.Collections.Generic;
using System.Linq;

namespace IntersectUtilities.GraphWriteV2
{
    internal sealed class ComponentTree
    {
        public global::IntersectUtilities.UtilsCommon.Graphs.Graph<NodeContext> Graph { get; }
        public Dictionary<(Handle from, Handle to), (EndType fromEnd, EndType toEnd)> EdgeEndTypes { get; }
        public string RootAlignment { get; }

        public ComponentTree(
            global::IntersectUtilities.UtilsCommon.Graphs.Graph<NodeContext> graph,
            Dictionary<(Handle from, Handle to), (EndType fromEnd, EndType toEnd)> edgeEndTypes,
            string rootAlignment)
        {
            Graph = graph;
            EdgeEndTypes = edgeEndTypes;
            RootAlignment = rootAlignment;
        }
    }

    internal sealed class GraphBuilderV2
    {
        private readonly Database _db;
        private readonly System.Data.DataTable _fjvTable;
        private readonly PropertySetManager _psmGraph;
        private readonly PropertySetManager _psmPipeline;
        private readonly PSetDefs.DriPipelineData _driPipelineData = new();

        public GraphBuilderV2(Database db, System.Data.DataTable fjvTable)
        {
            _db = db;
            _fjvTable = fjvTable;
            _psmGraph = new PropertySetManager(_db, PSetDefs.DefinedSets.DriGraph);
            _psmPipeline = new PropertySetManager(_db, PSetDefs.DefinedSets.DriPipelineData);
        }

        public List<ComponentTree> BuildForest(IEnumerable<Entity> allEntities)
        {
            // Map entities
            var entities = allEntities.ToDictionary(e => e.Handle, e => e);

            // Build GraphEntity for connectivity
            var graphEntities = entities.Values
                .Select(e => new GraphEntity(e, _psmGraph))
                .ToDictionary(ge => ge.OwnerHandle, ge => ge);

            // Build adjacency with end types (bidirectional)
            var adjacency = new Dictionary<Handle, List<(Handle neighbor, EndType ownEnd, EndType conEnd)>>();
            void AddEdge(Handle from, Handle to, EndType fromEnd, EndType toEnd)
            {
                if (!adjacency.TryGetValue(from, out var list))
                {
                    list = new List<(Handle, EndType, EndType)>();
                    adjacency[from] = list;
                }
                list.Add((to, fromEnd, toEnd));
            }

            foreach (var ge in graphEntities.Values)
            {
                foreach (var con in ge.Cons)
                {
                    if (!entities.ContainsKey(con.ConHandle)) continue;
                    AddEdge(ge.OwnerHandle, con.ConHandle, con.OwnEndType, con.ConEndType);
                    AddEdge(con.ConHandle, ge.OwnerHandle, con.ConEndType, con.OwnEndType);
                }
            }

            // Partition into connected components
            var unvisited = new HashSet<Handle>(entities.Keys);
            var components = new List<HashSet<Handle>>();
            var stack = new Stack<Handle>();

            while (unvisited.Count > 0)
            {
                var start = unvisited.First();
                var comp = new HashSet<Handle>();
                stack.Push(start);
                unvisited.Remove(start);
                while (stack.Count > 0)
                {
                    var h = stack.Pop();
                    comp.Add(h);
                    if (!adjacency.TryGetValue(h, out var neigh)) continue;
                    foreach (var (n, _, _) in neigh)
                    {
                        if (unvisited.Remove(n))
                            stack.Push(n);
                    }
                }
                components.Add(comp);
            }

            // Build a spanning tree per component
            var result = new List<ComponentTree>();
            foreach (var comp in components)
            {
                // Root selection (legacy semantics):
                // - Structural leaf: degree == 1 when excluding StikAfgrening and WeldOn (by own end type)
                // - Special case: nodes with only WeldOn connections but at least one WeldOn
                // - Fallback: node with max DN in the component
                int StructuralDegree(Handle h)
                {
                    if (!adjacency.TryGetValue(h, out var lst)) return 0;
                    return lst.Count(p => comp.Contains(p.neighbor)
                        && p.ownEnd != EndType.WeldOn
                        && p.ownEnd != EndType.StikAfgrening);
                }
                bool HasOnlyWeldOnConnections(Handle h)
                {
                    if (!adjacency.TryGetValue(h, out var lst)) return false;
                    int nonWeld = lst.Count(p => comp.Contains(p.neighbor) && p.ownEnd != EndType.WeldOn);
                    int weld = lst.Count(p => comp.Contains(p.neighbor) && p.ownEnd == EndType.WeldOn);
                    return nonWeld == 0 && weld > 0;
                }

                var candidates = comp.Where(h => StructuralDegree(h) == 1 || (StructuralDegree(h) == 0 && HasOnlyWeldOnConnections(h)));

                Handle rootHandle = candidates.Any()
                    ? candidates.OrderByDescending(h => LargestDnOf(h, entities, graphEntities)).First()
                    : comp.OrderByDescending(h => LargestDnOf(h, entities, graphEntities)).First();

                // BFS spanning tree construction
                var handleToNode = new Dictionary<Handle, Node<NodeContext>>();
                var visited = new HashSet<Handle>();
                var q = new Queue<Handle>();
                var edgeEndTypes = new Dictionary<(Handle from, Handle to), (EndType fromEnd, EndType toEnd)>();

                var rootContext = BuildNodeContext(rootHandle, entities[rootHandle]);
                var rootNode = new Node<NodeContext>(rootContext);
                handleToNode[rootHandle] = rootNode;
                visited.Add(rootHandle);
                q.Enqueue(rootHandle);

                while (q.Count > 0)
                {
                    var current = q.Dequeue();
                    if (!adjacency.TryGetValue(current, out var neighs)) continue;
                    foreach (var (n, ownEnd, conEnd) in neighs)
                    {
                        if (!comp.Contains(n)) continue;
                        if (visited.Contains(n)) continue;
                        var childContext = BuildNodeContext(n, entities[n]);
                        var childNode = new Node<NodeContext>(childContext);
                        handleToNode[n] = childNode;
                        handleToNode[current].AddChild(childNode);
                        edgeEndTypes[(current, n)] = (ownEnd, conEnd);
                        visited.Add(n);
                        q.Enqueue(n);
                    }
                }

                // Build Graph<T>
                Func<NodeContext, string> nameSel = nc => nc.Handle.ToString();
                Func<NodeContext, string> labelSel = nc =>
                {
                    // record-style, quoted
                    return $"\"{{{nc.Handle}|{nc.TypeLabel}}}|{nc.SystemLabel}\\n{nc.DnLabel}\"";
                };
                var graph = new global::IntersectUtilities.UtilsCommon.Graphs.Graph<NodeContext>(rootNode, nameSel, labelSel);

                // Root alignment (cluster to emphasize)
                var rootAlignment = rootContext.Alignment ?? string.Empty;

                result.Add(new ComponentTree(graph, edgeEndTypes, rootAlignment));
            }

            return result;
        }

        private NodeContext BuildNodeContext(Handle handle, Entity entity)
        {
            string alignment = _psmPipeline.ReadPropertyString(entity, _driPipelineData.BelongsToAlignment) ?? string.Empty;
            string typeLabel;
            string systemLabel;
            string dnLabel;
            int largestDn = 0;

            switch (entity)
            {
                case Polyline pl:
                    {
                        typeLabel = $"Rør L{pl.Length.ToString("0.##")}";
                        var psys = PipeScheduleV2.PipeScheduleV2.GetPipeSystem(pl).ToString();
                        var ptype = PipeScheduleV2.PipeScheduleV2.GetPipeType(pl).ToString();
                        systemLabel = $"{psys} {ptype}";
                        var dn = PipeScheduleV2.PipeScheduleV2.GetPipeDN(pl);
                        dnLabel = dn.ToString();
                        largestDn = dn;
                        break;
                    }
                case BlockReference br:
                    {
                        typeLabel = br.ReadDynamicCsvProperty(DynamicProperty.Type);
                        systemLabel = ComponentSchedule.ReadComponentSystem(br, _fjvTable);
                        var dn1 = ComponentSchedule.ReadComponentDN1(br, _fjvTable);
                        var dn2 = ComponentSchedule.ReadComponentDN2(br, _fjvTable);
                        dnLabel = string.IsNullOrWhiteSpace(dn2) || dn2 == "0" ? dn1 : $"{dn1}/{dn2}";
                        if (!int.TryParse(dn1, out largestDn)) largestDn = 0;
                        break;
                    }
                default:
                    typeLabel = entity.GetType().Name;
                    systemLabel = string.Empty;
                    dnLabel = string.Empty;
                    largestDn = 0;
                    break;
            }

            return new NodeContext(handle, entity, alignment, typeLabel, systemLabel, dnLabel, largestDn);
        }

        private static int LargestDnOf(
            Handle handle,
            IDictionary<Handle, Entity> entities,
            IDictionary<Handle, GraphEntity> graphEntities)
        {
            if (graphEntities.TryGetValue(handle, out var ge))
                return ge.LargestDn();
            var e = entities[handle];
            if (e is Polyline pl) return PipeScheduleV2.PipeScheduleV2.GetPipeDN(pl);
            if (e is BlockReference br)
            {
                var s = ComponentSchedule.ReadComponentDN1(br, CsvData.FK);
                return Convert.ToInt32(s);
            }
            return 0;
        }
    }
}