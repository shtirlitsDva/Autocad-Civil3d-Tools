using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.DatabaseServices;

using IntersectUtilities;
using IntersectUtilities.PipelineNetworkSystem;
using IntersectUtilities.PipelineNetworkSystem.PipelineSizeArray;
using IntersectUtilities.PipeScheduleV2;
using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.DataManager;
using IntersectUtilities.UtilsCommon.Enums;

using NTRExport.Elevation;
using NTRExport.Ntr;
using NTRExport.NtrConfiguration;
using NTRExport.Routing;
using NTRExport.SoilModel;
using NTRExport.TopologyModel;

using System.Diagnostics;
using System.IO;
using System.Text;

using static IntersectUtilities.UtilsCommon.Utils;

using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;

[assembly: CommandClass(typeof(NTRExport.Commands))]

namespace NTRExport
{
        public class Commands : IExtensionApplication
    {
        private const double CushionReachMeters = 2.0;
        #region IExtensionApplication members
        public void Initialize()
        {
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            doc.Editor.WriteMessage("\nVelkommen til NTR Export!\n");
#if DEBUG
            AppDomain.CurrentDomain.AssemblyResolve +=
        new ResolveEventHandler(MissingAssemblyLoader.Debug_AssemblyResolve);
#endif
        }

        public void Terminate()
        {
            //Add your termination code here
        }
        #endregion

        [CommandMethod("DXFEXPORT")]
        public void dxfexport()
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            DataManager dm = new(new DataReferencesOptions());

            using var fDb = dm.Fremtid();
            using var fTx = fDb.TransactionManager.StartTransaction();

            using var aDb = dm.Alignments();
            using var aTx = aDb.TransactionManager.StartTransaction();

            using var tx = localDb.TransactionManager.StartTransaction();

            #region Clean previous entities
            var toDelete = localDb.GetFjvPipes(tx);
            if (toDelete.Count > 0)
            {
                foreach (var item in toDelete)
                {
                    item.CheckOrOpenForWrite();
                    item.Erase(true);
                }
            }
            #endregion

            try
            {
                var ents = fDb.GetFjvEntities(fTx);
                var als = aDb.HashSetOfType<Alignment>(aTx);

                PipelineNetwork pn = new PipelineNetwork();
                pn.CreatePipelineNetwork(ents, als);
                pn.CreatePipelineGraph();
                pn.CreateSizeArrays();

                foreach (var graph in pn.PipelineGraphs)
                {
                    foreach (var node in graph)
                    {
                        IPipelineV2 ppl = node.Value;
                        var topopl = ppl.GetTopologyPolyline();
                        var sizes = ppl.PipelineSizes;

                        #region First try
                        List<(Polyline pl, SizeEntryV2 size)> results = new();
                        if (sizes.Length == 1)
                            results.Add((topopl, sizes[0]));
                        else
                        {
                            DoubleCollection dc = new DoubleCollection();

                            //Skip first as Start Station is zero
                            for (int i = 1; i < sizes.Length; i++)
                            {
                                var l = sizes[i].StartStation;
                                dc.Add(topopl.GetParameterAtDistance(l));
                            }

                            using var objs = topopl.GetSplitCurves(dc);

                            if (objs.Count != sizes.Length)
                            {
                                prdDbg($"Pipeline {ppl.Name} failed to split correctly!");
                                prdDbg($"Sizes count: {sizes.Length}; Objs count: {objs.Count}");
                                prdDbg(sizes);
                                continue;
                            }

                            for (int i = 0; i < sizes.Length; i++)
                            {
                                results.Add(((Polyline)objs[i], sizes[i]));
                            }
                        }

                        foreach (var (pl, size) in results)
                        {
                            var layer = PipeScheduleV2.GetLayerName(
                                size.DN, size.System, size.Type);
                            localDb.CheckOrCreateLayer(
                                layer, PipeScheduleV2.GetColorForDim(layer));
                            pl.Layer = layer;
                            pl.ConstantWidth = PipeScheduleV2.GetPipeKOd(
                                size.System, size.DN, size.Type, size.Series) / 1000;
                            pl.AddEntityToDbModelSpace(localDb);
                        }
                        #endregion
                    }
                }
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                tx.Abort();
                throw;
            }
            tx.Commit();
        }

        [CommandMethod("NTREXPORT")]
        public void ntrexport()
        {
            ntrexportmethod();
        }
#if DEBUG
        [CommandMethod("NTRTEST")]
        public void ntrtest()
        {
            ntrexportmethod(new ConfigurationData(
                new NtrLast("FJV_FREM_P10_T80", "10", "80", "1000"),
                new NtrLast("FJV_RETUR_P10_T45", "10", "45", "1000")));
        }
#endif
        internal void ntrexportmethod(ConfigurationData? ntrConf = null)
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            using var tx = localDb.TransactionManager.StartTransaction();

            var dwgPath = localDb.Filename;
            var outNtrPath = Path.ChangeExtension(
                string.IsNullOrEmpty(dwgPath) ? "export" : dwgPath, ".ntr");
            var outExceptionPath = Path.ChangeExtension(
                string.IsNullOrEmpty(dwgPath) ? "export" : dwgPath, ".exception.log");

            try
            {
                var ents = localDb.GetFjvEntities(tx);

                #region ------------- FILTER -------------
                var acceptedSystems = new HashSet<PipeSystemEnum>() { PipeSystemEnum.Stål };

                bool isAccepted(Entity ent)
                {
#if DEBUG
                    //prdDbg("DEBUG filtering active! Polylines only.");
                    //if (ent is BlockReference) return false;
#endif

                    switch (ent)
                    {
                        case Polyline _:
                            return acceptedSystems.Contains(
                                PipeScheduleV2.GetPipeSystem(ent));
                        case BlockReference br:
                            return acceptedSystems.Contains(
                                br.GetPipeSystemEnum());
                        default:
                            return false;
                    }
                }

                ents = ents.Where(x => isAccepted(x)).ToHashSet();
                #endregion

                #region ------------- CONFIG -------------
                const double CushionReach = 2.0;       // m
                var soilDefault = new SoilProfile(
                    name: "Soil_Default",
                    coverHeight: 0.6,
                    groundWaterDistance: null,
                    soilWeightAbove: null,
                    soilWeightBelow: null,
                    frictionAngleDeg: null,
                    cushionType: null,
                    cushionThickness: 0.0);
                var soilC80 = new SoilProfile(
                    name: "Soil_C80",
                    coverHeight: 0.6,
                    groundWaterDistance: null,
                    soilWeightAbove: null,
                    soilWeightBelow: null,
                    frictionAngleDeg: null,
                    cushionType: 2,
                    cushionThickness: 0.08);
                #endregion

                #region ------------- CAD ➜ Port topology -------------
                var polylines = ents.OfType<Polyline>().ToList();
                var fittings = ents.OfType<BlockReference>().ToList();
                var topo = TopologyBuilder.Build(polylines, fittings);
                #endregion

                #region ------------- Read NTR configuration from Excel -------------
                var conf = ntrConf ?? new ConfigurationData();
                //foreach (var l in conf.Last) prdDbg(l);
                #endregion

                #region ------------- Initialize NTR coordinate normalization -------------
                NtrCoord.InitFromTopology(topo, marginMeters: 0.0);
                #endregion

                #region ------------- Topology ➜ Traversal ➜ Routed skeleton -------------
                var routed = new Router(topo, CushionReachMeters).Route();
                var soilProfiles = new Dictionary<SoilHintKind, SoilProfile>
                {
                    { SoilHintKind.Cushion, soilC80 },
                };
                new SoilPlanner(routed, soilDefault, soilProfiles).Apply();
                #endregion

                #region ------------- Emit NTR -------------
                var writer = new NtrWriter(new Rohr2SoilAdapter(), conf);
                // Build IS and DN records across all distinct pipe groups found in the routed model
                var headerLines = new List<string>();
                var headerDedup = new HashSet<string>();

                // Group routed members by System, normalized Type (Enkelt/Twin), Series, and Twin flag
                var routedGroups = routed.Members.GroupBy(m => (
                    sys: m.System,
                    typ: m.Type == PipeTypeEnum.Twin ? PipeTypeEnum.Twin : PipeTypeEnum.Enkelt,
                    ser: m.Series,
                    twin: m.Type == PipeTypeEnum.Twin
                ));

                foreach (var rgrp in routedGroups)
                {
                    var dnsInGroup = new HashSet<int>();

                    foreach (var member in rgrp)
                    {
                        switch (member)
                        {
                            case RoutedReducer red:
                                if (red.Dn1 > 0) dnsInGroup.Add(red.Dn1);
                                if (red.Dn2 > 0) dnsInGroup.Add(red.Dn2);
                                break;
                            case RoutedTee tee:
                                if (tee.DN > 0) dnsInGroup.Add(tee.DN);
                                if (tee.DnBranch > 0) dnsInGroup.Add(tee.DnBranch);
                                break;
                            default:
                                if (member.DN > 0) dnsInGroup.Add(member.DN);
                                break;
                        }
                    }

                    if (dnsInGroup.Count == 0) continue;

                    var catalog = new NtrDnCatalog(rgrp.Key.sys, rgrp.Key.typ, rgrp.Key.ser, rgrp.Key.twin);
                    foreach (var line in catalog.BuildRecords(dnsInGroup))
                    {
                        if (headerDedup.Add(line)) headerLines.Add(line);
                    }
                }

                var ntrText = writer.Build(routed, headerLines);

                // Save next to DWG                
                File.WriteAllText(outNtrPath, ntrText, System.Text.Encoding.UTF8);
                prdDbg($"NTR written: {outNtrPath}");
                #endregion
            }
            catch (DebugPointException dbex)
            {
                prdDbg(dbex);

                File.WriteAllText(outExceptionPath,
                    dbex.ToString(),
                    System.Text.Encoding.UTF8);

                tx.Abort();

                using var dtx = localDb.TransactionManager.StartTransaction();
                foreach (var p in dbex.PointsToMark)
                {
                    DebugHelper.CreateDebugLine(p, ColorByName("red"));
                }
                dtx.Commit();
                return;
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);

                File.WriteAllText(outExceptionPath,
                    ex.ToString(),
                    System.Text.Encoding.UTF8);

                tx.Abort();
                return;
            }
            tx.Commit();
        }

#if DEBUG
        [CommandMethod("NTRDOT")]
        public void ntrdot()
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            using var tx = localDb.TransactionManager.StartTransaction();
            try
            {
                var dwgPath = localDb.Filename;
                var outDotPath = @"C:\Temp\ntrdot.dot";

                // Build topology (same filter as export)
                var ents = localDb.GetFjvEntities(tx);
                var acceptedSystems = new HashSet<PipeSystemEnum>() { PipeSystemEnum.Stål };
                bool isAccepted(Entity ent)
                {
                    switch (ent)
                    {
                        case Polyline _:
                            return acceptedSystems.Contains(PipeScheduleV2.GetPipeSystem(ent));
                        case BlockReference br:
                            return acceptedSystems.Contains(br.GetPipeSystemEnum());
                        default:
                            return false;
                    }
                }
                ents = ents.Where(x => isAccepted(x)).ToHashSet();

                var polylines = ents.OfType<Polyline>().ToList();
                var fittings = ents.OfType<BlockReference>().ToList();
                var topo = TopologyBuilder.Build(polylines, fittings);

                // Root selection debug (no traversal) - logs candidates and chosen roots per subnet
                prdDbg("[NTRDOT] Root selection debug start...");
                RootSelector.DebugPickRoots(topo);

                // Build node -> (element, port) adjacency
                var nodeAdj = new Dictionary<TNode, List<(ElementBase el, TPort port)>>(new RefEq<TNode>());
                foreach (var el in topo.Elements)
                {
                    foreach (var p in el.Ports)
                    {
                        if (!nodeAdj.TryGetValue(p.Node, out var list)) nodeAdj[p.Node] = list = new();
                        list.Add((el, p));
                    }
                }

                // Build element adjacency (undirected)
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

                // Find connected subnets of elements
                var subnets = new List<List<ElementBase>>();
                var visited = new HashSet<ElementBase>(new RefEq<ElementBase>());
                foreach (var el in topo.Elements)
                {
                    if (!visited.Add(el))
                        continue;
                    var subnet = new List<ElementBase>();
                    var q = new Queue<ElementBase>();
                    q.Enqueue(el);
                    while (q.Count > 0)
                    {
                        var cur = q.Dequeue();
                        subnet.Add(cur);
                        if (elAdj.TryGetValue(cur, out var nbrs))
                        {
                            foreach (var n in nbrs)
                            {
                                if (visited.Add(n))
                                    q.Enqueue(n);
                            }
                        }
                    }
                    subnets.Add(subnet);
                }

                // Emit DOT
                var sb = new StringBuilder();
                sb.AppendLine("graph G {");
                sb.AppendLine("  graph [compound=true];");
                sb.AppendLine("  node [shape=box, fontsize=10];");

                // Node labels and subnets
                int subnetIdx = 0;
                foreach (var comp in subnets)
                {
                    sb.AppendLine($"  subgraph cluster_{subnetIdx} {{");
                    sb.AppendLine($"    label=\"subnet {subnetIdx}\";");
                    sb.AppendLine("    color=lightgrey;");

                    // Define nodes
                    foreach (var e in comp)
                    {
                        var id = e.Source.ToString();
                        var label = e.DotLabelForTest();
                        sb.AppendLine($"    \"{id}\" [label=\"{label}\"];");
                    }

                    // DFS edges inside subnet, starting from chosen root
                    ElementBase ChooseRootForComp(List<ElementBase> c)
                    {
                        // Leaf-by-port: at least one port has no neighbor
                        bool IsLeafByPort(ElementBase el)
                        {
                            foreach (var p in el.Ports)
                            {
                                if (!nodeAdj.TryGetValue(p.Node, out var list)) return true;
                                if (DegreeExcluding(list, el) == 0) return true;
                            }
                            return false;
                        }

                        int filtered = 0;
                        ElementBase? best = null;
                        int bestDn = -1;
                        foreach (var e in c)
                        {
                            if (!IsLeafByPort(e)) { filtered++; continue; }
                            int dn = 0;
                            try { dn = e.DN; } catch { dn = 0; }
                            if (dn > bestDn || (dn == bestDn && best != null && string.CompareOrdinal(e.Source.ToString(), best.Source.ToString()) < 0))
                            {
                                bestDn = dn;
                                best = e;
                            }
                        }
                        prdDbg($"[NTRDOT] Subnet {subnetIdx}: {filtered} component(s) with leafByPort=False were not considered.");
                        if (best != null) return best;
                        // Fallback: element with largest DN in subnet
                        best = c.OrderByDescending(x => { try { return x.DN; } catch { return 0; } })
                                .ThenBy(x => x.Source.ToString(), StringComparer.Ordinal)
                                .First();
                        return best;
                    }

                    int DegreeExcluding(List<(ElementBase el, TPort port)> items, ElementBase exclude)
                    {
                        var set = new HashSet<ElementBase>(new RefEq<ElementBase>());
                        foreach (var t in items)
                        {
                            if (ReferenceEquals(t.el, exclude)) continue;
                            set.Add(t.el);
                        }
                        return set.Count;
                    }

                    int ElementDegree(ElementBase el, Dictionary<TNode, List<(ElementBase el, TPort port)>> adj)
                    {
                        var set = new HashSet<ElementBase>(new RefEq<ElementBase>());
                        foreach (var p in el.Ports)
                        {
                            if (!adj.TryGetValue(p.Node, out var list)) continue;
                            foreach (var t in list)
                            {
                                if (ReferenceEquals(t.el, el)) continue;
                                set.Add(t.el);
                            }
                        }
                        return set.Count;
                    }

                    var root = ChooseRootForComp(comp);
                    prdDbg($"[NTRDOT] Subnet {subnetIdx} DFS root: {root.Source} / {root.GetType().Name}");

                    // Build DFS ordered edges
                    var dfsVisited = new HashSet<ElementBase>(new RefEq<ElementBase>());
                    var edgesOrdered = new List<(string a, string b)>();
                    var edgeSeen = new HashSet<string>(StringComparer.Ordinal);

                    void Dfs(ElementBase cur, ElementBase? parent)
                    {
                        dfsVisited.Add(cur);
                        if (!elAdj.TryGetValue(cur, out var nbrs)) return;
                        // stable order
                        var ordered = nbrs.Where(n => comp.Contains(n))
                                          .OrderBy(n => n.Source.ToString(), StringComparer.Ordinal)
                                          .ToList();
                        foreach (var n in ordered)
                        {
                            if (parent != null && ReferenceEquals(n, parent)) continue;
                            if (!dfsVisited.Contains(n))
                            {
                                var ida = cur.Source.ToString();
                                var idb = n.Source.ToString();
                                if (!string.Equals(ida, idb, StringComparison.Ordinal))
                                {
                                    // dedupe undirected edge
                                    var key = string.CompareOrdinal(ida, idb) <= 0 ? $"{ida}--{idb}" : $"{idb}--{ida}";
                                    if (edgeSeen.Add(key))
                                        edgesOrdered.Add((ida, idb));
                                }
                                Dfs(n, cur);
                            }
                        }
                    }

                    Dfs(root, null);
                    // Cover any isolated or remaining nodes (safety)
                    foreach (var e in comp)
                    {
                        if (!dfsVisited.Contains(e))
                            Dfs(e, null);
                    }

                    foreach (var (a, b) in edgesOrdered)
                        sb.AppendLine($"    \"{a}\" -- \"{b}\";");

                    sb.AppendLine("  }");
                    subnetIdx++;
                }

                sb.AppendLine("}");

                var utf8NoBom = new UTF8Encoding(false);
                File.WriteAllText(outDotPath, sb.ToString(), utf8NoBom);
                prdDbg($"DOT written: {outDotPath}");

                // Run Graphviz (PDF)
                var cmd = new Process();
                cmd.StartInfo.FileName = "dot";
                cmd.StartInfo.WorkingDirectory = @"C:\Temp\";
                cmd.StartInfo.Arguments = "-Tpdf ntrdot.dot -o ntrdot.pdf";
                cmd.StartInfo.UseShellExecute = false;
                cmd.Start();
                cmd.WaitForExit();
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                tx.Abort();
                return;
            }
            tx.Commit();
        }
#endif

        [CommandMethod("NTRCREATEPS")]
        public void ntrcreateps()
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            PropertySetManager.UpdatePropertySetDefinition(localDb, PSetDefs.DefinedSets.NtrData);

            using var tx = localDb.TransactionManager.StartTransaction();

            try
            {
                var ents = localDb.GetFjvEntities(tx);

                #region ------------- FILTER -------------
                var acceptedSystems = new HashSet<PipeSystemEnum>() { PipeSystemEnum.Stål };

                bool isAccepted(Entity ent)
                {
#if DEBUG
                    //prdDbg("DEBUG filtering active! Polylines only.");
                    //if (ent is BlockReference) return false;
#endif

                    switch (ent)
                    {
                        case Polyline _:
                            return acceptedSystems.Contains(
                                PipeScheduleV2.GetPipeSystem(ent));
                        case BlockReference br:
                            return acceptedSystems.Contains(
                                br.GetPipeSystemEnum());
                        default:
                            return false;
                    }
                }

                ents = ents.Where(x => isAccepted(x)).ToHashSet();
                #endregion                

                #region ------------- CAD ➜ Port topology -------------
                var polylines = ents.OfType<Polyline>().ToList();
                var fittings = ents.OfType<BlockReference>().ToList();
                var topo = TopologyBuilder.Build(polylines, fittings);
                #endregion                

                foreach (var element in topo.Elements)
                {
                    element.AttachPropertySet();
                }                
            }
            catch (DebugPointException dbex)
            {
                prdDbg(dbex);                

                tx.Abort();

                using var dtx = localDb.TransactionManager.StartTransaction();
                foreach (var p in dbex.PointsToMark)
                {
                    DebugHelper.CreateDebugLine(p, ColorByName("red"));
                }
                dtx.Commit();
                return;
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);                

                tx.Abort();
                return;
            }
            tx.Commit();
        }

        private sealed class RefEq<T> : IEqualityComparer<T> where T : class
        {
            public bool Equals(T? x, T? y) => ReferenceEquals(x, y);
            public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}