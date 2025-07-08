using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;
using Autodesk.Civil.DataShortcuts;
using Autodesk.Aec.PropertyData;
using Autodesk.Aec.PropertyData.DatabaseServices;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Data;
using MoreLinq;
using GroupByCluster;
using IntersectUtilities.UtilsCommon;
using Dreambuild.AutoCAD;

using static IntersectUtilities.Enums;
using static IntersectUtilities.HelperMethods;
using static IntersectUtilities.Utils;
using static IntersectUtilities.UtilsCommon.Utils;
using static IntersectUtilities.PipeScheduleV2.PipeScheduleV2;

using static IntersectUtilities.UtilsCommon.UtilsDataTables;
using static IntersectUtilities.UtilsCommon.UtilsODData;

using BlockReference = Autodesk.AutoCAD.DatabaseServices.BlockReference;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using DataType = Autodesk.Gis.Map.Constants.DataType;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using ObjectIdCollection = Autodesk.AutoCAD.DatabaseServices.ObjectIdCollection;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Label = Autodesk.Civil.DatabaseServices.Label;
using DBObject = Autodesk.AutoCAD.DatabaseServices.DBObject;
using IntersectUtilities.PipelineNetworkSystem;
using IntersectUtilities.PipelineNetworkSystem.PipelineSizeArray;

namespace IntersectUtilities
{
    public partial class Intersect
    {
        /// <command>GRAPHPIPELINES</command>
        /// <summary>
        /// Creates a pipeline network and writes it to a graph.
        /// Used for validating pipeline network.
        /// All alignments are expected to be connected following the pipeline network.
        /// </summary>
        /// <category>Quality Assurance</category>
        [CommandMethod("GRAPHPIPELINES")]
        public void graphpipelines()
        {
            prdDbg("Dette skal køres i FJV Fremtid!");

            graphclear();
            graphpopulate();

            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            DataManagement.DataManager dm = new DataManagement.DataManager(new DataReferencesOptions());
            Database alDb = dm.GetForRead("Alignments");
            Transaction alTx = alDb.TransactionManager.StartTransaction();

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    var ents = localDb.GetFjvEntities(tx, false, false);
                    var als = alDb.HashSetOfType<Alignment>(alTx);

                    PipelineNetwork pn = new PipelineNetwork();
                    prdDbg("Creating pipeline network...");
                    System.Windows.Forms.Application.DoEvents();
                    pn.CreatePipelineNetwork(ents, als);
                    prdDbg("Creating pipeline graph...");
                    System.Windows.Forms.Application.DoEvents();
                    pn.CreatePipelineGraph();
                    prdDbg("Writing pipeline graph to dot...");
                    System.Windows.Forms.Application.DoEvents();
                    pn.PipelineGraphsToDot();
                    prdDbg("Finshed!");
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                    return;
                }
                finally
                {
                    alTx.Abort();
                    alTx.Dispose();
                    alDb.Dispose();
                }
                tx.Commit();
            }
        }

        /// <command>AUTOREVERSPLINESV2</command>
        /// <summary>
        /// Automatically reverses the direction of polylines in the pipeline network.
        /// The polyline direction follows the flow direction in the supply pipe.
        /// GRAPHPIPELINES must be run before this command and produce meaningful result.
        /// </summary>
        /// <category>Fjernvarme Fremtidig</category>
        [CommandMethod("AUTOREVERSPLINESV2")]
        public void autoreverseplinesv2()
        {
            prdDbg("Dette skal køres i FJV Fremtid!");

            graphclear();
            graphpopulate();

            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            DataManagement.DataManager dm = new DataManagement.DataManager(new DataReferencesOptions());
            Database alDb = dm.GetForRead("Alignments");
            Transaction alTx = alDb.TransactionManager.StartTransaction();

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    var ents = localDb.GetFjvEntities(tx, false, false);
                    var als = alDb.HashSetOfType<Alignment>(alTx);

                    PipelineNetwork pn = new PipelineNetwork();
                    pn.CreatePipelineNetwork(ents, als);
                    pn.CreatePipelineGraph();
                    pn.AutoReversePolylines();
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                    return;
                }
                finally
                {
                    alTx.Abort();
                    alTx.Dispose();
                    alDb.Dispose();
                }
                tx.Commit();
            }
        }

        /// <command>CORRECTCUTLENGTHSV2</command>
        /// <summary>
        /// Automatically moves reducers so that polyline lengths are divisible by standard pipe lengths without rest.
        /// The direction of the correction follows the supply pipe flow direction.
        /// GRAPHPIPELINES and AUTOREVERSPLINESV2 must be run before this command and produce meaningful results.
        /// </summary>
        /// <category>Fjernvarme Fremtidig</category>
        [CommandMethod("CORRECTCUTLENGTHSV2")]
        public void correctcutlengthsv2()
        {
            prdDbg("Dette skal køres i FJV Fremtid!");

            graphclear();
            graphpopulate();

            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            DataManagement.DataManager dm = new DataManagement.DataManager(new DataReferencesOptions());
            Database alDb = dm.GetForRead("Alignments");
            Transaction alTx = alDb.TransactionManager.StartTransaction();

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    var ents = localDb.GetFjvEntities(tx, false, false);
                    var als = alDb.HashSetOfType<Alignment>(alTx);

                    PipelineNetwork pn = new PipelineNetwork();
                    pn.CreatePipelineNetwork(ents, als);
                    pn.CreatePipelineGraph();
                    //pn.AutoReversePolylines();
                    pn.AutoCorrectLengths();
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                    return;
                }
                finally
                {
                    alTx.Abort();
                    alTx.Dispose();
                    alDb.Dispose();
                }
                tx.Commit();
            }

            cleanplines();
            cleanplines();
            cleanplines();
        }

        /// <command>CWDV2, CREATEWELDPOINTSV2</command>
        /// <summary>
        /// Creates weld points in fjernvarme fremtidig.
        /// GRAPHPIPELINES, AUTOREVERSPLINESV2 and CORRECTCUTLENGTHSV2 must be run before this command and produce meaningful results.
        /// </summary>
        /// <category>Fjernvarme Fremtidig</category>
        [CommandMethod("CWDV2")]
        [CommandMethod("CREATEWELDPOINTSV2")]
        public void createweldpointsv2()
        {
            prdDbg("Dette skal køres i FJV Fremtid!");

            graphclear();
            graphpopulate();

            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            DataManagement.DataManager dm = new DataManagement.DataManager(new DataReferencesOptions());
            Database alDb = dm.GetForRead("Alignments");
            Transaction alTx = alDb.TransactionManager.StartTransaction();

            //List to gather ALL weld points
            var wps = new List<WeldPointData2>();

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    var ents = localDb.GetFjvEntities(tx, false, false);
                    var als = alDb.HashSetOfType<Alignment>(alTx);

                    PipelineNetwork pn = new PipelineNetwork();
                    pn.CreatePipelineNetwork(ents, als);
                    pn.CreatePipelineGraph();
                    pn.CreateSizeArrays();

                    pn.GatherWeldPoints(wps);
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                    return;
                }
                finally
                {
                    alTx.Abort();
                    alTx.Dispose();
                    alDb.Dispose();
                }
                tx.Commit();
            }

            var gw = new PipelineGraphWorker();
            gw.CreateWeldBlocks(wps);
        }
#if DEBUG
        [CommandMethod("TPSA")]
        public void testpipelinesizearray()
        {
            prdDbg("Dette skal køres i FJV Fremtid!");

            graphclear();
            graphpopulate();

            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            DataManagement.DataManager dm = new DataManagement.DataManager(new DataReferencesOptions());
            Database alDb = dm.GetForRead("Alignments");
            Transaction alTx = alDb.TransactionManager.StartTransaction();

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    var ents = localDb.GetFjvEntities(tx, false, false);
                    var als = alDb.HashSetOfType<Alignment>(alTx);

                    PipelineNetwork pn = new PipelineNetwork();
                    pn.CreatePipelineNetwork(ents, als);
                    pn.CreatePipelineGraph();
                    pn.PrintSizeArrays();
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                    return;
                }
                finally
                {
                    alTx.Abort();
                    alTx.Dispose();
                    alDb.Dispose();
                }
                tx.Abort();
            }
        }

        [CommandMethod("TSPSA")]
        public void testsinglepipelinesizearray()
        {
            prdDbg("Dette skal køres i FJV Fremtid!");

            graphclear();
            graphpopulate();

            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;            

            DataManagement.DataManager dm = new DataManagement.DataManager(new DataReferencesOptions());
            using Database alDb = dm.GetForRead("Alignments");
            using Transaction alTx = alDb.TransactionManager.StartTransaction();

            using Transaction tx = localDb.TransactionManager.StartTransaction();
            PropertySetHelper pshFjv = new(localDb);

            try
            {
                var allEnts = localDb.GetFjvEntities(tx, false, false);
                var als = alDb.HashSetOfType<Alignment>(alTx);

                //Choose what pipeline to print
                var alNames = allEnts
                    .Select(pshFjv.Pipeline.BelongsToAlignment)
                    .Distinct()
                    .Where(x => als.Any(al => al.Name == x))
                    .Order();

                var choice = StringGridFormCaller.Call(alNames, "Choose pipeline to build size array: ");
                if (choice.IsNoE()) 
                {
                    tx.Abort();
                    return;
                }

                var al = als.First(x => x.Name == choice);
                var ents = allEnts.Where(x => pshFjv.Pipeline.BelongsToAlignment(x) == choice);

                IPipelineV2 pipeline = PipelineV2Factory.Create(ents, al);
                if (pipeline == null)
                    throw new System.Exception($"Pipeline building failed!");
                var sa = PipelineSizeArrayFactory.CreateSizeArray(pipeline);
                if (sa == null)
                    throw new System.Exception($"SizeArray building failed!");
                prdDbg(sa.ToString());
            }
            catch (System.Exception ex)
            {
                tx.Abort();
                prdDbg(ex);
                return;
            }
            finally
            {
                alTx.Abort();
                dm.Dispose();
            }
            tx.Abort();

        }

        [CommandMethod("RPDIRS")]
        public void randomizepolylinedirs()
        {
            prdDbg("Dette skal køres i FJV Fremtid!");

            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            Random rnd = new Random();

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    var ents = localDb.HashSetOfType<Polyline>(tx);
                    foreach (var item in ents)
                        if (rnd.Next(0, 2) == 0) { item.UpgradeOpen(); item.ReverseCurve(); }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                    return;
                }
                finally
                {
                }
                tx.Commit();
            }
        }
#endif
    }
}