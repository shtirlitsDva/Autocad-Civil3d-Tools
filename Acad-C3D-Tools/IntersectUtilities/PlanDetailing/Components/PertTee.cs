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
using static IntersectUtilities.PipeScheduleV2.PipeScheduleV2;

using static IntersectUtilities.UtilsCommon.Utils;
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
using DataTable = System.Data.DataTable;
using IntersectUtilities;
using IntersectUtilities.UtilsCommon.Enums;

namespace IntersectUtilities.PlanDetailing.Components
{
    internal class PertTee : ComponentData
    {
        private Oid stikPlId;
        private Polyline stikPl = null;

        public PertTee(Database db, Oid runId, Oid stikPlId, Point3d location)
            : base(db, runId, location)
        {
            blockName = "PRESKOBLING-TEE-PRT";
            cutBlockName = "MuffeIntern-MAIN";
            this.stikPlId = stikPlId;

            if (stikPlId != Oid.Null)
            {
                using (Transaction tx = db.TransactionManager.StartTransaction())
                {
                    stikPl = stikPlId.Go<Polyline>(tx);
                    tx.Commit();
                }
            }

        }
        internal override Result Validate()
        {
            Result result = base.Validate();
            using (Transaction tx = Db.TransactionManager.StartTransaction())
            {
                Polyline run = RunId.Go<Polyline>(tx);

                #region Test if location is on polyline
                if (run.GetDistToPoint(Location) > 0.0001)
                {
                    result.Status = ResultStatus.SoftError;
                    result.ErrorMsg = "Location is not on a pipe. Select location on pipe.";
                    tx.Commit();
                    return result;
                }
                #endregion

                #region Test pipesystem, must be PertFlextra
                if (PipeSystem != PipeSystemEnum.PertFlextra)
                {
                    result.Status = ResultStatus.SoftError;
                    result.ErrorMsg = "Skal bruges på PertFlextra ledninger!";
                    tx.Commit();
                    return result;
                }
                #endregion

                #region Test to see if segment is a line
                double realIdx = run.GetParameterAtPoint(Location);
                var segType = run.GetSegmentType((int)realIdx);

                if (segType != SegmentType.Line)
                {
                    result.Status = ResultStatus.SoftError;
                    result.ErrorMsg =
                        "PertTee can only be placed automatically on line segments!\n" +
                        "On arc segments do it manually.";
                    tx.Commit();
                    return result;
                }
                #endregion

                tx.Commit();
            }

            if (result.Status == ResultStatus.OK) { Valid = true; }
            return result;
        }
        internal override Result Place()
        {
            Result result = base.Place();
            if (result.Status != ResultStatus.OK) { return result; }

            using (Transaction tx = Db.TransactionManager.StartTransaction())
            {
                Polyline run = RunId.Go<Polyline>(tx);

                int idxReal = (int)run.GetParameterAtPoint(Location);

                LineSegment3d seg1 = run.GetLineSegmentAt(idxReal);
                double rotation = Math.Atan2(
                    seg1.Direction.Y, seg1.Direction.X);// + Math.PI / 2;

                BlockReference br = Db.CreateBlockWithAttributes(
                    blockName, Location, rotation);
                br.Layer = BlockLayerName;
                BrId = br.Id;

                int runDn = GetPipeDN(run);

                string type =
                    $"{runDn}" +
                    $"x" +
                    $"25";

                SetDynBlockPropertyObject(br, "Type", type);
                SetDynBlockPropertyObject(br, "System", PipeType.ToString());
                //SetDynBlockPropertyObject(br, "Serie", PipeSerie.ToString());
                br.AttSync();
                if (br.IsDynamicBlock)
                {
                    BlockTableRecord abtr = br.AnonymousBlockTableRecord.Go<BlockTableRecord>(tx);
                    abtr.UpdateAnonymousBlocks();
                }

                PropertySetManager.CopyAllProperties(run, br);

                tx.Commit();
            }
            if (result.Status == ResultStatus.OK) result = Cut(result);

            return result;
        }
        internal override Result Cut(Result result)
        {
            using (Transaction tx = Db.TransactionManager.StartTransaction())
            {
                Polyline run = RunId.Go<Polyline>(tx);
                BlockReference br = BrId.Go<BlockReference>(tx);
                CutPolylineWithBlocksToAccommodateBlock(tx, run, br, cutBlockName);
                tx.Commit();
            }

            return result;
        }
    }
}
