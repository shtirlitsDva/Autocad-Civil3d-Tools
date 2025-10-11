using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.DatabaseServices;

using IntersectUtilities.PipelineNetworkSystem;
using IntersectUtilities.PipelineNetworkSystem.PipelineSizeArray;
using IntersectUtilities.PipeScheduleV2;
using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.DataManager;
using IntersectUtilities.UtilsCommon.Graphs;

using NTRExport.Interfaces;
using NTRExport.Topology;

using static IntersectUtilities.UtilsCommon.Utils;

using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

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

                    Node<INtrSegment> TranslateGraph(Node<IPipelineSegmentV2> proot)
                    {
                        INtrSegment ntrSegment;
                        switch (proot.Value)
                        {
                            case PipelineSegmentV2 pseg:
                                switch (pseg.Size.Type)
                                {
                                    case IntersectUtilities.UtilsCommon.Enums.PipeTypeEnum.Twin:
                                        ntrSegment = new NtrSegmentTwin(pseg);
                                        break;
                                    case IntersectUtilities.UtilsCommon.Enums.PipeTypeEnum.Frem:
                                    case IntersectUtilities.UtilsCommon.Enums.PipeTypeEnum.Retur:
                                    case IntersectUtilities.UtilsCommon.Enums.PipeTypeEnum.Enkelt:
                                        ntrSegment = new NtrSegmentEnkelt(pseg);
                                        break;
                                    case IntersectUtilities.UtilsCommon.Enums.PipeTypeEnum.Ukendt:
                                    default:
                                        throw new NotImplementedException();
                                }
                                break;
                            case PipelineTransitionV2 tseg:
                                ntrSegment = new NtrSegmentTransition(tseg);
                                break;
                            default:
                                throw new System.Exception(
                                    $"ERR8736: Encountered unknown type: " +
                                    $"{proot.Value.GetType()}");
                        }

                        var node = new Node<INtrSegment>(ntrSegment);

                        foreach (var child in proot.Children)
                        {
                            var cnode = TranslateGraph(child);
                            cnode.Parent = node;
                            node.AddChild(cnode);
                        }

                        return node;
                    }
                    var root = TranslateGraph(sgraph.Root);
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
    }
}