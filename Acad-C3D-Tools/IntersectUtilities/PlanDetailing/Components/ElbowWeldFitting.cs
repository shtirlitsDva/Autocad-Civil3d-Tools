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
    internal class ElbowWeldFitting : ComponentData
    {
        private readonly Dictionary<int, double> radiusDict = new Dictionary<int, double>()
        {
            { 20, 0.037 },
            { 25, 0.038 },
            { 32, 0.048 },
            { 40, 0.057 },
            { 50, 0.076 },
            { 65, 0.095 },
            { 80, 0.114 },
            { 100, 0.152 },
            { 125, 0.19 },
            { 150, 0.229 },
            { 200, 0.305 },
            { 250, 0.381 },
            { 300, 0.457 },
            { 350, 0.533 },
            { 400, 0.61 },
            { 450, 0.686 },
            { 500, 0.762 },
            { 600, 0.914 },
        };
        public ElbowWeldFitting(Database db, Oid runId, Point3d location) : base(db, runId, location)
        {
            blockName = "BØJN KDLR v2";
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
                    #endregion

                    #region Test to see if bend is between 80° and 10° DISABLED
                    LineSegment3d seg1 = run.GetLineSegmentAt(idx);
                    LineSegment3d seg2 = run.GetLineSegmentAt(idx - 1);

                    double angle = seg1.Direction.GetAngleTo(seg2.Direction).ToDeg();
                    prdDbg(angle);

                    //if (angle > 80.0 || angle < 10.0)
                    //{
                    //    result.Status = ResultStatus.SoftError;
                    //    result.ErrorMsg =
                    //        $"The bend is not between 80° and 10°! But actually {angle}.";
                    //}
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
                double rotation = Math.Atan2(seg1.Direction.Y, seg1.Direction.X)
                    + Math.PI;
                double angle = seg1.Direction.GetAngleTo(seg2.Direction);

                try
                {
                    BlockReference br = Db.CreateBlockWithAttributes(blockName, Location, rotation);
                    br.Layer = BlockLayerName;
                    BrId = br.Id;
                    SetDynBlockPropertyObject(br, "Vinkel", angle);
                    SetDynBlockPropertyObject(br, "DN", Dn);
                    var cp = seg1.Direction.CrossProduct(seg2.Direction);
                    if (cp.Z < 0.0)
                        br.ScaleFactors = new Scale3d(1, -1, 1);
                    SetDynBlockPropertyObject(br, "System", PipeType.ToString());
                    SetDynBlockPropertyObject(br, "Serie", PipeSerie.ToString());
                    br.AttSync();
                }
                catch (System.Exception)
                {
                    tx.Abort();
                    throw;
                }

                tx.Commit();
            }

            //Application.DocumentManager.MdiActiveDocument.Editor.

            //A workaround for parametric geometry not updating inside command,
            Application.DocumentManager.MdiActiveDocument.Editor.Command(
                "_regen");
            //    "-PAN", new Point3d(), new Point3d());

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
                //double angle = Convert.ToDouble(
                //    br.ReadDynamicPropertyValue("Vinkel"), CultureInfo.InvariantCulture);
                //double ll = Math.Tan(angle.ToRadians() / 2) * radiusDict[Dn];

                //int idx = run.GetCoincidentIndexAtPoint(Location);

                //double l1 = run.GetLengthOfSegmentAt(idx);
                //double p1 = (double)idx + ll / l1;

                //double l2 = run.GetLengthOfSegmentAt(idx - 1);
                //double p2 = (double)idx - ll / l2;

                //CutPolylineWithDoublesToAccommodateBlock(run, new List<double> { p2, p1 });
                tx.Commit();
            }

            return result;
        }
    }
}
