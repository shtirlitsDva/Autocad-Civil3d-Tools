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
using static IntersectUtilities.Transition;

namespace IntersectUtilities
{
    internal abstract class ComponentData
    {
        internal readonly Oid RunId;
        internal readonly Point3d Location;
        internal readonly Database Db;
        internal readonly string BlockDb = @"X:\AutoCAD DRI - 01 Civil 3D\DynBlokke\Symboler.dwg";
        internal readonly string BlockLayerName = "0-KOMPONENT";
        internal readonly int Dn;
        internal readonly PipeSystemEnum PipeSystem; //Stål, Cu, Alu osv.
        protected string cutBlockName = "MuffeIntern";
        protected string blockName;
        private PipeTypeEnum _pipeType; //Twin, Frem, Retur
        internal PipeTypeEnum PipeType
        {
            get => _pipeType;
            set
            {
                if (value == PipeTypeEnum.Frem || value == PipeTypeEnum.Retur)
                    _pipeType = PipeTypeEnum.Enkelt;
                else _pipeType = value;
            }
        } //Twin, Frem, Retur
        internal PipeSeriesEnum PipeSerie;
        internal Oid BrId;
        internal bool Valid = false;
        //private DataTable Data;
        public ComponentData(Database db, Oid runId, Point3d location)
        {
            RunId = runId;
            Location = location;
            Db = db;

            using (Transaction tx = Db.TransactionManager.StartTransaction())
            {
                Polyline run = RunId.Go<Polyline>(tx);
                Dn = GetPipeDN(run);
                PipeSystem = GetPipeSystem(run);
                PipeType = GetPipeType(run);
                PipeSerie = GetPipeSeriesV2(run, true);
                tx.Commit();
            }

        }
        
        internal virtual Result Validate()
        {
            Result result = new Result();

            //Test BlockDb
            if (!File.Exists(BlockDb))
                throw new System.Exception("ComponentData cannot access " + BlockDb + "!");

            using (Transaction tx = Db.TransactionManager.StartTransaction())
            {
                Polyline run = RunId.Go<Polyline>(tx);
                //Validate presence of layer where to place blocks
                Db.CheckOrCreateLayer(BlockLayerName, 0);

                #region Test pipe properties
                //Test Dn
                if (Dn == 999)
                {
                    result.Status = ResultStatus.SoftError;
                    result.ErrorMsg = $"Pipe {run.Handle} fails to report correct DN!";
                }

                //Test pipe system
                if (PipeSystem == PipeSystemEnum.Ukendt)
                {
                    result.Status = ResultStatus.SoftError;
                    result.ErrorMsg = $"Pipe {run.Handle} fails to report correct PipeSystem (Stål, AluFlex osv.)!";
                }

                //Test pipe type
                if (PipeType == PipeTypeEnum.Ukendt)
                {
                    result.Status = ResultStatus.SoftError;
                    result.ErrorMsg = $"Pipe {run.Handle} fails to report correct PipeType (Twin/Enkelt)!";
                }

                //Test pipe series
                if (PipeSerie == PipeSeriesEnum.Undefined)
                {
                    result.Status = ResultStatus.SoftError;
                    result.ErrorMsg = $"Pipe {run.Handle} fails to report correct PipeSerie!";
                }
                #endregion

                #region Test to see if block is present in DB or import
                Db.CheckOrImportBlockRecord(BlockDb, blockName);
                #endregion

                #region Check to see if present block is latest version
                CheckIfBlockPresentInDrawingIsLatestVersion(tx, blockName);
                #endregion

                #region Check number of cutblocks in BTR
                CheckNumberOfNestedBlocks(tx, blockName, cutBlockName, 2);
                #endregion

                tx.Commit();
            }

            return result;
        }
        internal virtual Result Place()
        {
            Result result = new Result();
            if (!Valid)
            {
                result.Status = ResultStatus.SoftError;
                result.ErrorMsg = "Component has not been validated before placing!";
                return result;
            }
            return result;
        }
        internal virtual Result Cut(Result result)
        {
            throw new NotImplementedException();
        }
        internal void CheckIfBlockPresentInDrawingIsLatestVersion(Transaction tx, string blockName)
        {
            Result result = new Result();

            //Test to see if DynBlock csv is accessible
            string pathToCatalogue =
                @"X:\AutoCAD DRI - 01 Civil 3D\FJV Dynamiske Komponenter.csv";
            if (!File.Exists(pathToCatalogue))
                throw new System.Exception("ComponentData cannot access " + pathToCatalogue + "!");

            DataTable dt = CsvReader.ReadCsvToDataTable(pathToCatalogue, "DynKomps");

            var btr = Db.GetBlockTableRecordByName(blockName);

            #region Read present block version
            string version = "";
            foreach (Oid oid in btr)
            {
                if (oid.IsDerivedFrom<AttributeDefinition>())
                {
                    var atdef = oid.Go<AttributeDefinition>(tx);
                    if (atdef.Tag == "VERSION") { version = atdef.TextString; break; }
                }
            }
            if (version.IsNoE()) version = "1";
            if (version.Contains("v")) version = version.Replace("v", "");
            int blockVersion = Convert.ToInt32(version);
            #endregion

            #region Determine latest version
            var query = dt.AsEnumerable()
                    .Where(x => x["Navn"].ToString() == blockName)
                    .Select(x => x["Version"].ToString())
                    .Select(x => { if (x == "") return "1"; else return x; })
                    .Select(x => Convert.ToInt32(x.Replace("v", "")))
                    .OrderBy(x => x);

            if (query.Count() == 0)
                throw new System.Exception($"Block {blockName} is not present in FJV Dynamiske Komponenter.csv!");
            int maxVersion = query.Max();
            #endregion

            if (maxVersion != blockVersion)
                throw new System.Exception(
                    $"Block {blockName} v{blockVersion} is not latest version v{maxVersion}! " +
                    $"Update with latest version from:\n" +
                    $"{BlockDb}\n" +
                    $"WARNING! This can break existing blocks! Caution is advised!");
        }
        internal void CheckNumberOfNestedBlocks(
            Transaction tx, string blockName, string nestedBlockName, int expectedNumber)
        {
            BlockTable bt = Db.BlockTableId.Go<BlockTable>(tx);
            BlockTableRecord btr = bt[blockName].Go<BlockTableRecord>(tx);
            var blocks = btr.GetNestedBlocksByName(nestedBlockName);
            if (blocks.Length != expectedNumber)
                throw new System.Exception(
                    $"BlockTableRecord {btr.Name} has unexpected " +
                    $"number ({blocks.Length}) of {nestedBlockName}!");
        }
        internal void CutPolylineWithBlocksToAccommodateBlock(
            Transaction tx, Polyline run, BlockReference br, string cutBlockName)
        {
            BlockTableRecord btr = br.AnonymousBlockTableRecord.Go<BlockTableRecord>(tx);

            var muffer = btr.GetNestedBlocksByName(cutBlockName);

            List<double> splitPts = new List<double>();
            foreach (BlockReference muffe in muffer)
            {
                Point3d pt = muffe.Position.TransformBy(br.BlockTransform);
                splitPts.Add(
                    run.GetParameterAtPoint(
                        run.GetClosestPointTo(pt, false)));
            }

            splitPts.Sort();

            try
            {
                DBObjectCollection objs = run
                    .GetSplitCurves(new DoubleCollection(splitPts.ToArray()));

                if (objs.Count != 3) throw new System.Exception(
                    $"Unexpected number ({objs.Count}) of split curves for polyline {run.Handle}!");

                for (int i = 0; i < 3; i++)
                {
                    if (i == 1) continue;
                    Polyline pl = objs[i] as Polyline;

                    pl.AddEntityToDbModelSpace(Db);
                    pl.Layer = run.Layer;
                    pl.ConstantWidth = run.ConstantWidth;
                    PropertySetManager.CopyAllProperties(run, pl);
                }

                run.CheckOrOpenForWrite();
                run.Erase(true);
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                Application.ShowAlertDialog(ex.Message + "\n" + ex.StackTrace);
                throw new System.Exception("Splitting of pline failed!");
            }
        }
        internal void CutPolylineWithDoublesToAccommodateBlock(
            Polyline run, List<double> splitPts)
        {
            try
            {
                DBObjectCollection objs = run
                    .GetSplitCurves(new DoubleCollection(splitPts.ToArray()));

                if (objs.Count != 3) throw new System.Exception(
                    $"Unexpected number ({objs.Count}) of split curves for polyline {run.Handle}!");

                for (int i = 0; i < 3; i++)
                {
                    if (i == 1) continue;
                    Polyline pl = objs[i] as Polyline;

                    pl.AddEntityToDbModelSpace(Db);
                    pl.Layer = run.Layer;
                    pl.ConstantWidth = run.ConstantWidth;
                    PropertySetManager.CopyAllProperties(run, pl);
                }

                run.CheckOrOpenForWrite();
                run.Erase(true);
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                Application.ShowAlertDialog(ex.Message + "\n" + ex.StackTrace);
                throw new System.Exception("Splitting of pline failed!");
            }
        }
    }
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

                    double angle = seg1.Direction.GetAngleTo(seg2.Direction).ToDegrees();
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

            //A workaround for parametric geometry not updating inside command,
            //Application.DocumentManager.MdiActiveDocument.Editor.Command(
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
                //CutPolylineWithBlocksToAccommodateBlock(tx, run, br, cutBlockName);
                double angle = Convert.ToDouble(
                    br.ReadDynamicPropertyValue("Vinkel"), CultureInfo.InvariantCulture);
                double ll = Math.Tan(angle.ToRadians() / 2) * radiusDict[Dn];

                int idx = run.GetCoincidentIndexAtPoint(Location);

                double l1 = run.GetLengthOfSegmentAt(idx);
                double p1 = (double)idx + ll / l1;

                double l2 = run.GetLengthOfSegmentAt(idx - 1);
                double p2 = (double)idx - ll / l2;

                CutPolylineWithDoublesToAccommodateBlock(run, new List<double> { p2, p1 });
                tx.Commit();
            }

            return result;
        }
    }
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
                    .OrderByDescending(x => x).ToList();

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
                if (br.IsDynamicBlock)
                {
                    BlockTableRecord abtr = br.AnonymousBlockTableRecord.Go<BlockTableRecord>(tx);
                    abtr.UpdateAnonymousBlocks();
                }

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
        public enum TransitionType
        {
            X1,
            X2
        }
    }
    internal class Bueror : ComponentData
    {
        public Bueror(Database db, Oid runId, Point3d location) : base(db, runId, location) 
        {
            blockName = "BUEROR2";
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
                double minElasticRadius = GetPipeMinElasticRadiusCharacteristic(run);
                double minBuerorRadius = AskForBuerorMinRadius(run);

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
                //double pipeStdLength = GetPipeStdLength(run);
                double pipeStdLength = 12; //Alle buerør er 12m
                var arc = run.GetArcSegmentAt(idx);
                double radius = arc.Radius;
                double arcLength = run.GetLengthOfSegmentAt(idx);
                int nrOfPipes = (int)(arcLength / pipeStdLength) + 1;
                double lengthUpToArc = run.GetDistanceAtParameter(idx);

                //Determine if blocks must be mirrored
                var dir1 = run.GetFirstDerivative(idx);
                var dir2 = run.GetFirstDerivative((idx + 1));
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
                double minElasticRadius = GetPipeMinElasticRadiusCharacteristic(run);
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
    internal class PertTee : ComponentData
    {
        public PertTee(Database db, Oid runId, Point3d location)
            : base(db, runId, location)
        {
            blockName = "PRESKOBLING-TEE-PRT";
            cutBlockName = "MuffeIntern-MAIN";
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
