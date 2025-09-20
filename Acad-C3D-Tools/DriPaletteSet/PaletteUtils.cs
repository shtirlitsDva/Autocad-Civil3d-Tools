﻿using System;
using System.Diagnostics;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;
using Autodesk.Gis.Map;
using Autodesk.Gis.Map.ObjectData;
using Autodesk.Gis.Map.Constants;
using Autodesk.Gis.Map.Utilities;
using Autodesk.Aec.PropertyData;
using Autodesk.Aec.PropertyData.DatabaseServices;
using Autodesk.AutoCAD.Colors;
using static IntersectUtilities.PipeScheduleV2.PipeScheduleV2;
using IntersectUtilities.UtilsCommon;
using static IntersectUtilities.UtilsCommon.Utils;

using AcRx = Autodesk.AutoCAD.Runtime;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;
using ObjectId = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using DataType = Autodesk.Gis.Map.Constants.DataType;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using BlockReference = Autodesk.AutoCAD.DatabaseServices.BlockReference;
using ObjectIdCollection = Autodesk.AutoCAD.DatabaseServices.ObjectIdCollection;
using DBObject = Autodesk.AutoCAD.DatabaseServices.DBObject;
using ErrorStatus = Autodesk.AutoCAD.Runtime.ErrorStatus;
using PsDataType = Autodesk.Aec.PropertyData.DataType;
using IntersectUtilities;

namespace DriPaletteSet
{
    internal static class PaletteUtils
    {
        internal static PipeSeriesEnum CurrentSeries { get; set; }
        internal static PipeTypeEnum CurrentType { get; set; }
        internal static void ActivateLayer(PipeSystemEnum system, PipeTypeEnum type, string dn)
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Document doc = docCol.MdiActiveDocument;

            var pipeSystemString = GetSystemString(system);

            string layerName = string.Concat(
                    "FJV-", type.ToString(), "-", pipeSystemString, dn.ToString()).ToUpper();

            using (DocumentLock docLock = doc.LockDocument())
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    LayerTable lt = localDb.LayerTableId.Go<LayerTable>(tx);
                    Oid ltId;
                    if (!lt.Has(layerName))
                    {
                        LinetypeTable ltt = localDb.LinetypeTableId.Go<LinetypeTable>(tx);

                        LayerTableRecord ltr = new LayerTableRecord();
                        ltr.Name = layerName;

                        ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, GetLayerColor(system, type));

                        Oid continuous = ltt["Continuous"];
                        ltr.LinetypeObjectId = continuous;
                        ltr.LineWeight = LineWeight.ByLineWeightDefault;

                        //Make layertable writable
                        lt.CheckOrOpenForWrite();

                        //Add the new layer to layer table
                        ltId = lt.Add(ltr);
                        tx.AddNewlyCreatedDBObject(ltr, true);
                    }
                    else ltId = lt[layerName];

                    localDb.Clayer = ltId;

                    tx.Commit();
                }
                catch (System.Exception e)
                {
                    prdDbg(e.ToString() + "\n");
                    tx.Abort();
                    return;
                }

            }

            prdDbg(layerName + "\n");
        }
        internal static void UpdateWidths()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Document doc = docCol.MdiActiveDocument;
            Editor editor = docCol.MdiActiveDocument.Editor;

            using (DocumentLock docLock = doc.LockDocument())
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    HashSet<Polyline> pipes = localDb.GetFjvPipes(tx);
                    foreach (Polyline pipe in pipes)
                    {
                        double kappeOd = GetPipeKOd(pipe, CurrentSeries);
                        if (kappeOd < 0.1) continue;
                        pipe.CheckOrOpenForWrite();
                        pipe.ConstantWidth = kappeOd / 1000;
                    }

                    tx.Commit();
                }
                catch (System.Exception e)
                {
                    prdDbg(e.ToString() + "\n");
                    tx.Abort();
                    return;
                }
            }
        }
        internal static void ResetWidths()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Document doc = docCol.MdiActiveDocument;
            Editor editor = docCol.MdiActiveDocument.Editor;

            using (DocumentLock docLock = doc.LockDocument())
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    HashSet<Polyline> pipes = localDb.GetFjvPipes(tx);
                    foreach (Polyline pipe in pipes)
                    {
                        pipe.CheckOrOpenForWrite();
                        pipe.ConstantWidth = 0.0;
                    }

                    tx.Commit();
                }
                catch (System.Exception e)
                {
                    prdDbg(e.ToString() + "\n");
                    tx.Abort();
                    return;
                }
            }
        }
        public static void labelpipe()
        {

            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor ed = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;

            using (DocumentLock docLock = doc.LockDocument())
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    PromptEntityOptions promptEntityOptions1 = new PromptEntityOptions(
                        "\nSelect pipe (polyline) to label: ");
                    promptEntityOptions1.SetRejectMessage("\nNot a polyline!");
                    promptEntityOptions1.AddAllowedClass(typeof(Polyline), false);
                    PromptEntityResult entity1 = ed.GetEntity(promptEntityOptions1);
                    if (((PromptResult)entity1).Status != PromptStatus.OK) { tx.Abort(); return; }
                    Oid plineId = entity1.ObjectId;
                    Entity ent = plineId.Go<Entity>(tx);

                    string labelText = GetLabel(ent);

                    PromptPointOptions pPtOpts = new PromptPointOptions("\nChoose location of label: ");
                    PromptPointResult pPtRes = ed.GetPoint(pPtOpts);
                    Point3d selectedPoint = pPtRes.Value;
                    if (pPtRes.Status != PromptStatus.OK) { tx.Abort(); return; }

                    //Create new text
                    string layerName = "FJV-DIM";
                    LayerTable lt = tx.GetObject(localDb.LayerTableId, OpenMode.ForRead) as LayerTable;
                    if (!lt.Has(layerName))
                    {
                        LayerTableRecord ltr = new LayerTableRecord();
                        ltr.Name = layerName;
                        ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, 0);

                        lt.CheckOrOpenForWrite();
                        lt.Add(ltr);
                        tx.AddNewlyCreatedDBObject(ltr, true);
                    }

                    MText label = new MText();
                    label.SetDatabaseDefaults();
                    label.Attachment = AttachmentPoint.MiddleCenter;
                    label.Location = new Point3d(selectedPoint.X, selectedPoint.Y, 0);
                    label.TextHeight = 0.8;
                    label.Contents = labelText;
                    label.Layer = layerName;
                    label.BackgroundFill = true;
                    label.UseBackgroundColor = true;
                    label.BackgroundScaleFactor = 1.2;

                    //Find rotation
                    Polyline pline = (Polyline)ent;
                    Point3d closestPoint = pline.GetClosestPointTo(selectedPoint, true);
                    Vector3d derivative = pline.GetFirstDerivative(closestPoint);
                    double rotation = Math.Atan2(derivative.Y, derivative.X);
                    label.Rotation = rotation;

                    BlockTable bt = tx.GetObject(localDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord modelSpace =
                        tx.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                    Oid labelId = modelSpace.AppendEntity(label);
                    tx.AddNewlyCreatedDBObject(label, true);
                    label.Draw();

                    System.Windows.Forms.Application.DoEvents();

                    //Enable flipping of label
                    const string kwd1 = "Yes";
                    const string kwd2 = "No";
                    PromptKeywordOptions pkos = new PromptKeywordOptions("\nFlip label? ");
                    pkos.Keywords.Add(kwd1);
                    pkos.Keywords.Add(kwd2);
                    pkos.AllowNone = true;
                    pkos.Keywords.Default = kwd2;
                    PromptResult pkwdres = ed.GetKeywords(pkos);
                    string result = pkwdres.StringResult;

                    if (result == kwd1) label.Rotation += Math.PI;

                    #region Attach id data
                    PropertySetManager psm = new PropertySetManager(localDb, PSetDefs.DefinedSets.DriSourceReference);
                    PSetDefs.DriSourceReference driSourceReference = new PSetDefs.DriSourceReference();

                    string handle = ent.Handle.ToString();
                    psm.WritePropertyString(label, driSourceReference.SourceEntityHandle, handle);
                    #endregion
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    ed.WriteMessage("\n" + ex.ToString());
                    return;
                }
                tx.Commit();
            }
        }
    }
}
