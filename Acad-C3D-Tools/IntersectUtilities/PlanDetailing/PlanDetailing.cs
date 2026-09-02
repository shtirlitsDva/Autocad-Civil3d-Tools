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

        /// <command>DRAWBONDEDCL</command>
        /// <summary>
        /// Tegner en eksakt centerlinje for enkelt (bonded) rørledninger.
        /// Twin ignoreres, da en twin-polylinje allerede ER sin egen centerlinje.
        ///
        /// Trin 1: FREM- og RETUR-polylinjer og de komponenter der sidder på dem samles
        /// til sammenhængende "run"-linjer, én pr. streng. Sammenhæng bestemmes af
        /// geometrisk sammenfald mellem rørender og komponenternes MuffeIntern-punkter.
        /// Vejen igennem en komponent tages fra komponentens EGEN centerlinje-geometri
        /// (korteste vej mellem muffepunkterne i blokkens interne kurvenet), så fx
        /// BUERØR gengives med sin rigtige bue og ikke som en korde.
        /// Disse hjælpelinjer lægges på laget 0-FJV-CL-RUN.
        ///
        /// Trin 2: Selve rørledningens centerlinje er den ægte bisektor (ækvidistante
        /// kurve) mellem frem- og retur-run: hvert punkt ligger på normalen af frem i
        /// sit frem-fodpunkt, på normalen af retur i sit retur-fodpunkt, og i samme
        /// afstand fra begge. Kun korridor-arket accepteres (fodpunkts-akkorden skal
        /// ligge i stationens normalkegle), så grene ved T-stykker forstyrrer ikke.
        /// Bisektoren af to linje/bue-kæder har ingen eksakt linje/bue-form, så den
        /// spores numerisk station for station (afstandsligningen er monoton, så
        /// bisektion er garanteret), skarpe hjørner samles med geringskryds som ved
        /// offset, og resultatet genopbygges af linjer og buer med sub-millimeter
        /// tolerance på laget 0-FJV-CL-ENKELT.
        ///
        /// Kommandoen er idempotent: eksisterende geometri på begge lag slettes først.
        /// </summary>
        /// <category>Fjernvarme Fremtidig</category>
        [CommandMethod("DRAWBONDEDCL")]
        public void drawbondedcl()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            //Connection tolerance. Measured on FJV-fremtid_2.26.1_fixture.dwg:
            //80% of pipe ends sit exactly on their MuffeIntern port and 96.5% within 1 um,
            //but the worst legitimate connection is ~14 mm off. Two ports on the same
            //component are never closer than ~100 mm, so 10 mm connects without merging.
            const double tol = 0.01;
            //Continuation threshold at nodes with 3+ edges: two edges are treated as
            //the same run only when they pass essentially straight through (bend
            //under ~37 degrees). Junction fittings route their main line straight
            //(a tee's through-edges are collinear); a sharper bend at a 3+ node
            //means a different logical run - e.g. the two single-pipe stubs of a
            //twin-transition fork, which a looser threshold would join into a
            //hairpin that then cannot pair with anything.
            const double continuationDot = -0.8;
            //A frem point is only paired with a retur point closer than this. The real
            //centre-to-centre spacing is DN dependent (~0.5-1.5 m); this is only a sanity
            //cutoff so a run is never paired with an unrelated run somewhere else.
            const double maxPairSeparation = 5.0;
            //Arc-length spacing of the stations at which the bisector is traced. The
            //trace is refitted afterwards, so the step only bounds how much geometry
            //can hide between two stations - not the accuracy of the fitted result.
            const double bisectorStep = 0.25;
            //Max deviation of the fitted line/arc centreline from the traced bisector.
            const double bisectorFitTol = 0.001;
            //A centreline piece supported by only a couple of samples is a junction
            //wisp, not a corridor - two station steps is the shortest real piece.
            const double minPieceLength = 2.0 * bisectorStep;
            const string runLayerName = "0-FJV-CL-RUN";
            const string clLayerName = "0-FJV-CL-ENKELT";
            const string clLinetypeName = "DASHED";

            List<Polyline> temps = new List<Polyline>();

            using Transaction tx = localDb.TransactionManager.StartTransaction();

            try
            {
                #region Prepare layer and linetype, wipe previous result
                LinetypeTable ltt = localDb.LinetypeTableId.Go<LinetypeTable>(tx);
                if (!ltt.Has(clLinetypeName))
                {
                    localDb.LoadLineTypeFile(clLinetypeName, "acad.lin");
                    ltt = localDb.LinetypeTableId.Go<LinetypeTable>(tx);
                }

                localDb.CheckOrCreateLayer(runLayerName, 8, false);
                localDb.CheckOrCreateLayer(clLayerName, 6, false);

                LayerTable lt = localDb.LayerTableId.Go<LayerTable>(tx);
                foreach (string name in new[] { runLayerName, clLayerName })
                {
                    LayerTableRecord ltr = lt[name].Go<LayerTableRecord>(tx);
                    if (ltr.LinetypeObjectId == ltt[clLinetypeName]) continue;
                    ltr.CheckOrOpenForWrite();
                    ltr.LinetypeObjectId = ltt[clLinetypeName];
                }

                foreach (Polyline old in localDb.ListOfType<Polyline>(tx)
                    .Where(x => x.Layer == clLayerName || x.Layer == runLayerName).ToList())
                {
                    old.CheckOrOpenForWrite();
                    old.Erase(true);
                }
                #endregion

                #region Collect enkelt pipes and candidate components
                List<Polyline> pipes = localDb.GetFjvPipes(tx, true)
                    .Where(x =>
                        GetPipeType(x) == PipeTypeEnum.Frem ||
                        GetPipeType(x) == PipeTypeEnum.Retur)
                    .ToList();

                if (pipes.Count == 0)
                {
                    prdDbg("No enkelt (FREM/RETUR) pipes found in drawing!");
                    tx.Abort();
                    return;
                }

                List<BlockReference> blocks =
                    localDb.GetFjvBlocks(tx, true, true, true).ToList();
                #endregion

                #region Build node set and pipe edges
                List<Point3d> nodePts = new List<Point3d>();
                Dictionary<(long, long), List<int>> nodeGrid = new();

                List<Polyline> edgeGeom = new List<Polyline>();
                List<int> edgeA = new List<int>();
                List<int> edgeB = new List<int>();
                List<bool> edgeIsPipe = new List<bool>();
                List<PipeTypeEnum> edgeType = new List<PipeTypeEnum>();

                foreach (Polyline pl in pipes)
                {
                    if (pl.Length < tol) continue;

                    Polyline copy = (Polyline)pl.Clone();
                    temps.Add(copy);

                    edgeGeom.Add(copy);
                    edgeA.Add(DbclGetOrAddNode(pl.StartPoint, nodePts, nodeGrid, tol));
                    edgeB.Add(DbclGetOrAddNode(pl.EndPoint, nodePts, nodeGrid, tol));
                    edgeIsPipe.Add(true);
                    edgeType.Add(GetPipeType(pl));
                }
                #endregion

                #region Build component edges from their internal centreline geometry
                List<string> fallbackBlocks = new List<string>();
                List<string> unresolvedBlocks = new List<string>();

                foreach (BlockReference br in blocks)
                {
                    List<Point3d> ports = br.GetAllEndPoints().ToList();
                    //Endebund and friends: a single port just terminates a chain.
                    if (ports.Count < 2) continue;

                    #region Collect the block's internal curves in WCS
                    BlockTableRecord btr = br.BlockTableRecord.Go<BlockTableRecord>(tx);
                    List<Polyline> inner = new List<Polyline>();
                    foreach (Oid oid in btr)
                    {
                        Entity nested = oid.Go<Entity>(tx);
                        Polyline conv = DbclCurveToPolyline(nested, br.BlockTransform);
                        if (conv == null) continue;
                        if (conv.Length < tol) { conv.Dispose(); continue; }
                        inner.Add(conv);
                    }
                    #endregion

                    #region Planarize: node set, then split curves at interior nodes
                    List<Point3d> locPts = new List<Point3d>();
                    Dictionary<(long, long), List<int>> locGrid = new();

                    foreach (Point3d p in ports) DbclGetOrAddNode(p, locPts, locGrid, tol);
                    foreach (Polyline c in inner)
                    {
                        DbclGetOrAddNode(c.StartPoint, locPts, locGrid, tol);
                        DbclGetOrAddNode(c.EndPoint, locPts, locGrid, tol);
                    }

                    List<Polyline> segs = new List<Polyline>();
                    foreach (Polyline c in inner)
                    {
                        List<double> pars = new List<double>();
                        foreach (Point3d p in locPts)
                        {
                            Point3d cp = c.GetClosestPointTo(p, false);
                            if (cp.DistanceHorizontalTo(p) > tol) continue;
                            double par = c.GetParameterAtPoint(cp);
                            if (par <= 1e-6 || par >= c.EndParam - 1e-6) continue;
                            pars.Add(par);
                        }

                        if (pars.Count == 0) { segs.Add(c); continue; }

                        pars.Sort();
                        DBObjectCollection split = c.GetSplitCurves(
                            new DoubleCollection(pars.ToArray()));
                        foreach (DBObject dbo in split)
                            if (dbo is Polyline sp) segs.Add(sp);
                        c.Dispose();
                    }
                    #endregion

                    #region Shortest path from the first port to every other port
                    List<(int A, int B, double W)> locEdges = new();
                    List<Polyline> locGeom = new List<Polyline>();
                    foreach (Polyline s in segs)
                    {
                        int a = DbclGetOrAddNode(s.StartPoint, locPts, locGrid, tol);
                        int b = DbclGetOrAddNode(s.EndPoint, locPts, locGrid, tol);
                        if (a == b) continue; //closed symbol geometry, never a path
                        locEdges.Add((a, b, s.Length));
                        locGeom.Add(s);
                    }

                    HashSet<int> used = new HashSet<int>();
                    bool anyPathFailed = false;
                    int fromNode = DbclGetOrAddNode(ports[0], locPts, locGrid, tol);

                    for (int i = 1; i < ports.Count; i++)
                    {
                        int toNode = DbclGetOrAddNode(ports[i], locPts, locGrid, tol);
                        List<int> path = DbclShortestPathEdges(
                            fromNode, toNode, locPts.Count, locEdges);

                        if (path == null) { anyPathFailed = true; continue; }
                        foreach (int e in path) used.Add(e);
                    }
                    #endregion

                    #region Emit the block's contribution
                    HashSet<Polyline> keep = new HashSet<Polyline>();
                    foreach (int e in used)
                    {
                        Polyline g = locGeom[e];
                        keep.Add(g);
                        temps.Add(g);
                        edgeGeom.Add(g);
                        edgeA.Add(DbclGetOrAddNode(g.StartPoint, nodePts, nodeGrid, tol));
                        edgeB.Add(DbclGetOrAddNode(g.EndPoint, nodePts, nodeGrid, tol));
                        edgeIsPipe.Add(false);
                        edgeType.Add(PipeTypeEnum.Ukendt);
                    }

                    //Decoration and symbol geometry never lies on a port-to-port path.
                    foreach (Polyline s in segs) if (!keep.Contains(s)) s.Dispose();

                    if (anyPathFailed)
                    {
                        //Fallback: chord through the insertion point. Exact for most
                        //two-port fittings, approximate for curved ones - reported below.
                        fallbackBlocks.Add($"{br.RealName()} {br.Handle}");

                        for (int i = 1; i < ports.Count; i++)
                        {
                            Polyline chord = new Polyline();
                            chord.AddVertexAt(0, ports[0].To2d(), 0.0, 0.0, 0.0);
                            if (br.Position.DistanceHorizontalTo(ports[0]) > tol &&
                                br.Position.DistanceHorizontalTo(ports[i]) > tol)
                                chord.AddVertexAt(
                                    chord.NumberOfVertices, br.Position.To2d(), 0.0, 0.0, 0.0);
                            chord.AddVertexAt(
                                chord.NumberOfVertices, ports[i].To2d(), 0.0, 0.0, 0.0);

                            if (chord.Length < tol) { chord.Dispose(); continue; }

                            temps.Add(chord);
                            edgeGeom.Add(chord);
                            edgeA.Add(DbclGetOrAddNode(
                                chord.StartPoint, nodePts, nodeGrid, tol));
                            edgeB.Add(DbclGetOrAddNode(
                                chord.EndPoint, nodePts, nodeGrid, tol));
                            edgeIsPipe.Add(false);
                            edgeType.Add(PipeTypeEnum.Ukendt);
                        }
                    }

                    if (used.Count == 0 && !anyPathFailed)
                        unresolvedBlocks.Add($"{br.RealName()} {br.Handle}");
                    #endregion
                }
                #endregion

                #region Drop components that contain no enkelt pipe
                int[] parent = new int[nodePts.Count];
                for (int i = 0; i < parent.Length; i++) parent[i] = i;

                for (int e = 0; e < edgeGeom.Count; e++)
                {
                    int ra = DbclFindRoot(edgeA[e], parent);
                    int rb = DbclFindRoot(edgeB[e], parent);
                    if (ra != rb) parent[ra] = rb;
                }

                HashSet<int> pipeRoots = new HashSet<int>();
                for (int e = 0; e < edgeGeom.Count; e++)
                    if (edgeIsPipe[e]) pipeRoots.Add(DbclFindRoot(edgeA[e], parent));

                List<int> liveEdges = new List<int>();
                for (int e = 0; e < edgeGeom.Count; e++)
                    if (pipeRoots.Contains(DbclFindRoot(edgeA[e], parent))) liveEdges.Add(e);
                #endregion

                #region Pair half-edges at every node
                //Half-edge he = edgeIndex * 2 + end (0 = A-end, 1 = B-end).
                Dictionary<int, List<int>> nodeHalfEdges = new Dictionary<int, List<int>>();
                foreach (int e in liveEdges)
                {
                    if (!nodeHalfEdges.TryGetValue(edgeA[e], out var la))
                    { la = new List<int>(); nodeHalfEdges[edgeA[e]] = la; }
                    la.Add(e * 2);

                    if (!nodeHalfEdges.TryGetValue(edgeB[e], out var lb))
                    { lb = new List<int>(); nodeHalfEdges[edgeB[e]] = lb; }
                    lb.Add(e * 2 + 1);
                }

                Dictionary<int, int> pairOf = new Dictionary<int, int>();
                foreach (var kvp in nodeHalfEdges)
                {
                    List<int> hes = kvp.Value;
                    if (hes.Count < 2) continue;

                    if (hes.Count == 2)
                    {
                        //A plain joint - always continue, even around a 90 degree elbow.
                        pairOf[hes[0]] = hes[1];
                        pairOf[hes[1]] = hes[0];
                        continue;
                    }

                    //Tee, Y or F-model: continue along the straightest pair, the rest branch off.
                    List<(double Dot, int H1, int H2)> cands = new();
                    for (int i = 0; i < hes.Count; i++)
                        for (int j = i + 1; j < hes.Count; j++)
                            cands.Add((
                                DbclOutgoingDir(hes[i], edgeGeom).DotProduct(
                                    DbclOutgoingDir(hes[j], edgeGeom)),
                                hes[i], hes[j]));

                    foreach (var c in cands.OrderBy(x => x.Dot))
                    {
                        if (c.Dot > continuationDot) break;
                        if (pairOf.ContainsKey(c.H1) || pairOf.ContainsKey(c.H2)) continue;
                        pairOf[c.H1] = c.H2;
                        pairOf[c.H2] = c.H1;
                    }
                }
                #endregion

                #region Trace chains and emit centreline polylines
                HashSet<int> visited = new HashSet<int>();
                List<List<int>> chains = new List<List<int>>();

                //Open chains first: start at every half-edge that has no continuation.
                foreach (int e in liveEdges)
                {
                    for (int end = 0; end < 2; end++)
                    {
                        int he = e * 2 + end;
                        if (pairOf.ContainsKey(he)) continue;
                        if (visited.Contains(e)) continue;
                        chains.Add(DbclTraceChain(he, pairOf, visited));
                    }
                }

                //Whatever is left is a closed loop.
                foreach (int e in liveEdges)
                {
                    if (visited.Contains(e)) continue;
                    chains.Add(DbclTraceChain(e * 2, pairOf, visited));
                }

                List<Polyline> fremRuns = new List<Polyline>();
                List<Polyline> returRuns = new List<Polyline>();

                int drawn = 0;
                foreach (List<int> chain in chains)
                {
                    if (chain.Count == 0) continue;

                    Polyline outPl = new Polyline();
                    Point3d lastPt = Point3d.Origin;

                    //A chain is frem or retur according to the pipes it is built from;
                    //component edges carry no type of their own.
                    PipeTypeEnum chainType = PipeTypeEnum.Ukendt;
                    foreach (int he in chain)
                    {
                        if (edgeType[he / 2] == PipeTypeEnum.Ukendt) continue;
                        chainType = edgeType[he / 2];
                        break;
                    }

                    foreach (int he in chain)
                    {
                        Polyline g = edgeGeom[he / 2];
                        bool forward = he % 2 == 0;

                        int n = g.NumberOfVertices;
                        for (int i = 0; i < n - 1; i++)
                        {
                            int vi = forward ? i : n - 1 - i;
                            //Reversing a polyline flips which vertex owns the bulge
                            //and its sign; take it from the vertex we are leaving.
                            double bulge = forward
                                ? g.GetBulgeAt(vi)
                                : -g.GetBulgeAt(vi - 1);
                            outPl.AddVertexAt(
                                outPl.NumberOfVertices, g.GetPoint2dAt(vi), bulge, 0.0, 0.0);
                        }
                        lastPt = forward ? g.EndPoint : g.StartPoint;
                    }

                    outPl.AddVertexAt(outPl.NumberOfVertices, lastPt.To2d(), 0.0, 0.0, 0.0);

                    if (outPl.NumberOfVertices < 2 || outPl.Length < tol)
                    { outPl.Dispose(); continue; }

                    outPl.Layer = runLayerName;
                    outPl.Linetype = clLinetypeName;
                    outPl.ConstantWidth = 0.0;
                    outPl.AddEntityToDbModelSpace(localDb);
                    drawn++;

                    if (chainType == PipeTypeEnum.Frem) fremRuns.Add(outPl);
                    else if (chainType == PipeTypeEnum.Retur) returRuns.Add(outPl);
                }
                #endregion

                #region Derive the pipeline centreline as the trimmed bisector of frem and retur
                //The centreline is the locus of points equidistant from the frem and the
                //retur runs: every centreline point lies on the normal of frem at its
                //frem foot point, on the normal of retur at its retur foot point, and at
                //equal distance from both curves (the trimmed bisector, Elber & Kim,
                //Computer-Aided Design 30(14) 1998). Only the "corridor sheet" of the
                //bisector is wanted, so a foot-point pair is accepted only where the two
                //tangents are near-parallel - this rejects the sheets that bisect a run
                //against its perpendicular branch at a tee. The bisector of two line/arc
                //chains is algebraic but has no exact line/arc representation, so it is
                //traced numerically station by station (the distance solve is monotone,
                //so bisection is guaranteed) and refitted with lines and arcs to
                //sub-millimetre tolerance.
                int clDrawn = 0;
                List<string> unpairedRuns = new List<string>();

                List<DbclRunGeom> returIdx = returRuns
                    .Select(DbclBuildRunGeom).ToList();

                foreach (Polyline f in fremRuns)
                {
                    List<List<Point3d>> pieces = DbclTraceBisector(
                        f, returIdx, maxPairSeparation, bisectorStep);

                    bool any = false;
                    foreach (List<Point3d> piece in pieces)
                    {
                        Polyline mid = DbclFitPolyline(piece, bisectorFitTol);
                        if (mid == null) continue;
                        if (mid.NumberOfVertices < 2 || mid.Length < minPieceLength)
                        { mid.Dispose(); continue; }

                        mid.Layer = clLayerName;
                        mid.Linetype = clLinetypeName;
                        mid.ConstantWidth = 0.0;
                        mid.AddEntityToDbModelSpace(localDb);
                        clDrawn++;
                        any = true;
                    }

                    if (!any) unpairedRuns.Add(f.Handle.ToString());
                }
                #endregion

                #region Report
                prdDbg(
                    $"DRAWBONDEDCL: {pipes.Count} enkelt pipe(s), {liveEdges.Count} edge(s) -> " +
                    $"{drawn} run line(s) on {runLayerName} " +
                    $"({fremRuns.Count} frem / {returRuns.Count} retur) -> " +
                    $"{clDrawn} pipeline centreline(s) on {clLayerName}.");

                if (unpairedRuns.Count > 0)
                    prdDbg(
                        $"{unpairedRuns.Count} frem run(s) had no retur run within " +
                        $"{maxPairSeparation} m and got no centreline:\n" +
                        string.Join(", ", unpairedRuns));

                if (fallbackBlocks.Count > 0)
                    prdDbg(
                        $"{fallbackBlocks.Count} component(s) had no internal port-to-port " +
                        $"centreline and fell back to a chord through the insertion point:\n" +
                        string.Join("\n", fallbackBlocks.Distinct()));

                if (unresolvedBlocks.Count > 0)
                    prdDbg(
                        $"{unresolvedBlocks.Count} component(s) contributed no geometry:\n" +
                        string.Join("\n", unresolvedBlocks.Distinct()));
                #endregion
            }
            catch (System.Exception ex)
            {
                tx.Abort();
                prdDbg(ex);
                return;
            }
            finally
            {
                foreach (Polyline p in temps) p.Dispose();
            }

            tx.Commit();
        }

        /// <summary>
        /// Returns the index of the node at <paramref name="p"/>, creating it when no
        /// existing node lies within <paramref name="tol"/>. Uses a hash grid sized to
        /// the tolerance, so lookup stays constant time as the node count grows.
        /// </summary>
        private static int DbclGetOrAddNode(
            Point3d p,
            List<Point3d> nodePts,
            Dictionary<(long, long), List<int>> grid,
            double tol)
        {
            long cx = (long)Math.Floor(p.X / tol);
            long cy = (long)Math.Floor(p.Y / tol);

            for (long dx = -1; dx <= 1; dx++)
                for (long dy = -1; dy <= 1; dy++)
                    if (grid.TryGetValue((cx + dx, cy + dy), out var bucket))
                        foreach (int idx in bucket)
                            if (nodePts[idx].DistanceHorizontalTo(p) <= tol) return idx;

            int newIdx = nodePts.Count;
            nodePts.Add(p);
            if (!grid.TryGetValue((cx, cy), out var own))
            { own = new List<int>(); grid[(cx, cy)] = own; }
            own.Add(newIdx);
            return newIdx;
        }

        /// <summary>
        /// Converts a Line, Arc or Polyline to an in-memory Polyline in WCS.
        /// Returns null for anything else (text, hatches, nested blocks).
        /// The caller owns and must dispose the result.
        /// </summary>
        private static Polyline DbclCurveToPolyline(Entity ent, Matrix3d xform)
        {
            switch (ent)
            {
                case Line ln:
                    {
                        using Line c = (Line)ln.Clone();
                        c.TransformBy(xform);
                        Polyline pl = new Polyline(2);
                        pl.AddVertexAt(0, c.StartPoint.To2d(), 0.0, 0.0, 0.0);
                        pl.AddVertexAt(1, c.EndPoint.To2d(), 0.0, 0.0, 0.0);
                        return pl;
                    }
                case Arc ar:
                    {
                        using Arc c = (Arc)ar.Clone();
                        c.TransformBy(xform);
                        //An arc always sweeps CCW about its own normal; a mirrored
                        //instance flips the normal, and with it the bulge sign.
                        double bulge = Math.Tan(c.TotalAngle / 4.0);
                        if (c.Normal.Z < 0.0) bulge = -bulge;
                        Polyline pl = new Polyline(2);
                        pl.AddVertexAt(0, c.StartPoint.To2d(), bulge, 0.0, 0.0);
                        pl.AddVertexAt(1, c.EndPoint.To2d(), 0.0, 0.0, 0.0);
                        return pl;
                    }
                case Polyline plo:
                    {
                        Polyline c = (Polyline)plo.Clone();
                        c.TransformBy(xform);
                        return c;
                    }
                default:
                    return null;
            }
        }

        /// <summary>
        /// Dijkstra over a small undirected edge list. Returns the edge indices of the
        /// shortest path, or null when the two nodes are not connected.
        /// </summary>
        private static List<int> DbclShortestPathEdges(
            int fromNode, int toNode, int nodeCount, List<(int A, int B, double W)> edges)
        {
            if (fromNode == toNode) return new List<int>();

            double[] dist = new double[nodeCount];
            int[] prevEdge = new int[nodeCount];
            bool[] done = new bool[nodeCount];
            for (int i = 0; i < nodeCount; i++)
            { dist[i] = double.MaxValue; prevEdge[i] = -1; }
            dist[fromNode] = 0.0;

            while (true)
            {
                int u = -1;
                double best = double.MaxValue;
                for (int i = 0; i < nodeCount; i++)
                    if (!done[i] && dist[i] < best) { best = dist[i]; u = i; }

                if (u == -1 || u == toNode) break;
                done[u] = true;

                for (int e = 0; e < edges.Count; e++)
                {
                    int v = -1;
                    if (edges[e].A == u) v = edges[e].B;
                    else if (edges[e].B == u) v = edges[e].A;
                    if (v == -1 || done[v]) continue;

                    double nd = dist[u] + edges[e].W;
                    if (nd < dist[v]) { dist[v] = nd; prevEdge[v] = e; }
                }
            }

            if (dist[toNode] == double.MaxValue) return null;

            List<int> path = new List<int>();
            int cur = toNode;
            while (cur != fromNode)
            {
                int e = prevEdge[cur];
                if (e == -1) return null;
                path.Add(e);
                cur = edges[e].A == cur ? edges[e].B : edges[e].A;
            }
            path.Reverse();
            return path;
        }

        /// <summary>
        /// Unit direction pointing away from the node that half-edge
        /// <paramref name="he"/> is attached to.
        /// </summary>
        private static Vector3d DbclOutgoingDir(int he, List<Polyline> edgeGeom)
        {
            Polyline g = edgeGeom[he / 2];
            Vector3d v = he % 2 == 0
                ? g.GetFirstDerivative(g.StartPoint)
                : g.GetFirstDerivative(g.EndPoint).Negate();
            return v.Length < Autodesk.AutoCAD.Geometry.Tolerance.Global.EqualPoint
                ? v
                : v.GetNormal();
        }

        /// <summary>
        /// Walks the paired half-edges forward from <paramref name="startHe"/> and
        /// returns the chain as half-edge indices, each entered at its listed end.
        /// </summary>
        private static List<int> DbclTraceChain(
            int startHe, Dictionary<int, int> pairOf, HashSet<int> visited)
        {
            List<int> chain = new List<int>();
            int cur = startHe;

            while (true)
            {
                int e = cur / 2;
                if (visited.Contains(e)) break;
                visited.Add(e);
                chain.Add(cur);

                int exitHe = e * 2 + (1 - cur % 2);
                if (!pairOf.TryGetValue(exitHe, out int next)) break;
                if (visited.Contains(next / 2)) break;
                cur = next;
            }

            return chain;
        }

        /// <summary>Union-find root with path compression.</summary>
        private static int DbclFindRoot(int i, int[] parent)
        {
            while (parent[i] != i)
            {
                parent[i] = parent[parent[i]];
                i = parent[i];
            }
            return i;
        }

        //Corridor-sheet condition: a foot-point pair is only accepted when the chord
        //from the query point to the retur foot deviates less than 35 degrees from
        //the station normal. 35 degrees passes the tangent mismatch drafting slop
        //and curvature produce, and rejects 45-degree-and-up branch sheets at tees.
        private const double DbclChordSkewSin = 0.57357643635104609;

        /// <summary>
        /// Traces the corridor sheet of the trimmed bisector between
        /// <paramref name="frem"/> and the retur runs. Stations are placed every
        /// <paramref name="step"/> of arc length plus at every vertex; a vertex
        /// with a real turn gets one station per one-sided tangent and the pair is
        /// joined by a miter intersection, the way offset corners join. At each
        /// station the equidistant radius r solves dist(fp + r*n, retur) = r, which
        /// is a guaranteed bisection root because g(r) = dist - r is monotone
        /// nonincreasing (the distance field has unit gradient). Stations without a
        /// valid correspondence split the trace into separate pieces.
        /// </summary>
        private static List<List<Point3d>> DbclTraceBisector(
            Polyline frem,
            List<DbclRunGeom> returRuns,
            double maxSep,
            double step)
        {
            //A vertex turning less than this is treated as smooth.
            const double minMiterTurn = 1e-3;

            double totalLen = frem.Length;

            #region Station distances: regular steps plus every vertex station
            List<double> dists = new List<double>();
            for (double d = 0.0; d < totalLen; d += step) dists.Add(d);
            dists.Add(totalLen);
            for (int i = 1; i < frem.NumberOfVertices - 1; i++)
                dists.Add(frem.GetDistanceAtParameter(i));
            dists.Sort();
            #endregion

            #region Stations: foot point, ray tangent, and the corner kind
            //T orients the station's ray (normal = rot90(T)) and its sheet test.
            //A vertex with a real turn gets TWO stations, one per one-sided
            //tangent (Kind 1 entering, 2 leaving) - the pair is joined by a miter
            //intersection when both solve, the way offset corners join. The
            //equidistant set itself bulges outward at a sharp double corner (the
            //diagonal is equidistant to the two corner POINTS at more than the
            //half-gap), which is not how a pipe pair's axis corners.
            List<(Point3d P, Vector3d T, int Kind)> stations =
                new List<(Point3d, Vector3d, int)>();
            double prevD = double.MinValue;
            foreach (double d in dists)
            {
                if (d - prevD < 1e-9) continue;
                prevD = d;

                double par = frem.GetParameterAtDistance(Math.Min(d, totalLen));
                Point3d fp = frem.GetPointAtParameter(par);

                bool interiorVertex =
                    Math.Abs(par - Math.Round(par)) < 1e-6 &&
                    par > 0.5 && par < frem.EndParam - 0.5;

                if (!interiorVertex)
                {
                    Vector3d t = frem.GetFirstDerivative(par);
                    if (t.Length < 1e-12) continue;
                    stations.Add((fp, t.GetNormal(), 0));
                    continue;
                }

                Vector3d tin = frem.GetFirstDerivative(
                    Math.Max(par - 1e-6, 0.0)).GetNormal();
                Vector3d tout = frem.GetFirstDerivative(
                    Math.Min(par + 1e-6, frem.EndParam)).GetNormal();
                double ang = Math.Atan2(
                    tin.X * tout.Y - tin.Y * tout.X, tin.DotProduct(tout));
                if (Math.Abs(ang) < minMiterTurn)
                {
                    stations.Add((fp, tin, 0));
                    continue;
                }
                stations.Add((fp, tin, 1));
                stations.Add((fp, tout, 2));
            }
            #endregion

            #region Solve the equidistant radius at every station
            List<List<Point3d>> pieces = new List<List<Point3d>>();
            List<Point3d> cur = new List<Point3d>();
            //Consecutive bisector samples further apart than this belong to
            //different sheets - split rather than bridge.
            double jumpLimit = 5.0 * step;

            bool pendingMiter = false;
            Point3d miterP = Point3d.Origin;
            Vector3d miterT = Vector3d.XAxis;

            foreach ((Point3d fp, Vector3d T, int kind) in stations)
            {
                double d0 = DbclValidDist(fp, T, 0.0, returRuns, maxSep, out Point3d q0);
                bool ok = d0 <= maxSep;
                Point3d bis = Point3d.Origin;

                if (ok)
                {
                    //Normal pointing to the side the retur foot is on.
                    double side = T.X * (q0.Y - fp.Y) - T.Y * (q0.X - fp.X);
                    Vector3d n = new Vector3d(-T.Y, T.X, 0.0);
                    if (side < 0.0) n = n.Negate();

                    double lo = 0.0, hi = d0;
                    double gHi = DbclValidDist(
                        fp + n * hi, T, 0.0, returRuns, maxSep, out _) - hi;
                    if (gHi > 0.0)
                    {
                        hi = maxSep;
                        gHi = DbclValidDist(
                            fp + n * hi, T, 0.0, returRuns, maxSep, out _) - hi;
                    }

                    if (gHi > 0.0) ok = false;
                    else
                    {
                        for (int it = 0; it < 24; it++)
                        {
                            double mid = (lo + hi) / 2.0;
                            double g = DbclValidDist(
                                fp + n * mid, T, 0.0, returRuns, maxSep, out _) - mid;
                            if (g > 0.0) lo = mid; else hi = mid;
                        }
                        double r = (lo + hi) / 2.0;
                        bis = fp + n * r;
                        //The sheet filter can make the distance field jump; accept
                        //only a true root, not a discontinuity bisection homed on.
                        double res = DbclValidDist(
                            bis, T, 0.0, returRuns, maxSep, out _) - r;
                        if (Math.Abs(res) > 1e-4) ok = false;
                    }
                }

                if (!ok)
                {
                    pendingMiter = false;
                    if (cur.Count > 1) pieces.Add(cur);
                    cur = new List<Point3d>();
                    continue;
                }

                if (cur.Count > 0 &&
                    cur[cur.Count - 1].DistanceHorizontalTo(bis) > jumpLimit)
                {
                    pendingMiter = false;
                    if (cur.Count > 1) pieces.Add(cur);
                    cur = new List<Point3d>();
                }

                //Join the two one-sided corner points through their miter: the
                //intersection of the incoming mid-line (through the Kind-1 point
                //along its tangent) with the outgoing one, when it lies between
                //them and within a sane miter length.
                if (kind == 2 && pendingMiter)
                {
                    //Lines miterP + t1*miterT and bis + s*T; the miter is valid
                    //when it lies ahead of the incoming point (t1 > 0), behind
                    //the outgoing one (s < 0), and within a sane miter length.
                    double det = miterT.X * T.Y - miterT.Y * T.X;
                    if (Math.Abs(det) > 1e-9)
                    {
                        double rx = bis.X - miterP.X, ry = bis.Y - miterP.Y;
                        double t1 = (rx * T.Y - ry * T.X) / det;
                        double s = (rx * miterT.Y - ry * miterT.X) / det;
                        double span = 2.0 * miterP.DistanceHorizontalTo(bis);
                        if (t1 > 1e-9 && s < -1e-9 && t1 < span && -s < span)
                            cur.Add(new Point3d(
                                miterP.X + t1 * miterT.X,
                                miterP.Y + t1 * miterT.Y, 0.0));
                    }
                }
                pendingMiter = kind == 1;
                if (pendingMiter) { miterP = bis; miterT = T; }

                cur.Add(bis);
            }
            if (cur.Count > 1) pieces.Add(cur);
            #endregion

            #region Drop wisps, then stitch pieces across small disruptions
            //Near a component joint or a spot where the pair swaps sides (the two
            //runs cross), correspondence degenerates for a metre or two and the
            //trace splits, sometimes leaving a stray far-sheet wisp between the
            //good pieces. Wisps go; consecutive pieces of this run separated by
            //less than half the pairing cutoff are rejoined - through the miter
            //intersection of their end tangents when the ends form a corner, as a
            //plain chord otherwise.
            double stitchLimit = maxSep / 2.0;
            double wispLimit = 2.0 * step;

            List<List<Point3d>> stitched = new List<List<Point3d>>();
            foreach (List<Point3d> piece in pieces)
            {
                if (piece[0].DistanceHorizontalTo(piece[piece.Count - 1]) < wispLimit)
                    continue;
                if (stitched.Count == 0) { stitched.Add(piece); continue; }

                List<Point3d> prev = stitched[stitched.Count - 1];
                if (prev[prev.Count - 1].DistanceHorizontalTo(piece[0]) > stitchLimit)
                { stitched.Add(piece); continue; }

                Vector3d ta = (prev[prev.Count - 1] - prev[prev.Count - 2]).GetNormal();
                Vector3d tb = (piece[1] - piece[0]).GetNormal();

                //In a crossing region the two pieces overlap spatially; joining
                //them raw folds the polyline back on itself. Trim what lies
                //behind the seam on either side first.
                while (piece.Count > 2 &&
                    (piece[0] - prev[prev.Count - 1]).DotProduct(ta) <= 1e-9)
                    piece.RemoveAt(0);
                while (prev.Count > 2 &&
                    (piece[0] - prev[prev.Count - 1]).DotProduct(tb) <= 1e-9)
                    prev.RemoveAt(prev.Count - 1);

                Point3d aEnd = prev[prev.Count - 1];
                Point3d bStart = piece[0];
                double gap = aEnd.DistanceHorizontalTo(bStart);
                if (gap > stitchLimit) { stitched.Add(piece); continue; }

                //Recompute the seam tangents from the TRIMMED ends: a stray
                //far-sheet sample at a raw piece boundary would otherwise
                //corrupt them and veto a legitimate miter.
                ta = (prev[prev.Count - 1] - prev[prev.Count - 2]).GetNormal();
                tb = (piece[1] - piece[0]).GetNormal();

                //Miter only when the intersection stays near the seam - a miter
                //longer than twice the gap means near-parallel end tangents, and
                //a chord joins those better than a spike.
                double det = ta.X * tb.Y - ta.Y * tb.X;
                if (Math.Abs(det) > 1e-3)
                {
                    double rx = bStart.X - aEnd.X, ry = bStart.Y - aEnd.Y;
                    double t1 = (rx * tb.Y - ry * tb.X) / det;
                    double s = (rx * ta.Y - ry * ta.X) / det;
                    if (t1 > 1e-9 && s < -1e-9 &&
                        t1 < 2.0 * gap && -s < 2.0 * gap)
                        prev.Add(new Point3d(
                            aEnd.X + t1 * ta.X, aEnd.Y + t1 * ta.Y, 0.0));
                }
                prev.AddRange(piece);
            }
            #endregion

            #region Remove sub-resolution backtracks
            //Where the runs converge (twin-to-single reducers) or a sheet
            //transition crowds stations, consecutive samples can jitter side to
            //side. The trace advances one step per station, so a reversal of
            //more than 120 degrees whose legs are both shorter than the station
            //spacing cannot represent real geometry - it is sampling noise. The
            //120-degree bound keeps legitimate miter corners: a right-angle
            //elbow's miter turns exactly 90 degrees on legs of about the
            //half-gap, which can be shorter than the station spacing.
            double noiseLeg = 1.5 * step;
            foreach (List<Point3d> piece in stitched)
            {
                bool removed = true;
                while (removed && piece.Count > 2)
                {
                    removed = false;
                    for (int k = 1; k < piece.Count - 1; k++)
                    {
                        Vector3d v1 = piece[k] - piece[k - 1];
                        Vector3d v2 = piece[k + 1] - piece[k];
                        if (v1.DotProduct(v2) < -0.5 * v1.Length * v2.Length &&
                            v1.Length < noiseLeg && v2.Length < noiseLeg)
                        { piece.RemoveAt(k); removed = true; break; }
                    }
                }
            }
            #endregion

            return stitched;
        }

        /// <summary>
        /// Line/arc segments and interior vertices of one run, unpacked to plain
        /// doubles so the distance query in the bisector trace runs without any
        /// AutoCAD API calls or allocations.
        /// </summary>
        private sealed class DbclRunGeom
        {
            public double MinX, MinY, MaxX, MaxY;
            //Per segment: type 0 = line (A + t*U, t in [0, Len]),
            //type 1 = arc (centre C, radius R, start angle Sa, signed sweep).
            public int[] SegType;
            public double[] Ax, Ay, Ux, Uy, Len;
            public double[] Cx, Cy, R, Sa, Sweep;
            //Interior vertices with their one-sided unit tangents and the signed
            //turn angle - the corner candidates.
            public double[] Vx, Vy, VinX, VinY, Turn;
        }

        /// <summary>Unpacks a run polyline into a <see cref="DbclRunGeom"/>.</summary>
        private static DbclRunGeom DbclBuildRunGeom(Polyline r)
        {
            int nSeg = r.NumberOfVertices - 1;
            DbclRunGeom g = new DbclRunGeom
            {
                SegType = new int[nSeg],
                Ax = new double[nSeg], Ay = new double[nSeg],
                Ux = new double[nSeg], Uy = new double[nSeg], Len = new double[nSeg],
                Cx = new double[nSeg], Cy = new double[nSeg], R = new double[nSeg],
                Sa = new double[nSeg], Sweep = new double[nSeg],
            };

            Extents3d ext = r.GeometricExtents;
            g.MinX = ext.MinPoint.X; g.MinY = ext.MinPoint.Y;
            g.MaxX = ext.MaxPoint.X; g.MaxY = ext.MaxPoint.Y;

            for (int i = 0; i < nSeg; i++)
            {
                Point2d a = r.GetPoint2dAt(i);
                Point2d b = r.GetPoint2dAt(i + 1);
                double bulge = r.GetBulgeAt(i);
                double chord = a.GetDistanceTo(b);

                if (Math.Abs(bulge) < 1e-12 || chord < 1e-12)
                {
                    g.SegType[i] = 0;
                    g.Ax[i] = a.X; g.Ay[i] = a.Y;
                    g.Len[i] = chord;
                    if (chord > 1e-12)
                    { g.Ux[i] = (b.X - a.X) / chord; g.Uy[i] = (b.Y - a.Y) / chord; }
                    continue;
                }

                //Centre from the bulge: a positive bulge sweeps CCW, which puts
                //the centre on the LEFT of the chord at r*cos(theta/2) from the
                //chord midpoint (negative for a major arc, flipping the side).
                g.SegType[i] = 1;
                double theta = 4.0 * Math.Atan(bulge);
                double radius = chord / (2.0 * Math.Sin(Math.Abs(theta) / 2.0));
                double nx = -(b.Y - a.Y) / chord, ny = (b.X - a.X) / chord;
                double off = Math.Sign(bulge) * radius * Math.Cos(theta / 2.0);
                double cx = (a.X + b.X) / 2.0 + nx * off;
                double cy = (a.Y + b.Y) / 2.0 + ny * off;
                g.Cx[i] = cx; g.Cy[i] = cy; g.R[i] = radius;
                g.Sa[i] = Math.Atan2(a.Y - cy, a.X - cx);
                g.Sweep[i] = theta;
            }

            #region Interior vertices with one-sided tangents
            int nV = Math.Max(0, r.NumberOfVertices - 2);
            g.Vx = new double[nV]; g.Vy = new double[nV];
            g.VinX = new double[nV]; g.VinY = new double[nV];
            g.Turn = new double[nV];
            for (int j = 0; j < nV; j++)
            {
                Point2d v = r.GetPoint2dAt(j + 1);
                g.Vx[j] = v.X; g.Vy[j] = v.Y;
                Vector3d tin = r.GetFirstDerivative(j + 1 - 1e-6);
                Vector3d tout = r.GetFirstDerivative(j + 1 + 1e-6);
                if (tin.Length < 1e-12 || tout.Length < 1e-12) continue;
                tin = tin.GetNormal(); tout = tout.GetNormal();
                g.VinX[j] = tin.X; g.VinY[j] = tin.Y;
                g.Turn[j] = Math.Atan2(
                    tin.X * tout.Y - tin.Y * tout.X, tin.DotProduct(tout));
            }
            #endregion

            return g;
        }

        /// <summary>
        /// Distance from <paramref name="p"/> to the nearest retur foot point that
        /// is a valid corridor correspondence. EVERY local foot candidate on every
        /// run is considered - each segment's perpendicular (or radial) interior
        /// foot and each interior vertex - because the globally closest point may
        /// be an invalid sheet while a slightly farther foot is the valid one. A
        /// smooth foot is valid when its chord is near-perpendicular to the
        /// station tangent; a vertex foot when its chord lies in the vertex's
        /// normal cone widened by the same slack. Run endpoints are never feet -
        /// correspondence simply ends there. Returns MaxValue when no valid foot
        /// exists within <paramref name="cutoff"/>.
        /// </summary>
        private static double DbclValidDist(
            Point3d p,
            Vector3d coneTin,
            double coneTurn,
            List<DbclRunGeom> returRuns,
            double cutoff,
            out Point3d foot)
        {
            const double eps = 1e-6;
            double slack = Math.Asin(DbclChordSkewSin);
            double best = double.MaxValue;
            foot = Point3d.Origin;

            foreach (DbclRunGeom g in returRuns)
            {
                double lim = Math.Min(best, cutoff);
                double bx = Math.Max(Math.Max(g.MinX - p.X, p.X - g.MaxX), 0.0);
                double by = Math.Max(Math.Max(g.MinY - p.Y, p.Y - g.MaxY), 0.0);
                if (bx * bx + by * by >= lim * lim) continue;

                #region Segment interior feet
                for (int i = 0; i < g.SegType.Length; i++)
                {
                    double fx, fy, d;
                    if (g.SegType[i] == 0)
                    {
                        if (g.Len[i] < 1e-12) continue;
                        double t = (p.X - g.Ax[i]) * g.Ux[i] + (p.Y - g.Ay[i]) * g.Uy[i];
                        if (t <= eps || t >= g.Len[i] - eps) continue;
                        fx = g.Ax[i] + t * g.Ux[i];
                        fy = g.Ay[i] + t * g.Uy[i];
                    }
                    else
                    {
                        double vx = p.X - g.Cx[i], vy = p.Y - g.Cy[i];
                        double vLen = Math.Sqrt(vx * vx + vy * vy);
                        if (vLen < 1e-9) continue;
                        double ang = Math.Atan2(vy, vx);
                        double sw = g.Sweep[i];
                        double angEps = eps / g.R[i];
                        double along = sw > 0.0
                            ? DbclNormalizeCcw(ang - g.Sa[i])
                            : DbclNormalizeCcw(g.Sa[i] - ang);
                        if (along <= angEps || along >= Math.Abs(sw) - angEps) continue;
                        fx = g.Cx[i] + g.R[i] * vx / vLen;
                        fy = g.Cy[i] + g.R[i] * vy / vLen;
                    }

                    double ddx = fx - p.X, ddy = fy - p.Y;
                    d = Math.Sqrt(ddx * ddx + ddy * ddy);
                    if (d >= best || d > cutoff || d < 1e-9) continue;

                    //The chord must lie in the STATION's cone: normal +- slack at
                    //a smooth station, the corner's normal cone +- slack at one.
                    if (!DbclChordInCone(
                        ddx / d, ddy / d, coneTin, coneTurn, slack)) continue;

                    best = d;
                    foot = new Point3d(fx, fy, 0.0);
                }
                #endregion

                #region Interior vertex feet
                for (int j = 0; j < g.Vx.Length; j++)
                {
                    double wx = p.X - g.Vx[j], wy = p.Y - g.Vy[j];
                    double d = Math.Sqrt(wx * wx + wy * wy);
                    if (d >= best || d > cutoff || d < 1e-9) continue;
                    wx /= d; wy /= d;

                    //Both cones must accept the chord: the station's (chord from
                    //station side toward retur) and the retur corner's own normal
                    //cone (chord from the corner toward the station side).
                    if (!DbclChordInCone(-wx, -wy, coneTin, coneTurn, slack)) continue;
                    if (!DbclChordInCone(
                        wx, wy,
                        new Vector3d(g.VinX[j], g.VinY[j], 0.0), g.Turn[j], slack))
                        continue;

                    best = d;
                    foot = new Point3d(g.Vx[j], g.Vy[j], 0.0);
                }
                #endregion
            }
            return best;
        }

        /// <summary>
        /// True when the unit chord (cx, cy) lies within the normal cone spanned
        /// by rotating the normal of <paramref name="coneTin"/> through
        /// <paramref name="coneTurn"/>, widened by <paramref name="slack"/> on
        /// both edges. Either side of the curve is accepted - the cone is mirror
        /// symmetric. A zero turn degenerates to normal +- slack, the smooth case.
        /// </summary>
        private static bool DbclChordInCone(
            double cx, double cy, Vector3d coneTin, double coneTurn, double slack)
        {
            double sideSign = coneTin.X * cy - coneTin.Y * cx >= 0.0 ? 1.0 : -1.0;
            double nInX = -coneTin.Y * sideSign, nInY = coneTin.X * sideSign;
            double delta = Math.Atan2(nInX * cy - nInY * cx, nInX * cx + nInY * cy);
            return delta >= Math.Min(0.0, coneTurn) - slack &&
                   delta <= Math.Max(0.0, coneTurn) + slack;
        }

        /// <summary>
        /// Fits one polyline of line and arc segments through the ordered samples,
        /// keeping every sample within <paramref name="tol"/> of its segment. At
        /// every start index the LONGEST primitive wins: the line reach and the arc
        /// reach are searched independently and the arc is taken only when it
        /// swallows clearly more samples - a line-first greedy would chop a
        /// large-radius arc into within-tolerance chords, and an arc-first one
        /// would round every sharp miter corner. Returns null for degenerate
        /// input. The caller owns the result.
        /// </summary>
        private static Polyline DbclFitPolyline(List<Point3d> pts, double tol)
        {
            //Collapse near-duplicates: at a sheet transition two consecutive
            //stations can land within millimetres of each other with a tiny
            //backtrack between them, which would force an unmergeable kink.
            List<Point3d> p = new List<Point3d>();
            foreach (Point3d q in pts)
                if (p.Count == 0 || p[p.Count - 1].DistanceHorizontalTo(q) > 0.01)
                    p.Add(q);
            if (p.Count < 2) return null;

            Polyline outPl = new Polyline();
            int i = 0;
            while (i < p.Count - 1)
            {
                int lineReach = DbclMaxReach(p, i, tol, false, out _);
                int arcReach = DbclMaxReach(p, i, tol, true, out double arcBulge);

                //An arc must prove itself: through any 3 points there is a circle,
                //so demand at least two interior samples AND a clear win over the
                //line before rounding anything.
                bool useArc = arcReach >= lineReach + 2 && arcReach - i >= 3;
                int reach = useArc ? arcReach : lineReach;
                double bulge = useArc ? arcBulge : 0.0;

                outPl.AddVertexAt(outPl.NumberOfVertices, p[i].To2d(), bulge, 0.0, 0.0);
                i = reach;
            }
            outPl.AddVertexAt(
                outPl.NumberOfVertices, p[p.Count - 1].To2d(), 0.0, 0.0, 0.0);
            return outPl;
        }

        /// <summary>
        /// Farthest index j &gt; i such that samples i..j fit a single primitive of
        /// the requested kind within <paramref name="tol"/> (exponential probe,
        /// then binary refine). Outputs the bulge of the winning fit.
        /// </summary>
        private static int DbclMaxReach(
            List<Point3d> p, int i, double tol, bool arc, out double bulge)
        {
            int reach = i + 1;
            bulge = 0.0;

            int probe = 1;
            while (reach + probe <= p.Count - 1 &&
                   DbclPrimitiveFits(p, i, reach + probe, tol, arc, out double b1))
            { reach += probe; bulge = b1; probe *= 2; }

            int lo = reach, hi = Math.Min(reach + probe, p.Count - 1);
            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (DbclPrimitiveFits(p, i, mid, tol, arc, out double b2))
                { lo = mid; bulge = b2; }
                else hi = mid;
            }
            return lo;
        }

        /// <summary>
        /// True when samples i..j (inclusive) fit a single line (arc = false) or a
        /// single circular arc (arc = true) from p[i] to p[j] within
        /// <paramref name="tol"/>; outputs the bulge.
        /// </summary>
        private static bool DbclPrimitiveFits(
            List<Point3d> p, int i, int j, double tol, bool arc, out double bulge)
        {
            bulge = 0.0;
            Point3d a = p[i], b = p[j];
            double abLen = a.DistanceHorizontalTo(b);
            if (abLen < 1e-9) return false;
            if (j - i == 1) return true;

            if (!arc)
            {
                double ux = (b.X - a.X) / abLen, uy = (b.Y - a.Y) / abLen;
                for (int k = i + 1; k < j; k++)
                {
                    double vx = p[k].X - a.X, vy = p[k].Y - a.Y;
                    double along = vx * ux + vy * uy;
                    double off = Math.Abs(vx * uy - vy * ux);
                    if (off > tol || along < -tol || along > abLen + tol) return false;
                }
                return true;
            }

            #region Arc through p[i], the middle sample, p[j]
            //Work relative to a: the circumcenter formula on absolute UTM-sized
            //coordinates cancels catastrophically for short spans (meters of
            //center error), while local coordinates keep full precision.
            Point3d m = p[(i + j) / 2];
            double mx = m.X - a.X, my = m.Y - a.Y;
            double bx = b.X - a.X, by = b.Y - a.Y;
            double det = 2.0 * (mx * by - my * bx);
            if (Math.Abs(det) < 1e-12) return false;
            double mm = mx * mx + my * my;
            double bb = bx * bx + by * by;
            double cx = a.X + (mm * by - bb * my) / det;
            double cy = a.Y + (bb * mx - mm * bx) / det;
            double radius = Math.Sqrt((a.X - cx) * (a.X - cx) + (a.Y - cy) * (a.Y - cy));

            double angA = Math.Atan2(a.Y - cy, a.X - cx);
            double angB = Math.Atan2(b.Y - cy, b.X - cx);
            double angM = Math.Atan2(m.Y - cy, m.X - cx);
            double sweepCcw = DbclNormalizeCcw(angB - angA);
            bool ccw = DbclNormalizeCcw(angM - angA) < sweepCcw;
            double sweep = ccw ? sweepCcw : sweepCcw - 2.0 * Math.PI;
            //The tracer never emits near-half-circle pieces; a fit that claims one
            //is a degenerate circumcircle, not the bisector.
            if (Math.Abs(sweep) > Math.PI * 1.5) return false;

            //Every sample on the circle, advancing monotonically along the sweep -
            //otherwise the circumcircle happens to pass through three points of a
            //shape that is not one arc.
            double prevT = 0.0;
            for (int k = i + 1; k < j; k++)
            {
                double rr = Math.Sqrt(
                    (p[k].X - cx) * (p[k].X - cx) + (p[k].Y - cy) * (p[k].Y - cy));
                if (Math.Abs(rr - radius) > tol) return false;
                double ak = Math.Atan2(p[k].Y - cy, p[k].X - cx);
                double t = ccw
                    ? DbclNormalizeCcw(ak - angA) / Math.Abs(sweep)
                    : DbclNormalizeCcw(angA - ak) / Math.Abs(sweep);
                if (t < prevT - 1e-9 || t > 1.0 + 1e-9) return false;
                prevT = t;
            }

            bulge = Math.Tan(sweep / 4.0);
            return true;
            #endregion
        }

        /// <summary>Angle wrapped to [0, 2*pi).</summary>
        private static double DbclNormalizeCcw(double ang)
        {
            while (ang < 0.0) ang += 2.0 * Math.PI;
            while (ang >= 2.0 * Math.PI) ang -= 2.0 * Math.PI;
            return ang;
        }
    }
}
