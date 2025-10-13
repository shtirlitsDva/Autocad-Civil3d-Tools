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
using IntersectUtilities.UtilsCommon.Graphs;

using NTRExport.CadExtraction;
using NTRExport.Geometry;
using NTRExport.Interfaces;
using NTRExport.Ntr;
using NTRExport.SoilModel;
using NTRExport.TopologyModel;

using static IntersectUtilities.UtilsCommon.Utils;

using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;

[assembly: CommandClass(typeof(NTRExport.Commands))]

namespace NTRExport
{
    public class Commands : IExtensionApplication
    {
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

                #region Basic implementation of rotation
                //Read any NtrData from fremtidig
                var ntrPsm = new PropertySetManager(
                    fDb, PSetDefs.DefinedSets.NtrData);
                var ntrDef = new PSetDefs.NtrData();
                
                Dictionary<Entity, double> rotationDict = new();

                foreach (var ent in ents)
                {
                    var rotation = ntrPsm.ReadPropertyDouble(
                        ent, ntrDef.ElementRotation);
                    if (rotation == 0) continue;
                    rotationDict[ent] = rotation;
                }
                #endregion

                PipelineNetwork pn = new PipelineNetwork();
                pn.CreatePipelineNetwork(ents, als);
                pn.CreatePipelineGraph();
                pn.CreateSizeArrays();
                pn.CreateSegmentGraphs();

                var ntrgraphs = new List<Graph<INtrSegment>>();

                if (pn.PipelineGraphs == null) return;

                foreach (var pgraph in pn.PipelineGraphs)
                {
                    var sgraph = pgraph.Root.Value.SegmentsGraph;
                    if (sgraph == null)
                    {
                        prdDbg($"WARNING: Segments graph for " +
                        $"{pgraph.Root.Value.Name} is null!"); continue;
                    }

                    Node<INtrSegment> TranslateGraph(
                        Node<IPipelineSegmentV2> proot,
                        Dictionary<Entity, double> rdict)
                    {
                        INtrSegment ntrSegment;
                        switch (proot.Value)
                        {
                            case PipelineSegmentV2 pseg:
                                switch (pseg.Size.Type)
                                {
                                    case IntersectUtilities.UtilsCommon.Enums.PipeTypeEnum.Twin:
                                        ntrSegment = new NtrSegmentTwin(pseg, rdict);
                                        break;
                                    case IntersectUtilities.UtilsCommon.Enums.PipeTypeEnum.Frem:
                                    case IntersectUtilities.UtilsCommon.Enums.PipeTypeEnum.Retur:
                                    case IntersectUtilities.UtilsCommon.Enums.PipeTypeEnum.Enkelt:
                                        ntrSegment = new NtrSegmentEnkelt(pseg, rdict);
                                        break;
                                    case IntersectUtilities.UtilsCommon.Enums.PipeTypeEnum.Ukendt:
                                    default:
                                        throw new NotImplementedException();
                                }
                                break;
                            case PipelineTransitionV2 tseg:
                                ntrSegment = new NtrSegmentTransition(tseg, rdict);
                                break;
                            default:
                                throw new System.Exception(
                                    $"ERR8736: Encountered unknown type: " +
                                    $"{proot.Value.GetType()}");
                        }

                        var node = new Node<INtrSegment>(ntrSegment);

                        foreach (var child in proot.Children)
                        {
                            var cnode = TranslateGraph(child, rdict);
                            cnode.Parent = node;
                            node.AddChild(cnode);
                        }

                        return node;
                    }
                    var root = TranslateGraph(sgraph.Root, rotationDict);
                    var ntrgraph = new Graph<INtrSegment>(
                        root,
                        ntr => $"{ntr.PipelineSegment.Owner.Name}-{ntr.PipelineSegment.MidStation}",
                        ntr => $"{ntr.PipelineSegment.Label}"
                        );
                    ntrgraphs.Add(ntrgraph);
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

        [CommandMethod("NTREXPORTV2")]
        public void ntrexportv2()
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            DataManager dm = new(new DataReferencesOptions());

            using var fDb = dm.Fremtid();
            using var fTx = fDb.TransactionManager.StartTransaction();

            using var tx = localDb.TransactionManager.StartTransaction();            

            try
            {
                var ents = fDb.GetFjvEntities(fTx);

                // ------------- CONFIG -------------
                const double CushionReach = 2.0;       // m
                var soilDefault = new SoilProfile("Soil_Default", 0.00);
                var soilC80 = new SoilProfile("Soil_C80", 0.08);

                // ------------- CAD ➜ Port topology -------------
                var cad = new CadModel();
                foreach (var e in ents)
                {
                    if (e is Polyline pl) cad.Pipes.Add(PolylineAdapterFactory.Create(pl));
                    else if (e is BlockReference br) cad.Fittings.Add(BlockRefAdapterFactory.Create(br));
                }
                var topo = new TopologyBuilder(cad).Build();

                // ------------- Topology ➜ NTR skeleton (coords only, no explicit nodes) -------------
                var ntr = new NtrMapper().Map(topo);

                // ------------- Build anchors at every fitting joint (soil applies to all, geometry-driven) -------------
                static string Key(Pt2 p) => $"{Math.Round(p.X, 3):F3},{Math.Round(p.Y, 3):F3}";
                var anchors = new HashSet<string>();
                void AddAnchor(Pt2 p) => anchors.Add(Key(p));

                foreach (var m in ntr.Members)
                {
                    switch (m)
                    {
                        case NtrBend b: AddAnchor(b.A); AddAnchor(b.B); break;
                        case NtrTee t: AddAnchor(t.Ph1); AddAnchor(t.Ph2); AddAnchor(t.Pa1); AddAnchor(t.Pa2); break;
                        case NtrReducer r:
                            // If reducers are modeled as fittings in your data, add their ends here when you add NtrReducer mapping.
                            break;
                        default: break; // pipes are not anchors by themselves
                    }
                }

                // ------------- Pipe adjacency by joint coordinate -------------
                var pipes = ntr.Members.OfType<NtrPipe>().ToList();
                var adj = new Dictionary<string, List<(NtrPipe pipe, bool isA)>>();
                void AddAdj(Pt2 p, NtrPipe pipe, bool isA)
                {
                    var k = Key(p);
                    if (!adj.TryGetValue(k, out var list)) adj[k] = list = new();
                    list.Add((pipe, isA));
                }
                foreach (var p in pipes) { AddAdj(p.A, p, true); AddAdj(p.B, p, false); }

                static bool Equal(Pt2 a, Pt2 b, double tol) => Math.Abs(a.X - b.X) <= tol && Math.Abs(a.Y - b.Y) <= tol;
                static (double x, double y) DirFrom(Pt2 from, Pt2 to)
                {
                    var dx = to.X - from.X; var dy = to.Y - from.Y; var L = Math.Sqrt(dx * dx + dy * dy);
                    return L < 1e-9 ? (0, 0) : (dx / L, dy / L);
                }

                // Pick continuation pipe at a joint: maximize colinearity with current direction.
                (NtrPipe next, bool nextFromA)? NextPipe(NtrPipe cur, bool atA, Dictionary<string, List<(NtrPipe, bool)>> index)
                {
                    var joint = atA ? cur.A : cur.B;
                    var curDir = atA ? DirFrom(cur.A, cur.B) : DirFrom(cur.B, cur.A);
                    var key = Key(joint);
                    if (!index.TryGetValue(key, out var lst)) return null;

                    NtrPipe? best = null; bool bestFromA = true; double bestDot = -2;
                    foreach (var (p, isA) in lst)
                    {
                        if (p == cur) continue;
                        var dir = isA ? DirFrom(p.A, p.B) : DirFrom(p.B, p.A);
                        var dot = curDir.x * dir.x + curDir.y * dir.y;
                        if (dot > bestDot) { bestDot = dot; best = p; bestFromA = isA; }
                    }
                    return best is null ? null : (best, bestFromA);
                }

                // ------------- Compute cushion zones from anchors and split only pipes -------------
                var cutMarks = new Dictionary<NtrPipe, SortedSet<double>>();
                var cushionSpans = new Dictionary<NtrPipe, List<(double s0, double s1)>>();

                void Mark(NtrPipe p, double s)
                {
                    if (!cutMarks.TryGetValue(p, out var set)) cutMarks[p] = set = new() { 0.0, p.Length };
                    set.Add(Math.Clamp(s, 0, p.Length));
                }
                void Span(NtrPipe p, double a, double b)
                {
                    if (!cushionSpans.TryGetValue(p, out var list)) cushionSpans[p] = list = new();
                    var s0 = Math.Max(0, Math.Min(a, b)); var s1 = Math.Min(p.Length, Math.Max(a, b));
                    if (s1 - s0 > 1e-6) list.Add((s0, s1));
                }

                // Traverse away from an anchor for CushionReach, skipping fittings, splitting only pipes.
                foreach (var anchorKey in anchors)
                {
                    if (!adj.TryGetValue(anchorKey, out var incident)) continue;

                    foreach (var (startPipe, startFromA) in incident)
                    {
                        var rem = CushionReach;
                        var p = startPipe;
                        var fromA = startFromA;
                        var s0 = fromA ? 0.0 : p.Length;

                        while (true)
                        {
                            var segRem = fromA ? p.Length - s0 : s0;
                            if (rem <= segRem + CadExtraction.Tolerance.Tol)
                            {
                                var s1 = fromA ? (s0 + rem) : (s0 - rem);
                                Span(p, s0, s1);
                                Mark(p, s1);
                                break;
                            }
                            else
                            {
                                // consume whole remainder of this pipe and continue
                                var sEnd = fromA ? p.Length : 0.0;
                                Span(p, s0, sEnd);
                                Mark(p, sEnd);
                                rem -= segRem;

                                var nxt = NextPipe(p, !fromA, adj); // move out of the far joint
                                if (nxt is null) break;
                                p = nxt.Value.next; fromA = nxt.Value.nextFromA; s0 = fromA ? 0.0 : p.Length;
                                if (p.Length < 1e-8) break;
                            }
                        }
                    }
                }

                // ------------- Replace each affected pipe with split children and assign soils -------------
                Pt2 Lerp(Pt2 a, Pt2 b, double t) => new(a.X + t * (b.X - a.X), a.Y + t * (b.Y - a.Y));
                Pt2 PointAt(NtrPipe p, double s) => Lerp(p.A, p.B, p.Length < 1e-9 ? 0.0 : s / p.Length);

                bool Covered(List<(double s0, double s1)> spans, double a, double b)
                {
                    var mid = 0.5 * (a + b);
                    return spans.Any(z => mid >= z.s0 - 1e-9 && mid <= z.s1 + 1e-9);
                }

                foreach (var (pipe, cuts) in cutMarks)
                {
                    var cutList = cuts.ToList();
                    var idx = ntr.Members.IndexOf(pipe);
                    ntr.Members.RemoveAt(idx);

                    var spans = cushionSpans.TryGetValue(pipe, out var lst) ? lst : new();

                    for (int i = 0; i < cutList.Count - 1; i++)
                    {
                        var a = cutList[i];
                        var b = cutList[i + 1];
                        if (b - a < 1e-6) continue;

                        var pa = PointAt(pipe, a);
                        var pb = PointAt(pipe, b);
                        var child = pipe.With(pa, pb, Covered(spans, a, b) ? soilC80 : soilDefault);
                        ntr.Members.Insert(idx++, child);
                    }
                }

                // ------------- Emit NTR -------------
                var writer = new NtrWriter(new Rohr2SoilAdapter());
                var ntrText = writer.Build(ntr);

                // Save next to DWG
                var dwgPath = localDb.Filename;
                var outPath = System.IO.Path.ChangeExtension(
                    string.IsNullOrEmpty(dwgPath) ? "export" : dwgPath, ".ntr");
                System.IO.File.WriteAllText(outPath, ntrText, System.Text.Encoding.Latin1);
                prdDbg($"NTR written: {outPath}");
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
    }
}