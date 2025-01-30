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
    internal class Branch : ComponentData
    {
        private readonly Oid main;
        private readonly Oid branch;

        public Branch(Database db, Oid runId, Point3d location) : base(db, runId, location)
        {
            blockName = "BUEROR2";

            using (Transaction tx = Db.TransactionManager.StartTransaction())
            {
                var pipes = db.GetFjvPipes(tx);
                var twoNearest = pipes
                    .MinByEnumerable(x => location.DistanceHorizontalTo(
                        x.GetClosestPointTo(location, false)))
                    .Take(2);

                if (twoNearest.Count() != 2)
                    throw new System.Exception("twoNearest did not contain two pipes!");

                var first = twoNearest.First();
                var last = twoNearest.Last();

                //Determine run and branch
                double startParam = first
                    .GetParameterAtPoint(
                    first.GetClosestPointTo(location, false));

                tx.Commit();
            }
        }
        internal override Result Validate()
        {
            Result result = base.Validate();
            using (Transaction tx = Db.TransactionManager.StartTransaction())
            {
                Polyline run = RunId.Go<Polyline>(tx);

                #region Test if location is on polyline
                if (run.GetDistToPoint(Location) > 0.000001)
                {
                    result.Status = ResultStatus.SoftError;
                    result.ErrorMsg = "Location is not on a pipe. Select location on pipe.";
                    tx.Commit();
                    return result;
                }
                #endregion

                #region Test to see if segment is a buerør
                int idx = run.GetCoincidentIndexAtPoint(Location);

                if (idx == run.NumberOfVertices - 1)
                {
                    result.Status = ResultStatus.SoftError;
                    result.ErrorMsg = "The command does not accept end points. Yet...";
                    tx.Commit();
                    return result;
                }

                double realIdx = run.GetParameterAtPoint(Location);
                var segType = run.GetSegmentType((int)realIdx);

                if (segType != SegmentType.Arc)
                {
                    result.Status = ResultStatus.SoftError;
                    result.ErrorMsg = "Buerør can only be placed on arc segments!";
                    tx.Commit();
                    return result;
                }

                var seg = run.GetArcSegmentAt((int)realIdx);

                double actualRadius = seg.Radius;
                double minElasticRadius = GetPipeMinElasticRadiusHorizontalCharacteristic(run);
                double minBuerorRadius = GetBuerorMinRadius(run);

                //Check arc segment radius to be whithin bueror range
                if (actualRadius > minElasticRadius)
                {
                    result.Status = ResultStatus.SoftError;
                    result.ErrorMsg =
                        $"Arc's radius {actualRadius.ToString("#.##")} is larger than " +
                        $"minimum elastic radius ({minElasticRadius.ToString("#.##")})!";
                    tx.Commit();
                    return result;
                }
                if (actualRadius < minBuerorRadius)
                {
                    result.Status = ResultStatus.SoftError;
                    result.ErrorMsg =
                        $"Arc's radius {actualRadius.ToString("#.##")} is smaller than " +
                        $"minimum buerør radius ({minBuerorRadius.ToString("#.##")})!";
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

                int idx = (int)run.GetParamAtPointX(Location);
                double pipeStdLength = GetPipeStdLength(run);
                var arc = run.GetArcSegmentAt(idx);
                double radius = arc.Radius;
                double arcLength = run.GetLengthOfSegmentAt(idx);
                int nrOfPipes = (int)(arcLength / pipeStdLength) + 1;
                double lengthUpToArc = run.GetDistanceAtParameter((double)idx);

                //Determine if blocks must be mirrored
                var dir1 = run.GetFirstDerivative((double)idx);
                var dir2 = run.GetFirstDerivative((double)(idx + 1));
                var cp = dir1.CrossProduct(dir2);
                bool mustBeMirrored = false;
                if (cp.Z < 0.0) mustBeMirrored = true;

                for (int i = 0; i < nrOfPipes; i++)
                {
                    try
                    {
                        double curLength = lengthUpToArc + i * pipeStdLength;
                        double curPar = run.GetParamAtDist(curLength);
                        Vector3d dir = run.GetFirstDerivative(curPar);
                        Point3d curLoc = run.GetPointAtParam(curPar);
                        double rotation = Math.Atan2(dir.Y, dir.X);

                        BlockReference br = Db.CreateBlockWithAttributes(blockName, curLoc, rotation);
                        br.Layer = BlockLayerName;
                        BrId = br.Id;
                        SetDynBlockPropertyObject(br, "R", radius);
                        SetDynBlockPropertyObject(br, "Type", Dn.ToString());
                        SetDynBlockPropertyObject(br, "System", PipeType.ToString());
                        SetDynBlockPropertyObject(br, "Serie", PipeSerie.ToString());
                        if (i != nrOfPipes - 1)
                            SetDynBlockPropertyObject(br, "L", pipeStdLength);
                        else SetDynBlockPropertyObject(br, "L", arcLength % pipeStdLength);

                        if (mustBeMirrored) br.ScaleFactors = new Scale3d(1, -1, 1);
                        br.AttSync();
                    }
                    catch (System.Exception ex)
                    {
                        prdDbg(ex);
                        tx.Abort();
                        throw;
                    }
                }

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

                int idx = (int)run.GetParamAtPointX(Location);
                double p1 = run.GetParameterAtPoint(run.GetPoint3dAt(idx));
                double p2 = run.GetParameterAtPoint(run.GetPoint3dAt(++idx));

                CutPolylineWithDoublesToAccommodateBlock(run, new List<double> { p1, p2 });

                tx.Commit();
            }

            return result;
        }
    }
}
