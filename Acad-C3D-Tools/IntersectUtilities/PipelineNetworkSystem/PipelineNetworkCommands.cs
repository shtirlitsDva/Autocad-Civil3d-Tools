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

namespace IntersectUtilities
{
    public partial class Intersect
    {
        [CommandMethod("GRAPHPIPELINES")]
        public void graphpipelines()
        {
            prdDbg("Dette skal køres i FJV Fremtid!");

            graphclear();
            graphpopulate();

            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            DataManager.DataManager dm = new DataManager.DataManager(new DataReferencesOptions());
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
                    pn.PipelineGraphsToDot();
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

        [CommandMethod("AUTOREVERSPLINESV2")]
        public void autoreverseplinesv2()
        {
            prdDbg("Dette skal køres i FJV Fremtid!");

            graphclear();
            graphpopulate();

            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            DataManager.DataManager dm = new DataManager.DataManager(new DataReferencesOptions());
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

        [CommandMethod("CORRECTCUTLENGTHSV2")]
        public void correctcutlengthsv2()
        {
            prdDbg("Dette skal køres i FJV Fremtid!");

            graphclear();
            graphpopulate();

            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            DataManager.DataManager dm = new DataManager.DataManager(new DataReferencesOptions());
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

        [CommandMethod("CWDV2")]
        [CommandMethod("CREATEWELDPOINTSV2")]
        public void createweldpointsv2()
        {
            prdDbg("Dette skal køres i FJV Fremtid!");

            graphclear();
            graphpopulate();

            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            DataManager.DataManager dm = new DataManager.DataManager(new DataReferencesOptions());
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

                    pn.CreateWeldPoints();
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
#if DEBUG
        [CommandMethod("TPSA")]
        public void testpipelinesizearray()
        {
            prdDbg("Dette skal køres i FJV Fremtid!");

            graphclear();
            graphpopulate();

            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            DataManager.DataManager dm = new DataManager.DataManager(new DataReferencesOptions());
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
                    pn.CreateSizeArraysAndPrint();
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