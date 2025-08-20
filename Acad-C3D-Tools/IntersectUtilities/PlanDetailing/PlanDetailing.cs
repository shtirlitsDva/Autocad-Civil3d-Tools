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
using Autodesk.Gis.Map;
using Autodesk.Gis.Map.ObjectData;
using Autodesk.Gis.Map.Utilities;
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
using static IntersectUtilities.UtilsCommon.Utils;
using Dreambuild.AutoCAD;
using IntersectUtilities.PlanDetailing.Components;

using static IntersectUtilities.Enums;
using static IntersectUtilities.HelperMethods;
using static IntersectUtilities.Utils;
using static IntersectUtilities.PipeScheduleV2.PipeScheduleV2;
using static IntersectUtilities.ComponentSchedule;

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
using IntersectUtilities.UtilsCommon.Enums;

namespace IntersectUtilities
{
    public partial class Intersect
    {
        /// <command>DELETEWELDPOINTS, DWP</command>
        /// <summary>
        /// Deletes all weld points in the drawing.
        /// </summary>
        /// <category>Fjernvarme Fremtidig</category>
        [CommandMethod("DELETEWELDPOINTS")]
        [CommandMethod("DWP")]
        public void deleteweldpoints()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                string blockLayerName = "0-SVEJSEPKT";
                string blockName = "SVEJSEPUNKT";
                string textLayerName = "0-DEBUG-TXT";
                //////////////////////////////////////

                #region Delete previous blocks
                //Delete previous blocks
                var existingBlocks = localDb.GetBlockReferenceByName(blockName);
                foreach (BlockReference br in existingBlocks)
                {
                    br.CheckOrOpenForWrite();
                    br.Erase(true);
                }
                //Delete previous blocks
                existingBlocks = localDb.GetBlockReferenceByName(blockName + "-NOTXT");
                foreach (BlockReference br in existingBlocks)
                {
                    br.CheckOrOpenForWrite();
                    br.Erase(true);
                }
                //Delete previous blocks
                existingBlocks = localDb.GetBlockReferenceByName(blockName + "-V2");
                foreach (BlockReference br in existingBlocks)
                {
                    br.CheckOrOpenForWrite();
                    br.Erase(true);
                }
                #endregion
                tx.Commit();
            }            
        }

        /// <command>FIXPLINEGLOBALWIDTH</command>
        /// <summary>
        /// Fixes the global width of polylines.
        /// It actually takes the start width of the polyline and sets it as the global width.
        /// </summary>
        /// <category>Fjernvarme Fremtidig</category>
        [CommandMethod("FIXPLINEGLOBALWIDTH")]
        public void fixplineglobalwidth()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    var pls = localDb.HashSetOfType<Polyline>(tx);

                    foreach (Polyline pl in pls)
                    {
                        double constWidth;
                        try
                        {
                            constWidth = pl.ConstantWidth;
                        }
                        catch (System.Exception)
                        {
                            prdDbg($"Pline {pl.Handle} needs to fix ConstantWidth!");
                            pl.CheckOrOpenForWrite();
                            pl.ConstantWidth = pl.GetStartWidthAt(0);
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                    return;
                }

                tx.Commit();
            }
        }

        /// <command>PLACEELBOW, PE</command>
        /// <summary>
        /// Places a preinsulated elbow at the specified location.
        /// </summary>
        /// <category>Blocks</category>
        [CommandMethod("PLACEELBOW")]
        [CommandMethod("PE")]
        public void placeelbow()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;

            while (true)
            {
                try
                {
                    #region Get pipes
                    HashSet<Oid> plOids = localDb.HashSetOfFjvPipeIds(true);
                    if (plOids.Count == 0)
                    {
                        prdDbg("No DH pipes in drawing!");
                        return;
                    }
                    #endregion

                    #region Ask for point
                    //message for the ask for point prompt
                    string message = "Select location to place pipe fitting: ";
                    var opt = new PromptPointOptions(message);

                    Point3d location = Algorithms.NullPoint3d;
                    do
                    {
                        var res = ed.GetPoint(opt);
                        if (res.Status == PromptStatus.Cancel)
                        {
                            bringallblockstofront();
                            return;
                        }
                        if (res.Status == PromptStatus.OK) location = res.Value;
                    }
                    while (location.IsNull());
                    #endregion

                    #region Find nearest pline
                    Oid nearestPlId;
                    using (Transaction tx = localDb.TransactionManager.StartTransaction())
                    {
                        Polyline pl = plOids.Select(x => x.Go<Polyline>(tx))
                            .MinByEnumerable(x => location.DistanceHorizontalTo(
                                x.GetClosestPointTo(location, false)))
                            .FirstOrDefault();
                        nearestPlId = pl.Id;
                        tx.Commit();
                    }

                    if (nearestPlId == default)
                    {
                        prdDbg("Nearest pipe cannot be found!");
                        return;
                    }
                    #endregion

                    #region Place preinsulated elbow
                    ElbowPreinsulated elbow = new ElbowPreinsulated(localDb, nearestPlId, location);
                    Result result = elbow.Validate();
                    if (result.Status != ResultStatus.OK)
                    {
                        prdDbg(result.ErrorMsg);
                        continue;
                    }
                    result = elbow.Place();
                    if (result.Status != ResultStatus.OK)
                    {
                        prdDbg(result.ErrorMsg);
                        continue;
                    }
                    #endregion
                }
                catch (System.Exception ex)
                {
                    prdDbg(ex);
                    AbortGracefully(localDb);
                    return;
                }
            }
        }

        /// <command>PLACEKEDELRØRSBØJNING, PK</command>
        /// <summary>
        /// Places a kedelrørsbøjning at the specified location.
        /// </summary>
        /// <category>Blocks</category>
        [CommandMethod("PLACEKEDELRØRSBØJNING")]
        [CommandMethod("PK")]
        public void placekedelrørsbøjning()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;

            while (true)
            {
                try
                {
                    #region Get pipes
                    HashSet<Oid> plOids = localDb.HashSetOfFjvPipeIds(true);
                    if (plOids.Count == 0)
                    {
                        prdDbg("No DH pipes in drawing!");
                        return;
                    }
                    #endregion

                    #region Ask for point
                    //message for the ask for point prompt
                    string message = "Select location to place pipe fitting: ";
                    var opt = new PromptPointOptions(message);

                    Point3d location = Algorithms.NullPoint3d;
                    do
                    {
                        var res = ed.GetPoint(opt);
                        if (res.Status == PromptStatus.Cancel)
                        {
                            bringallblockstofront();
                            return;
                        }
                        if (res.Status == PromptStatus.OK) location = res.Value;
                    }
                    while (location.IsNull());
                    #endregion

                    #region Find nearest pline
                    Oid nearestPlId;
                    using (Transaction tx = localDb.TransactionManager.StartTransaction())
                    {
                        Polyline pl = plOids.Select(x => x.Go<Polyline>(tx))
                            .MinByEnumerable(x => location.DistanceHorizontalTo(
                                x.GetClosestPointTo(location, false)))
                            .FirstOrDefault();
                        nearestPlId = pl.Id;
                        tx.Commit();
                    }

                    if (nearestPlId == default)
                    {
                        prdDbg("Nearest pipe cannot be found!");
                        return;
                    }
                    #endregion

                    #region Place kedelrørsfitting
                    ElbowWeldFitting elbow = new ElbowWeldFitting(localDb, nearestPlId, location);
                    Result result = elbow.Validate();
                    if (result.Status != ResultStatus.OK)
                    {
                        prdDbg(result.ErrorMsg);
                        continue;
                    }
                    result = elbow.Place();
                    if (result.Status != ResultStatus.OK)
                    {
                        prdDbg(result.ErrorMsg);
                        continue;
                    }
                    #endregion
                }
                catch (System.Exception ex)
                {
                    prdDbg(ex);
                    AbortGracefully(localDb);
                    return;
                }
            }
        }

        /// <command>PLACETRANSITIONX1, PT1</command>
        /// <summary>
        /// Places a reducer fitting reducing one size at the specified location.
        /// </summary>
        /// <category>Blocks</category>
        [CommandMethod("PLACETRANSITIONX1")]
        [CommandMethod("PT1")]
        public void placetransitionx1()
        {
            placetransition(Transition.TransitionType.X1);
        }

        /// <command>PLACETRANSITIONX2, PT2</command>
        /// <summary>
        /// Places a reducer fitting reducing two sizes at the specified location.
        /// </summary>
        /// <category>Blocks</category>
        [CommandMethod("PLACETRANSITIONX2")]
        [CommandMethod("PT2")]
        public void placetransitionx2()
        {
            placetransition(Transition.TransitionType.X2);
        }
        private void placetransition(Transition.TransitionType transitionType)
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;

            while (true)
            {
                try
                {
                    #region Get pipes
                    HashSet<Oid> plOids = localDb.HashSetOfFjvPipeIds(true);
                    if (plOids.Count == 0)
                    {
                        prdDbg("No DH pipes in drawing!");
                        return;
                    }
                    #endregion

                    #region Ask for point
                    //message for the ask for point prompt
                    string message = "Select location to place pipe fitting: ";
                    var opt = new PromptPointOptions(message);

                    Point3d location = Algorithms.NullPoint3d;
                    do
                    {
                        var res = ed.GetPoint(opt);
                        if (res.Status == PromptStatus.Cancel)
                        {
                            bringallblockstofront();
                            return;
                        }
                        if (res.Status == PromptStatus.OK) location = res.Value;
                    }
                    while (location.IsNull());
                    #endregion

                    #region Find nearest pline
                    Oid nearestPlId;
                    using (Transaction tx = localDb.TransactionManager.StartTransaction())
                    {
                        Polyline pl = plOids.Select(x => x.Go<Polyline>(tx))
                            .MinByEnumerable(x => location.DistanceHorizontalTo(
                                x.GetClosestPointTo(location, false)))
                            .FirstOrDefault();
                        nearestPlId = pl.Id;
                        tx.Commit();
                    }

                    if (nearestPlId == default)
                    {
                        prdDbg("Nearest pipe cannot be found!");
                        return;
                    }
                    #endregion

                    #region Place transition
                    Transition transition = new Transition(
                        localDb, nearestPlId, location, transitionType);
                    Result result = transition.Validate();
                    if (result.Status != ResultStatus.OK)
                    {
                        prdDbg(result.ErrorMsg);
                        continue;
                    }
                    result = transition.Place();
                    if (result.Status != ResultStatus.OK)
                    {
                        prdDbg(result.ErrorMsg);
                        continue;
                    }
                    #endregion
                }
                catch (System.Exception ex)
                {
                    prdDbg(ex);
                    AbortGracefully(localDb);
                    return;
                }
            }
        }

        /// <command>PLACEBUEROR, PB</command>
        /// <summary>
        /// Places a bueror at the specified location.
        /// </summary>
        /// <category>Blocks</category>
        [CommandMethod("PLACEBUEROR")]
        [CommandMethod("PB")]
        public void placebueror()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;

            while (true)
            {
                try
                {
                    #region Get pipes
                    HashSet<Oid> plOids = localDb.HashSetOfFjvPipeIds(true);
                    if (plOids.Count == 0)
                    {
                        prdDbg("No DH pipes in drawing!");
                        return;
                    }
                    #endregion

                    #region Ask for point
                    //message for the ask for point prompt
                    string message = "Select location to place pipe fitting: ";
                    var opt = new PromptPointOptions(message);

                    Point3d location = Algorithms.NullPoint3d;
                    do
                    {
                        var res = ed.GetPoint(opt);
                        if (res.Status == PromptStatus.Cancel)
                        {
                            bringallblockstofront();
                            return;
                        }
                        if (res.Status == PromptStatus.OK) location = res.Value;
                    }
                    while (location.IsNull());
                    #endregion

                    #region Find nearest pline
                    Oid nearestPlId;
                    using (Transaction tx = localDb.TransactionManager.StartTransaction())
                    {
                        Polyline pl = plOids.Select(x => x.Go<Polyline>(tx))
                            .MinByEnumerable(x => location.DistanceHorizontalTo(
                                x.GetClosestPointTo(location, false)))
                            .FirstOrDefault();
                        nearestPlId = pl.Id;
                        tx.Commit();
                    }

                    if (nearestPlId == default)
                    {
                        prdDbg("Nearest pipe cannot be found!");
                        return;
                    }
                    #endregion

                    #region Place preinsulated elbow
                    Bueror bueror = new Bueror(localDb, nearestPlId, location);
                    Result result = bueror.Validate();
                    if (result.Status != ResultStatus.OK)
                    {
                        prdDbg(result.ErrorMsg);
                        continue;
                    }
                    result = bueror.Place();
                    if (result.Status != ResultStatus.OK)
                    {
                        prdDbg(result.ErrorMsg);
                        continue;
                    }
                    #endregion
                }
                catch (System.Exception ex)
                {
                    prdDbg(ex);
                    return;
                }
            }
        }

        //[CommandMethod("PLACEBRANCH")]
        //[CommandMethod("PA")]
        public void placebranch()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;

            while (true)
            {
                try
                {
                    #region Get pipes
                    HashSet<Oid> plOids = localDb.HashSetOfFjvPipeIds(true);
                    if (plOids.Count == 0)
                    {
                        prdDbg("No DH pipes in drawing!");
                        return;
                    }
                    #endregion

                    #region Ask for point
                    //message for the ask for point prompt
                    string message = "Select location to place pipe fitting: ";
                    var opt = new PromptPointOptions(message);

                    Point3d location = Algorithms.NullPoint3d;
                    do
                    {
                        var res = ed.GetPoint(opt);
                        if (res.Status == PromptStatus.Cancel)
                        {
                            bringallblockstofront();
                            return;
                        }
                        if (res.Status == PromptStatus.OK) location = res.Value;
                    }
                    while (location.IsNull());
                    #endregion

                    #region Place branch
                    Branch branch = new Branch(localDb, Oid.Null, location);
                    Result result = branch.Validate();
                    if (result.Status != ResultStatus.OK)
                    {
                        prdDbg(result.ErrorMsg);
                        continue;
                    }
                    result = branch.Place();
                    if (result.Status != ResultStatus.OK)
                    {
                        prdDbg(result.ErrorMsg);
                        continue;
                    }
                    #endregion
                }
                catch (System.Exception ex)
                {
                    prdDbg(ex);
                    AbortGracefully(localDb);
                    return;
                }
            }
        }

        /// <command>PLACEPERTTEE, PPT</command>
        /// <summary>
        /// Places a PERT tee at the specified location.
        /// </summary>
        /// <category>Blocks</category>
        [CommandMethod("PLACEPERTTEE")]
        [CommandMethod("PPT")]
        public void placeperttee()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;

            while (true)
            {
                try
                {
                    #region Get pipes
                    HashSet<Oid> plOids = localDb.HashSetOfFjvPipeIds(true);
                    if (plOids.Count == 0)
                    {
                        prdDbg("No DH pipes in drawing!");
                        return;
                    }
                    #endregion

                    #region Ask for point
                    //message for the ask for point prompt
                    string message = "Select location to place pipe fitting: ";
                    var opt = new PromptPointOptions(message);

                    Point3d location = Algorithms.NullPoint3d;
                    do
                    {
                        var res = ed.GetPoint(opt);
                        if (res.Status == PromptStatus.Cancel)
                        {
                            bringallblockstofront();
                            return;
                        }
                        if (res.Status == PromptStatus.OK) location = res.Value;
                    }
                    while (location.IsNull());
                    #endregion

                    #region Select nearest pline
                    Oid nearestPlId = Interaction.GetEntity("Select MAIN pipe: ", typeof(Polyline), true);
                    if (nearestPlId == Oid.Null)
                    {
                        prdDbg("MAIN pipe selection cancelled!");
                        return;
                    }
                    #endregion

                    #region Select stik
                    Oid stikPlId = Interaction.GetEntity("Select STIK pipe: ", typeof(Polyline), true);
                    if (nearestPlId == Oid.Null)
                    {
                        prdDbg("STIK pipe selection cancelled!");
                    }
                    #endregion

                    #region Place perttee
                    PertTee pertTee = new PertTee(
                        localDb, nearestPlId, stikPlId, location);
                    Result result = pertTee.Validate();
                    if (result.Status != ResultStatus.OK)
                    {
                        prdDbg(result.ErrorMsg);
                        continue;
                    }
                    result = pertTee.Place();
                    if (result.Status != ResultStatus.OK)
                    {
                        prdDbg(result.ErrorMsg);
                        continue;
                    }
                    #endregion
                }
                catch (System.Exception ex)
                {
                    prdDbg(ex);
                    AbortGracefully(localDb);
                    return;
                }
            }
        }

        /// <command>PLACEPERTTEEAUTO, PPTAUTO</command>
        /// <summary>
        /// Places a PERT tee at stik locations. Currently assumes all PERT25 is stik.
        /// </summary>
        /// <category>Blocks</category>
        [CommandMethod("PLACEPERTTEEAUTO")]
        [CommandMethod("PPTAUTO")]
        public void placepertteeauto()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;

            List<(Point3d pt, Oid stikpipe, Oid mainpipe)> list = new();

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    var pipes = localDb.GetFjvPipes(tx, true);

                    var stiks = pipes.Where(
                        x =>
                        GetPipeSystem(x) == PipeSystemEnum.PertFlextra &&
                        GetPipeDN(x) == 25
                        ).ToHashSet();

                    pipes.ExceptWith(stiks);

                    foreach (var stik in stiks)
                    {
                        Point3d sp = stik.StartPoint;
                        Point3d ep = stik.EndPoint;

                        var closestPipe = pipes.MinBy(
                            x =>
                            {
                                return new double[]
                                {
                                x.GetClosestPointTo(sp, false).DistanceTo(sp),
                                x.GetClosestPointTo(ep, false).DistanceTo(ep)
                                }.Min();
                            });

                        if (closestPipe == default)
                        {
                            prdDbg($"No closest pipe found for stik {stik.Handle}!");
                            continue;
                        }

                        double tol = 0.005;

                        double d1 = closestPipe.GetClosestPointTo(sp, false).DistanceTo(sp);
                        double d2 = closestPipe.GetClosestPointTo(ep, false).DistanceTo(ep);
                        double d = d1 > d2 ? d2 : d1;

                        if (d > tol)
                        { 
                            prdDbg($"Distance between stik {stik.Handle} and pipe {closestPipe.Handle} is {d}!\n" +
                                $"Which is larger than {tol}!");
                            continue;
                        }

                        list.Add((d1 > d2 ? ep : sp, stik.Id, closestPipe.Id));
                    }
                }
                catch (System.Exception ex)
                {
                    prdDbg(ex);
                    tx.Abort();
                    return;
                }
            }

            int count = 0;

            try
            {
                Oid closestId = default;
                foreach (var item in list)
                {
                    #region Find closest line

                    using (Transaction tx = localDb.TransactionManager.StartTransaction())
                    {
                        try
                        {
                            var pipes = localDb.GetFjvPipes(tx, true);

                            var stiks = pipes.Where(
                                x =>
                                GetPipeSystem(x) == PipeSystemEnum.PertFlextra &&
                                GetPipeDN(x) == 25
                                ).ToHashSet();

                            pipes.ExceptWith(stiks);

                            Polyline stik = item.stikpipe.Go<Polyline>(tx);

                            Point3d sp = stik.StartPoint;
                            Point3d ep = stik.EndPoint;

                            var closestPipe = pipes.MinBy(
                                x =>
                                {
                                    return new double[]
                                    {
                                x.GetClosestPointTo(sp, false).DistanceTo(sp),
                                x.GetClosestPointTo(ep, false).DistanceTo(ep)
                                    }.Min();
                                });

                            if (closestPipe == default)
                            {
                                prdDbg($"No closest pipe found for stik {stik.Handle}!");
                                continue;
                            }

                            closestId = closestPipe.Id;
                        }
                        catch (System.Exception ex)
                        {
                            prdDbg(ex);
                            tx.Abort();
                            continue;
                        }
                    }
                    #endregion

                    #region Place perttee
                    PertTee pertTee = new PertTee(
                        localDb, closestId, item.stikpipe, item.pt);
                    Result result = pertTee.Validate();
                    if (result.Status != ResultStatus.OK)
                    {
                        prdDbg(result.ErrorMsg);
                        continue;
                    }
                    result = pertTee.Place();
                    if (result.Status != ResultStatus.OK)
                    {
                        prdDbg(result.ErrorMsg);
                        continue;
                    }
                    if (result.Status == ResultStatus.OK) count++;
                    #endregion 
                }
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                AbortGracefully(localDb);
                return;
            }

            prdDbg($"Placed {count} PertTees!");
        }

        /// <command>CORRECTCUSTOMTRANSITIONS</command>
        /// <summary>
        /// If a transition found to have type "CUSTOM",
        /// attempts to find the correct type by reading the sizes
        /// of adjacent polylines.
        /// </summary>
        /// <category>Blocks</category>
        [CommandMethod("CORRECTCUSTOMTRANSISIONS")]
        public void correcttransitionsizes()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    HashSet<BlockReference> allbrs = localDb.HashSetOfType<BlockReference>(tx);
                    HashSet<Curve> curves = localDb.ListOfType<Curve>(tx).ToHashSet();

                    #region Read CSV
                    System.Data.DataTable dt = default;
                    try
                    {
                        dt = CsvReader.ReadCsvToDataTable(
                                @"X:\AutoCAD DRI - 01 Civil 3D\FJV Dynamiske Komponenter.csv", "FjvKomponenter");
                    }
                    catch (System.Exception ex)
                    {
                        prdDbg("Reading of FJV Dynamiske Komponenter.csv failed!");
                        prdDbg(ex);
                        throw;
                    }
                    if (dt == default)
                    {
                        prdDbg("Reading of FJV Dynamiske Komponenter.csv failed!");
                        throw new System.Exception("Failed to read FJV Dynamiske Komponenter.csv");
                    }
                    #endregion

                    var reducers = allbrs.Where(x => x.ReadDynamicCsvProperty(DynamicProperty.Type) == "Reduktion");

                    foreach (BlockReference r in reducers)
                    {
                        string type = r.ReadDynamicPropertyValue("Type");

                        if (type != "Custom") continue;

                        var endPoints = r.GetAllEndPoints();

                        if (endPoints.Count > 2 || endPoints.Count < 2)
                            throw new System.Exception($"Reducer {r.Handle} has unexpected number of endpoints: {endPoints.Count}!");

                        List<int> dns = new List<int>();

                        bool failed = false;
                        foreach (Point3d end in endPoints)
                        {
                            var query = curves.Where(x => end.IsOnCurve(x, 0.05));
                            if (query.Count() != 1)
                            {
                                prdDbg($"Reducer {r.Handle} cannot find connecting or finds multiple curve(s) at point {end}!");
                                failed = true;
                            }

                            if (!failed)
                            {
                                Curve curve = query.First();
                                dns.Add(GetPipeDN(curve));
                            }
                        }

                        type = "";
                        if (dns.Count > 0) type = $"{dns.Max()}x{dns.Min()}";

                        prdDbg($"Failed: {failed}; {r.Handle} -> {type}");

                        if (!failed)
                        {
                            SetDynBlockProperty(r, "Type", type);
                            r.AttSync();
                        }
                    }
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

        /// <command>SPLITPL2P</command>
        /// <summary>
        /// Splits a polyline at two specified points.
        /// </summary>
        /// <category>Fjernvarme Fremtidig</category>
        [CommandMethod("SPLITPL2P")]
        public void splitpl2p()
        {
            // Get the current document and database
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            // Prompt for the polyline selection
            PromptEntityOptions peo = new PromptEntityOptions("\nSelect a polyline to split: ");
            peo.SetRejectMessage("\nSelected entity is not a polyline.");
            peo.AddAllowedClass(typeof(Polyline), true);
            PromptEntityResult per = ed.GetEntity(peo);

            // If the polyline is selected, continue
            if (per.Status != PromptStatus.OK)
            {
                prdDbg("No polyline selected!");
                return;
            }

            Oid polylineId = per.ObjectId;

            // Prompt for the first point to split
            PromptPointResult ppr1 = ed.GetPoint("\nSelect first point on polyline to split: ");
            if (ppr1.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\nFirst point selection cancelled.");
                return;
            }

            // Prompt for the second point to split
            PromptPointResult ppr2 = ed.GetPoint("\nSelect second point on polyline to split: ");
            if (ppr2.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\nSecond point selection cancelled.");
                return;
            }

            // Open the transaction
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    // Open the polyline for write
                    Polyline pline = tr.GetObject(polylineId, OpenMode.ForRead) as Polyline;
                    if (pline == null)
                    {
                        ed.WriteMessage("\nSelected object is not a polyline.");
                        tr.Abort();
                        return;
                    }

                    // Find the closest points on the polyline to the selected points
                    Point3d nearestPt1 = pline.GetClosestPointTo(ppr1.Value, false);
                    Point3d nearestPt2 = pline.GetClosestPointTo(ppr2.Value, false);

                    // Get the parameters of the closest points
                    double param1 = pline.GetParameterAtPoint(nearestPt1);
                    double param2 = pline.GetParameterAtPoint(nearestPt2);

                    // Ensure param1 is less than param2 (swap if necessary)
                    if (param1 > param2)
                    {
                        double temp = param1;
                        param1 = param2;
                        param2 = temp;
                    }

                    DoubleCollection dc = new DoubleCollection([param1, param2]);
                    DBObjectCollection split = pline.GetSplitCurves(dc);

                    List<Polyline> result = new List<Polyline>();

                    bool success = false;
                    if (split.Count == 3)
                    {
                        success = true;
                        result.Add((Polyline)split[0]);
                        result.Add((Polyline)split[2]);
                    }
                    if (split.Count == 2)
                    {
                        success = true;
                        if (param1 == 0) result.Add((Polyline)split[1]);
                        else result.Add((Polyline)split[0]);
                    }

                    if (success)
                    {
                        foreach (Polyline poly in result)
                        {
                            poly.AddEntityToDbModelSpace(db);
                            PropertySetManager.CopyAllProperties(pline, poly);
                        }

                        pline.CheckOrOpenForWrite();
                        pline.Erase(true);
                    }
                }
                catch (System.Exception ex)
                {
                    prdDbg(ex);
                    tr.Abort();
                    return;
                }
                tr.Commit();
            }
        }
    }
}
