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
using IntersectUtilities.UtilsCommon.Graphs;

using NTRExport.CadExtraction;
using NTRExport.Interfaces;
using NTRExport.Ntr;
using NTRExport.NtrConfiguration;
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

        //[CommandMethod("NTREXPORTV2")]
        public void ntrexportv2()
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

        [CommandMethod("NTREXPORT")]
        public void ntrexport()
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;            

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

                #region ------------- CONFIG -------------
                const double CushionReach = 2.0;       // m
                var soilDefault = new SoilProfile("Soil_Default", 0.00);
                var soilC80 = new SoilProfile("Soil_C80", 0.08);
                #endregion

                #region ------------- CAD ➜ Port topology -------------
                var cad = new CadModel();
                foreach (var e in ents)
                {
                    if (e is Polyline pl) 
                        cad.Pipes.Add(PolylineAdapterFactory.Create(pl));
                    else if (e is BlockReference br)
                        cad.Fittings.Add(BlockRefAdapterFactory.Create(br));
                }
                var topo = new TopologyBuilder(cad).Build();
                #endregion

                #region ------------- Topology-level soil planning -------------
                new TopologySoilPlanner(topo, 2.0, soilDefault, soilC80).Apply();
                #endregion

                #region ------------- Read NTR configuration from Excel -------------
                var conf = new ConfigurationData();
                //foreach (var l in conf.Last) prdDbg(l);
                #endregion

                #region ------------- Initialize NTR coordinate normalization -------------
                NtrCoord.InitFromTopology(topo, marginMeters: 0.0);
                #endregion

                #region ------------- Topology ➜ NTR skeleton -------------
                var ntr = new NtrMapper().Map(topo);
                #endregion

                #region ------------- Emit NTR -------------
                var writer = new NtrWriter(new Rohr2SoilAdapter());
                // Build IS and DN records across all distinct pipe groups found in the drawing
                var headerLines = new List<string>();
                var headerDedup = new HashSet<string>();
                var plines = ents.OfType<Polyline>().ToList();
                var groups = plines.GroupBy(pl => (
                    sys: PipeScheduleV2.GetPipeSystem(pl),
                    typ: PipeScheduleV2.GetPipeType(pl, true),
                    ser: PipeScheduleV2.GetPipeSeriesV2(pl),
                    twin: PipeScheduleV2.GetPipeType(pl, true) == PipeTypeEnum.Twin
                ));

                foreach (var ggrp in groups)
                {
                    var dnsInGroup = ggrp.Select(PipeScheduleV2.GetPipeDN)
                        .Where(d => d > 0)
                        .Distinct()
                        .ToList();
                    if (dnsInGroup.Count == 0) continue;

                    var catalog = new NtrDnCatalog(ggrp.Key.sys, ggrp.Key.typ, ggrp.Key.ser, ggrp.Key.twin);
                    foreach (var line in catalog.BuildRecords(dnsInGroup))
                    {
                        if (headerDedup.Add(line)) headerLines.Add(line);
                    }
                }

                var ntrText = writer.Build(ntr, headerLines, conf);

                // Save next to DWG
                var dwgPath = localDb.Filename;
                var outPath = System.IO.Path.ChangeExtension(
                    string.IsNullOrEmpty(dwgPath) ? "export" : dwgPath, ".ntr");
                System.IO.File.WriteAllText(outPath, ntrText, System.Text.Encoding.UTF8);
                prdDbg($"NTR written: {outPath}");
                #endregion
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