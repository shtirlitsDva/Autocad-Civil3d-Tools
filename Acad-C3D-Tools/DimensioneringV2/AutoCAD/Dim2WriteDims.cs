using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using IntersectUtilities.UtilsCommon.Enums;
using static IntersectUtilities.UtilsCommon.Utils;
using IntersectUtilities.NTS;
using IntersectUtilities.PlanDetailing;

using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;

using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;

using NetTopologySuite.Geometries;

using DimensioneringV2.GraphFeatures;
using IntersectUtilities.UtilsCommon;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Colors;
using IntersectUtilities.PipeScheduleV2;
using NorsynHydraulicCalc.Pipes;

namespace DimensioneringV2.AutoCAD
{
    internal static class Dim2WriteDims
    {
        internal static void Write(IEnumerable<AnalysisFeature> features)
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            string dbFilename = localDb.Filename;
            string basePath = Path.GetDirectoryName(dbFilename);
            basePath += "\\";
            string dimDbFilename = basePath + "Fjernvarme DIM.dwg";

            using Database dimDb = new Database(true, true);
            using Transaction dimTx = dimDb.TransactionManager.StartTransaction();

            try
            {
                //Modify the standard text style to have Arial font
                TextStyleTable tst = dimDb.TextStyleTableId.Go<TextStyleTable>(dimTx);
                if (tst.Has("Standard"))
                {
                    TextStyleTableRecord tsr = tst["Standard"]
                        .Go<TextStyleTableRecord>(dimTx, OpenMode.ForWrite);
                    tsr.FileName = "arial.ttf";
                }

                //Preapare for linetype creation
                LinetypeTable ltt = dimDb.LinetypeTableId.Go<LinetypeTable>(dimTx, (OpenMode)1);

                foreach (AnalysisFeature feature in features)
                {
                    if (feature.Dim.NominalDiameter == 0) continue;

                    var lineString = feature.Geometry as LineString;
                    if (lineString == null)
                    {
                        prdDbg($"Feature is not a LineString! Check your geometry.");
                        continue;
                    }

                    var pline = NTSConversion.ConvertNTSLineStringToPline(lineString);
                    pline.AddEntityToDbModelSpace(dimDb);

                    PipeSystemEnum ps = TranslatePipeTypeToSystem(feature.Dim.PipeType);

                    #region Handle line type generation
                    string lineTypeText =
                        PipeScheduleV2.GetLineTypeLayerPrefix(ps) +
                        feature.Dim.NominalDiameter.ToString();

                    string lineTypeName = "LT-" + lineTypeText;

                    if (!ltt.Has(lineTypeName))
                    {
                        IntersectUtilities.PlanDetailing.LineTypes.LineTypes.createltmethod(
                            lineTypeName, lineTypeText, "Standard", dimDb);
                    }
                    #endregion

                    //Determine twin or single pipe
                    PipeTypeEnum pt = PipeScheduleV2.GetPipeTypeByAvailability(ps, feature.Dim.NominalDiameter);

                    //Determine layer
                    string systemString = PipeScheduleV2.GetSystemString(ps);
                    string layerName = string.Concat(
                        "FJV-", pt, "-", systemString, feature.Dim.NominalDiameter).ToUpper();

                    CheckOrCreateLayerForPipe(dimDb, layerName, ps, pt);

                    pline.Layer = layerName;
                    pline.ConstantWidth = PipeScheduleV2.GetPipeKOd(
                        ps, feature.Dim.NominalDiameter, pt, PipeSeriesEnum.S3) / 1000;
                    pline.LinetypeId = ltt[lineTypeName];
                    pline.Plinegen = true;
                }
            }
            catch (Exception ex)
            {
                prdDbg(ex);
                dimTx.Abort();
                return;
            }

            dimTx.Commit();
            dimDb.SaveAs(dimDbFilename, DwgVersion.Current);
        }

        private static PipeSystemEnum TranslatePipeTypeToSystem(NorsynHydraulicCalc.PipeType type)
        {
            switch (type)
            {
                case NorsynHydraulicCalc.PipeType.Stål:
                    return PipeSystemEnum.Stål;
                case NorsynHydraulicCalc.PipeType.PertFlextraFL:
                case NorsynHydraulicCalc.PipeType.PertFlextraSL:
                    return PipeSystemEnum.PertFlextra;
                case NorsynHydraulicCalc.PipeType.AluPEXSL:
                case NorsynHydraulicCalc.PipeType.AluPEXFL:
                    return PipeSystemEnum.AluPex;
                case NorsynHydraulicCalc.PipeType.Kobber:
                    return PipeSystemEnum.Kobberflex;
                case NorsynHydraulicCalc.PipeType.AquaTherm11:
                    return PipeSystemEnum.AquaTherm11;
                case NorsynHydraulicCalc.PipeType.Pe:
                    return PipeSystemEnum.PE;
                default:
                    throw new Exception($"Unknown pipe type {type}!\nAdd case to the switch!");
            }
        }

        private static void CheckOrCreateLayerForPipe(Database db, string layerName, PipeSystemEnum localSystem, PipeTypeEnum localType)
        {
            Transaction localTx = db.TransactionManager.TopTransaction;
            LayerTable lt = db.LayerTableId.Go<LayerTable>(localTx);
            Oid ltId;
            if (!lt.Has(layerName))
            {
                LinetypeTable ltt = db.LinetypeTableId.Go<LinetypeTable>(localTx);

                LayerTableRecord ltr = new LayerTableRecord();
                ltr.Name = layerName;
                short color = PipeScheduleV2.GetLayerColor(localSystem, localType);
                ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, color);
                Oid continuous = ltt["Continuous"];
                ltr.LinetypeObjectId = continuous;
                ltr.LineWeight = LineWeight.ByLineWeightDefault;

                //Make layertable writable
                lt.CheckOrOpenForWrite();

                //Add the new layer to layer table
                ltId = lt.Add(ltr);
                localTx.AddNewlyCreatedDBObject(ltr, true);
            }
            else ltId = lt[layerName];
        }
    }
}