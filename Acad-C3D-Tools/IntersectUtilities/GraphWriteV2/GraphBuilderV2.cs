using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;

using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.Enums;
using IntersectUtilities.UtilsCommon.Graphs;
using static IntersectUtilities.UtilsCommon.Utils;

using System;
using System.Collections.Generic;
using System.Linq;
using IntersectUtilities.GraphWrite;

namespace IntersectUtilities.GraphWriteV2
{
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

        public List<Graph<GraphEntity>> BuildGraphs(IEnumerable<Entity> allEntities)
        {
            // Map entities
            var entities = allEntities.ToDictionary(e => e.Handle, e => e);

            // Build GraphEntity for connectivity (unvisited working set)
            var unvisited = entities.Values
                .Select(e => new GraphEntity(e, _psmGraph))
                .ToHashSet();

            // Stable lookup for neighbor resolution
            var byHandle = unvisited.ToDictionary(x => x.OwnerHandle, x => x);

            var result = new List<Graph<GraphEntity>>();

            int graphCount = 0;

            // Traverse each connected component
            while (unvisited.Count > 0)
            {
                graphCount++;
                bool isEntryPoint = true;

                // Select an entry point: degree-1 (excluding specific end types) with largest DN
                GraphEntity? entry = unvisited
                    .Where(x =>
                        (x.Cons.Count(y => y.OwnEndType != EndType.StikAfgrening && y.OwnEndType != EndType.WeldOn) == 1) ||
                        (x.Cons.Count(y => y.OwnEndType != EndType.WeldOn) == 0 && x.Cons.Count(y => y.OwnEndType == EndType.WeldOn) > 0))
                    .MaxBy(x => x.LargestDn());

                if (entry == null)
                {
                    prdDbg("ERROR: Graph not complete!!!");
                    prdDbg(
                        "Check if entry pipe has a connection (afgreningsstuds or stik.)" +
                        "\nThis could prevent it from registering as entry element.");

                    throw new DebugEntityException(
                        "See above.", unvisited.Select(x => x.Owner).ToList());
                }

                prdDbg("Entry: " + entry.OwnerHandle.ToString());

                // Per-component state
                var visited = new HashSet<GraphEntity>();
                var handleToNode = new Dictionary<Handle, Node<GraphEntity>>();
                Node<GraphEntity>? rootNode = null;

                // DFS stack carries the parent node to link
                var stack = new Stack<(GraphEntity entity, Node<GraphEntity>? parent)>();
                stack.Push((entry, null));
                Graph<GraphEntity>? currentGraph = null;

                while (stack.Count > 0)
                {
                    var (current, parentNode) = stack.Pop();
                    if (visited.Contains(current)) continue;

                    visited.Add(current);
                    unvisited.Remove(current);

                    // Get or create a node for the current entity
                    if (!handleToNode.TryGetValue(current.OwnerHandle, out var currentNode))
                    {
                        currentNode = new Node<GraphEntity>(current);
                        handleToNode[current.OwnerHandle] = currentNode;
                    }

                    // Link to parent (build spanning tree)
                    if (parentNode != null)
                    {
                        parentNode.AddChild(currentNode);
                    }

                    // Initialize the graph on the first visited node for this component
                    if (isEntryPoint)
                    {
                        Func<GraphEntity, string> nameSel = n => n.OwnerHandle.ToString();
                        Func<GraphEntity, string> labelSel = n =>
                        {
                            return $"\"{{{n.OwnerHandle}|{n.TypeLabel}}}|{n.SystemLabel}\\n{n.DnLabel}\"";
                        };

                        rootNode = currentNode;
                        currentGraph = new Graph<GraphEntity>(rootNode, nameSel, labelSel);
                        result.Add(currentGraph);
                        isEntryPoint = false;
                    }

                    // Explore neighbors
                    foreach (Con con in current.Cons)
                    {
                        if (!byHandle.TryGetValue(con.ConHandle, out var neighbor)) continue;
                        if (ReferenceEquals(neighbor, current)) continue;
                        if (visited.Contains(neighbor))
                        {
                            // Record non-tree (cycle) edges: connect currentNode to an already-visited neighbor that isn't the parent
                            if (currentGraph != null && handleToNode.TryGetValue(con.ConHandle, out var neighborNode))
                            {
                                if (!ReferenceEquals(neighborNode, currentNode.Parent))
                                {
                                    currentGraph.AddCycleEdge(currentNode, neighborNode);
                                }
                            }
                            continue;
                        }
                        stack.Push((neighbor, currentNode));
                    }
                }
            }

            return result;
        }        
    }
}