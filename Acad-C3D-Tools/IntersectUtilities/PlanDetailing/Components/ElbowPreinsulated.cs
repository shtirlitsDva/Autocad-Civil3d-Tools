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

namespace IntersectUtilities.PlanDetailing.Components
{
    internal class ElbowPreinsulated : ComponentData
    {
        private readonly string blockNameTwin = "PRÆBØJN-90GR-TWIN-GLD";
        private readonly string blockNameEnkelt = "PRÆBØJN 90GR ENKELT";

        public ElbowPreinsulated(Database db, Oid runId, Point3d location) : base(db, runId, location)
        {
            blockName = PipeType == PipeTypeEnum.Twin ? blockNameTwin : blockNameEnkelt;
        }
        internal override Result Validate()
        {
            Result result = base.Validate();

            using (Transaction tx = Db.TransactionManager.StartTransaction())
            {
                Polyline run = RunId.Go<Polyline>(tx);

                #region Test to see if point coincides with a vertice or at ends
                int idx = run.GetCoincidentIndexAtPoint(Location);

                if (idx == -1)
                {
                    result.Status = ResultStatus.SoftError;
                    result.ErrorMsg = "Location not a vertice! The location must be a vertice.";
                }

                else if (idx == 0 || idx == run.NumberOfVertices - 1)
                {
                    result.Status = ResultStatus.SoftError;
                    result.ErrorMsg = "The command does not accept start or end points. Yet...";
                }
                #endregion
                else
                #region Test to see if adjacent segments are lines

                {
                    SegmentType st1 = run.GetSegmentType(idx);
                    if (st1 != SegmentType.Line)
                    {
                        result.Status = ResultStatus.SoftError;
                        result.ErrorMsg = "Next segment is not a Line!";
                    }
                    SegmentType st2 = run.GetSegmentType(idx - 1);
                    if (st2 != SegmentType.Line)
                    {
                        result.Status = ResultStatus.SoftError;
                        result.ErrorMsg = "Previous segment is not a Line!";
                    }

                    #region Test to see if the angle is 90°
                    LineSegment2d seg1 = run.GetLineSegment2dAt(idx);
                    LineSegment2d seg2 = run.GetLineSegment2dAt(idx - 1);

                    double angle = seg1.Direction.GetAngleTo(seg2.Direction).ToDegrees();

                    if (!angle.Equalz(90.0, 0.00001))
                    {
                        result.Status = ResultStatus.SoftError;
                        result.ErrorMsg =
                            $"The bend is not exactly 90°! But actually {angle}.";
                    }
                    #endregion
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
                int idx = run.GetCoincidentIndexAtPoint(Location);

                LineSegment3d seg1 = run.GetLineSegmentAt(idx);
                LineSegment3d seg2 = run.GetLineSegmentAt(idx - 1);
                double rotation = Math.Atan2(seg1.Direction.Y, seg1.Direction.X);

                BlockReference br = Db.CreateBlockWithAttributes(blockName, Location, rotation);
                br.Layer = BlockLayerName;
                BrId = br.Id;

                var cp = seg1.Direction.CrossProduct(seg2.Direction);
                if (cp.Z.Equalz(-1, 0.000001))
                    SetDynBlockProperty(br, "Flip-H", "1");

                //DN is NoUnits, must be set with a string
                SetDynBlockPropertyObject(br, "DN", Dn.ToString());
                if (PipeType == PipeTypeEnum.Twin)
                    SetDynBlockPropertyObject(br, "Serie", PipeSerie.ToString());
                br.AttSync();
                tx.Commit();
            }

            if (result.Status == ResultStatus.OK) result = this.Cut(result);
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
