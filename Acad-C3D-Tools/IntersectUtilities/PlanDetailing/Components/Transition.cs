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
    internal class Transition : ComponentData
    {
        private readonly TransitionType transitionType;
        public Transition(Database db, Oid runId, Point3d location, TransitionType type)
            : base(db, runId, location)
        {
            blockName = type == TransitionType.X1 ? "RED KDLR" : "RED KDLR x2";
            transitionType = type;
        }
        internal override Result Validate()
        {
            Result result = base.Validate();

            using (Transaction tx = Db.TransactionManager.StartTransaction())
            {
                Polyline run = RunId.Go<Polyline>(tx);

                #region Test pipesystem, must be Steel
                if (PipeSystem != PipeSystemEnum.Stål)
                {
                    result.Status = ResultStatus.SoftError;
                    result.ErrorMsg = "Flex ledninger er ikke understøttet. Virker kun på stål.";
                }
                #endregion

                #region Test to see if point is on line, is not coincident and not start or end

                else if (run.GetDistToPoint(Location) > 0.000001)
                {
                    result.Status = ResultStatus.SoftError;
                    result.ErrorMsg = "Location is not on a pipe. Select location on pipe.";
                }
                else
                {
                    int idx = run.GetCoincidentIndexAtPoint(Location);

                    //Real idx is the idx the segment belongs to even if location is not on vertice
                    int realIdx = (int)run.GetParameterAtPoint(Location);

                    if (idx != -1)
                    {
                        result.Status = ResultStatus.SoftError;
                        result.ErrorMsg = "Location is a vertice! The location must NOT be a vertice.";
                    }
                    else if (run.GetSegmentType(realIdx) != SegmentType.Line)
                    {
                        result.Status = ResultStatus.SoftError;
                        result.ErrorMsg = "The segment is not a Line! Must be a straight Line segment.";
                    }
                    else if ((transitionType == TransitionType.X1 && Dn == 20) ||
                        (transitionType == TransitionType.X2 &&
                            (Dn == 20 || Dn == 25)))
                    {
                        result.Status = ResultStatus.SoftError;
                        result.ErrorMsg = "Cannot place transition on smallest size!";
                    }
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
                double rotation = Math.Atan2(seg1.Direction.Y, seg1.Direction.X)
                    + Math.PI / 2;

                BlockReference br = Db.CreateBlockWithAttributes(
                    blockName, Location, rotation);
                br.Layer = BlockLayerName;
                BrId = br.Id;

                //Type is NoUnits, must be set with a string
                //Construct type name
                int interval = transitionType == TransitionType.X1 ? 1 : 2;

                var dnList = ListAllDnsForPipeSystemTypeSerie(
                    PipeSystemEnum.Stål, GetPipeType(run), PipeSeriesEnum.S3)
                    .OrderByDescending(x => x).Reverse().ToList();

                int runDn = GetPipeDN(run);
                var runIdx = dnList.IndexOf(runDn);
                var reducedDnIdx = dnList.IndexOf(runDn) - interval;
                if (reducedDnIdx < 0)
                { throw new System.Exception($"Reducer got too low range: {runDn} asked to reduce {interval} step(s)"); }
                int reducedDn = dnList[reducedDnIdx];

                string type =
                    $"{runDn}" +
                    $"x" +
                    $"{reducedDn}";

                SetDynBlockPropertyObject(br, "Type", type);
                SetDynBlockPropertyObject(br, "System", PipeType.ToString());
                SetDynBlockPropertyObject(br, "Serie", PipeSerie.ToString());
                br.AttSync();
                //if (br.IsDynamicBlock)
                //{
                //    BlockTableRecord abtr = br.AnonymousBlockTableRecord.Go<BlockTableRecord>(tx);
                //    abtr.UpdateAnonymousBlocks();
                //}

                tx.Commit();

                Application.DocumentManager.MdiActiveDocument.Editor.Command("_REGEN");
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
        public enum TransitionType
        {
            X1,
            X2
        }
    }
}
