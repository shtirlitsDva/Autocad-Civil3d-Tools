using System;
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
using IntersectUtilities.UtilsCommon.Enums;
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

namespace NSPaletteSet
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
                        double kappeOd = GetPipeKOd(pipe.Layer, CurrentSeries);
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
                MText? label = null;
                try
                {
                    PromptEntityOptions peo = new PromptEntityOptions(
                        "\nSelect pipe (polyline) to label: ");
                    peo.SetRejectMessage("\nNot a polyline!");
                    peo.AddAllowedClass(typeof(Polyline), false);
                    PromptEntityResult per = ed.GetEntity(peo);
                    if (per.Status != PromptStatus.OK) { tx.Abort(); return; }

                    Polyline pline = per.ObjectId.Go<Polyline>(tx);

                    string labelText = GetLabel(pline);
                    if (string.IsNullOrEmpty(labelText)) { tx.Abort(); return; }

                    // FJV-DIM layer must exist before the jig starts, because the
                    // MText sets its Layer property during preview rendering.
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

                    label = new MText();
                    label.SetDatabaseDefaults();
                    label.Attachment = AttachmentPoint.MiddleCenter;
                    label.TextHeight = 0.8;
                    label.Contents = labelText;
                    label.Layer = layerName;
                    label.BackgroundFill = true;
                    label.UseBackgroundColor = true;
                    label.BackgroundScaleFactor = 1.2;

                    LabelJig jig = new LabelJig(label, pline);

                    PromptResult dragResult;
                    while (true)
                    {
                        dragResult = ed.Drag(jig);
                        if (dragResult.Status == PromptStatus.Keyword)
                        {
                            if (string.Equals(dragResult.StringResult, "Flip",
                                    StringComparison.OrdinalIgnoreCase))
                                jig.ToggleFlip();
                            continue;
                        }
                        break;
                    }

                    if (dragResult.Status != PromptStatus.OK)
                    {
                        label.Dispose();
                        label = null;
                        tx.Abort();
                        return;
                    }

                    BlockTable bt = tx.GetObject(localDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord modelSpace =
                        tx.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                    modelSpace.AppendEntity(label);
                    tx.AddNewlyCreatedDBObject(label, true);

                    PropertySetManager psm = new PropertySetManager(
                        localDb, PSetDefs.DefinedSets.DriSourceReference);
                    PSetDefs.DriSourceReference driSourceReference = new PSetDefs.DriSourceReference();
                    psm.WritePropertyString(label, driSourceReference.SourceEntityHandle,
                        pline.Handle.ToString());

                    tx.Commit();
                }
                catch (System.Exception ex)
                {
                    label?.Dispose();
                    tx.Abort();
                    ed.WriteMessage("\n" + ex.ToString());
                }
            }
        }

        private sealed class LabelJig : EntityJig
        {
            private readonly Polyline _pline;
            private Point3d _cursor;
            private double _flipOffset;

            public LabelJig(MText label, Polyline pline) : base(label)
            {
                _pline = pline;
                _cursor = pline.StartPoint;
            }

            protected override SamplerStatus Sampler(JigPrompts prompts)
            {
                JigPromptPointOptions opts = new JigPromptPointOptions(
                    "\nChoose location of label [Flip]: ");
                opts.Keywords.Add("Flip");
                opts.UserInputControls = UserInputControls.AcceptOtherInputString;

                PromptPointResult res = prompts.AcquirePoint(opts);

                if (res.Status == PromptStatus.Keyword)
                    return SamplerStatus.OK;

                if (res.Status != PromptStatus.OK)
                    return SamplerStatus.Cancel;

                if (_cursor.IsEqualTo(res.Value, Tolerance.Global))
                    return SamplerStatus.NoChange;

                _cursor = res.Value;
                return SamplerStatus.OK;
            }

            protected override bool Update()
            {
                MText lbl = (MText)Entity;
                Point3d closest = _pline.GetClosestPointTo(_cursor, true);
                Vector3d d = _pline.GetFirstDerivative(closest);
                lbl.Location = new Point3d(_cursor.X, _cursor.Y, 0);
                lbl.Rotation = Math.Atan2(d.Y, d.X) + _flipOffset;
                return true;
            }

            public void ToggleFlip() => _flipOffset = (_flipOffset == 0.0) ? Math.PI : 0.0;
        }
    }
}
