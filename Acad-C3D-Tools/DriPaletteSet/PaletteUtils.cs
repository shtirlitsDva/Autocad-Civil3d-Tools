using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using MoreLinq;
using System.Text;
using System.Text.RegularExpressions;
using System.Data;
using System.Globalization;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using System.Xml.Serialization;
using Autodesk.Aec.DatabaseServices;
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
using static IntersectUtilities.PipeSchedule;
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

namespace DriPaletteSet
{
    internal static class PaletteUtils
    {
        internal static PipeSeriesEnum CurrentSeries { get; set; }
        internal static void ActivateLayer(PipeTypeEnum pipeType, PipeDnEnum pipeDn)
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Document doc = docCol.MdiActiveDocument;
            Editor editor = docCol.MdiActiveDocument.Editor;

            string layerName = string.Concat(
                "FJV-", pipeType.ToString(), "-", pipeDn.ToString()).ToUpper();

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
                        ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, 6);
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
        
    }
}
