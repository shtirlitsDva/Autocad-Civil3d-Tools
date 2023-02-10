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
using static IntersectUtilities.PipeSchedule;

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
using Autodesk.AutoCAD.MacroRecorder;

namespace IntersectUtilities
{
    internal abstract class ComponentData
    {
        internal readonly Polyline Run;
        internal readonly Point3d Location;
        internal readonly Database Db;
        internal readonly Transaction Tx;
        internal readonly string BlockDb = @"X:\AutoCAD DRI - 01 Civil 3D\DynBlokke\Symboler.dwg";
        internal readonly string BlockLayerName = "0-KOMPONENT";
        internal readonly int Dn;
        internal readonly PipeSystemEnum PipeSystem; //Stål, Cu, Alu osv.
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
        internal BlockReference Br;
        //private DataTable Data;
        public ComponentData(Polyline run, Point3d location)
        {
            Run = run;
            Location = location;
            Db = run.Database;

            if (Db.TransactionManager.TopTransaction != null)
            {
                Tx = Db.TransactionManager.TopTransaction;
            }
            else throw new System.Exception($"Class ComponentData created outside a transaction!");

            Dn = PipeSchedule.GetPipeDN(run);
            PipeSystem = PipeSchedule.GetPipeSystem(run);
            PipeType = PipeSchedule.GetPipeType(run);
            PipeSerie = PipeSchedule.GetPipeSeriesV2(Run, true);
        }
        //internal void ReadData(string pathToData)
        //{
        //    Data = CsvReader.ReadCsvToDataTable(pathToData, "Data");
        //}
        internal virtual Result Validate()
        {
            //Validate presence of layer where to place blocks
            Db.CheckOrCreateLayer(BlockLayerName, 0);

            Result result = new Result();
            //Test BlockDb
            if (!File.Exists(BlockDb))
                throw new System.Exception("ComponentData cannot access " + BlockDb + "!");

            //Test Dn
            if (Dn == 999)
            {
                result.Status = ResultStatus.SoftError;
                result.ErrorMsg = $"Pipe {Run.Handle} fails to report correct DN!";
            }

            //Test pipe system
            if (PipeSystem == PipeSystemEnum.Ukendt)
            {
                result.Status = ResultStatus.SoftError;
                result.ErrorMsg = $"Pipe {Run.Handle} fails to report correct PipeSystem (Stål, AluFlex osv.)!";
            }

            //Test pipe type
            if (PipeType == PipeTypeEnum.Ukendt)
            {
                result.Status = ResultStatus.SoftError;
                result.ErrorMsg = $"Pipe {Run.Handle} fails to report correct PipeType (Twin/Enkelt)!";
            }

            //Test pipe series
            if (PipeSerie == PipeSeriesEnum.Undefined)
            {
                result.Status = ResultStatus.SoftError;
                result.ErrorMsg = $"Pipe {Run.Handle} fails to report correct PipeSerie!";
            }

            return result;
        }
        internal virtual Result Place()
        {
            throw new NotImplementedException();
        }
        internal virtual Result Cut()
        {
            throw new NotImplementedException();
        }
        internal void CheckPresenceOrImportBlock(string blockName)
        {
            BlockTable bt = Db.BlockTableId.Go<BlockTable>(Tx);
            if (!bt.Has(blockName)) Db.CheckOrImportBlockRecord(BlockDb, blockName);
        }
    }
    internal class Elbow : ComponentData
    {
        private readonly string blockNameTwin = "PRÆBØJN-90GR-TWIN-GLD";
        private readonly string blockNameEnkelt = "PRÆBØJN 90GR ENKELT";
        private string blockName { get => PipeType == PipeTypeEnum.Twin ? blockNameTwin : blockNameEnkelt; }
        public Elbow(Polyline run, Point3d location) : base(run, location)
        {
            //string pathToData =
            //    @"X:\AutoCAD DRI - 01 Civil 3D\DynBlokke\Isoplus tabeller\Twin_90gr_Alle_S.csv";

            //if (File.Exists(pathToData)) this.ReadData(pathToData);
            //else throw new System.Exception("Class Elbow:ComponentData cannot find " + pathToData + "!");
        }
        internal override Result Validate()
        {
            Result result = base.Validate();

            #region Test to see if block is present in DB or import
            CheckPresenceOrImportBlock(blockNameTwin);
            CheckPresenceOrImportBlock(blockNameEnkelt);
            #endregion

            #region Check number of MuffeIntern blocks in BTR
            BlockTable bt = Db.BlockTableId.Go<BlockTable>(Tx);
            BlockTableRecord btr = bt[blockNameTwin].Go<BlockTableRecord>(Tx);
            var blocks = btr.GetNestedBlocksByName("MuffeIntern");
            if (blocks.Length != 2)
                throw new System.Exception(
                    $"BlockTableRecord {btr.Name} has unexpected number ({blocks.Length}) of MuffeIntern!");
            btr = bt[blockNameEnkelt].Go<BlockTableRecord>(Tx);
            blocks = btr.GetNestedBlocksByName("MuffeIntern");
            if (blocks.Length != 2)
                throw new System.Exception(
                    $"BlockTableRecord {btr.Name} has unexpected number ({blocks.Length}) of MuffeIntern!");
            #endregion

            #region Test to see if point coincides with a vertice or at ends
            int idx = Run.GetIndexAtPoint(Location);

            if (idx == -1)
            {
                result.Status = ResultStatus.SoftError;
                result.ErrorMsg = "Location not a vertice! The location must be a vertice.";
            }

            else if (idx == 0 || idx == Run.NumberOfVertices - 1)
            {
                result.Status = ResultStatus.SoftError;
                result.ErrorMsg = "The command does not accept start or end points. Yet...";
            }
            #endregion

            #region Test to see if adjacent segments are lines
            SegmentType st1 = Run.GetSegmentType(idx);
            if (st1 != SegmentType.Line)
            {
                result.Status = ResultStatus.SoftError;
                result.ErrorMsg = "Next segment is not a Line!";
            }
            SegmentType st2 = Run.GetSegmentType(idx - 1);
            if (st2 != SegmentType.Line)
            {
                result.Status = ResultStatus.SoftError;
                result.ErrorMsg = "Previous segment is not a Line!";
            }
            #endregion

            #region Test to see if bend is 90°
            else
            {
                LineSegment2d seg1 = Run.GetLineSegment2dAt(idx);
                LineSegment2d seg2 = Run.GetLineSegment2dAt(idx - 1);

                double angle = seg1.Direction.GetAngleTo(seg2.Direction).ToDegrees();

                if (!angle.Equalz(90.0, 0.00001))
                {
                    result.Status = ResultStatus.SoftError;
                    result.ErrorMsg =
                        $"The bend is not exactly 90°! But actually {angle}.";
                }
            }
            #endregion

            return result;
        }
        internal override Result Place()
        {
            Result result = new Result();
            int idx = Run.GetIndexAtPoint(Location);

            LineSegment3d seg1 = Run.GetLineSegmentAt(idx);
            LineSegment3d seg2 = Run.GetLineSegmentAt(idx - 1);
            double rotation = Math.Atan2(seg1.Direction.Y, seg1.Direction.X);

            Br = Db.CreateBlockWithAttributes(blockName, Location, rotation);
            Br.Layer = BlockLayerName;

            var cp = seg1.Direction.CrossProduct(seg2.Direction);
            if (cp.Z.Equalz(-1, 0.000001))
                SetDynBlockProperty(Br, "Flip-H", "1");

            //DN is NoUnits, must be set with a string
            SetDynBlockPropertyObject(Br, "DN", Dn.ToString());
            if (PipeType == PipeTypeEnum.Twin)
                SetDynBlockPropertyObject(Br, "Serie", PipeSerie.ToString());
            Br.AttSync();

            return result;
        }
        /// <summary>
        /// Must be called within same transaction as Place()
        /// </summary>
        internal override Result Cut()
        {
            Result result = new Result();
            BlockTableRecord btr = Br.BlockTableRecord.Go<BlockTableRecord>(Tx);

            var muffer = btr.GetNestedBlocksByName("MuffeIntern");

            List<double> splitPts = new List<double>();
            foreach (BlockReference br in muffer)
            {
                Point3d pt = br.Position.TransformBy(Br.BlockTransform);
                splitPts.Add(
                    Run.GetParameterAtPoint(
                        Run.GetClosestPointTo(pt, false)));
            }

            splitPts.Sort();

            try
            {
                DBObjectCollection objs = Run
                    .GetSplitCurves(new DoubleCollection(splitPts.ToArray()));

                if (objs.Count != 3) throw new System.Exception(
                    $"Unexpected number of split curves for polyline {Run.Handle}!");

                for (int i = 0; i < 3; i++)
                {
                    if (i == 1) continue;
                    Polyline pl = objs[i] as Polyline;

                    pl.AddEntityToDbModelSpace(Db);
                    pl.Layer = Run.Layer;
                    pl.ConstantWidth = Run.ConstantWidth;
                    PropertySetManager.CopyAllProperties(Run, pl);
                }

                Run.CheckOrOpenForWrite();
                Run.Erase(true);
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                Application.ShowAlertDialog(ex.Message + "\n" + ex.StackTrace);
                throw new System.Exception("Splitting of pline failed!");
            }

            return result;
        }
    }
    internal class Transition : ComponentData
    {
        private readonly string blockName;
        public Transition(Polyline run, Point3d location, TransitionType type)
            : base(run, location)
        {
            blockName = type == TransitionType.X1 ? "RED KDLR" : "RED KDLR x2";
        }
        internal override Result Validate()
        {
            Result result = base.Validate();

            #region Test to see if block is present in DB or import
            CheckPresenceOrImportBlock(blockName);
            #endregion

            #region Check number of MuffeIntern blocks in BTR
            BlockTable bt = Db.BlockTableId.Go<BlockTable>(Tx);
            BlockTableRecord btr = bt[blockName].Go<BlockTableRecord>(Tx);
            var blocks = btr.GetNestedBlocksByName("MuffeIntern");
            if (blocks.Length != 2)
                throw new System.Exception(
                    $"BlockTableRecord {btr.Name} has unexpected number ({blocks.Length}) of MuffeIntern!");
            #endregion

            #region Test to see if point is on line, is not coincident and not start or end
            int idx = Run.GetIndexAtPoint(Location);

            //Real idx is the idx the segment belongs to even if location is not on vertice
            int realIdx = (int)Run.GetParameterAtPoint(Location);

            if (Run.GetDistToPoint(Location) > 0.000001)
            {
                result.Status = ResultStatus.SoftError;
                result.ErrorMsg = "Location is not on a pipe. Select location on pipe.";
            }
            else if (idx != -1)
            {
                result.Status = ResultStatus.SoftError;
                result.ErrorMsg = "Location is a vertice! The location must NOT be a vertice.";
            }
            else if (idx == 0 || idx == Run.NumberOfVertices - 1)
            {
                result.Status = ResultStatus.SoftError;
                result.ErrorMsg = "The command does not accept start or end points. Yet...";
            }
            else if (Run.GetSegmentType(realIdx) != SegmentType.Line)
            {
                result.Status = ResultStatus.SoftError;
                result.ErrorMsg = "The segment is not a Line! Must be a straight Line segment.";
            }
            #endregion

            return result;
        }
        internal override Result Place()
        {
            throw new NotImplementedException("Continue here!");

            Result result = new Result();
            int idx = Run.GetIndexAtPoint(Location);
            

            LineSegment3d seg1 = Run.GetLineSegmentAt(idx);
            LineSegment3d seg2 = Run.GetLineSegmentAt(idx - 1);
            double rotation = Math.Atan2(seg1.Direction.Y, seg1.Direction.X);

            Br = Db.CreateBlockWithAttributes(blockName, Location, rotation);

            var cp = seg1.Direction.CrossProduct(seg2.Direction);
            if (cp.Z.Equalz(-1, 0.000001))
                SetDynBlockProperty(Br, "Flip-H", "1");

            //DN is NoUnits, must be set with a string
            SetDynBlockPropertyObject(Br, "DN", Dn.ToString());
            if (PipeType == PipeTypeEnum.Twin)
                SetDynBlockPropertyObject(Br, "Serie", PipeSerie.ToString());
            Br.AttSync();

            return result;
        }
        /// <summary>
        /// Must be called within same transaction as Place()
        /// </summary>
        internal override Result Cut()
        {
            Result result = new Result();
            BlockTableRecord btr = Br.BlockTableRecord.Go<BlockTableRecord>(Tx);

            var muffer = btr.GetNestedBlocksByName("MuffeIntern");

            List<double> splitPts = new List<double>();
            foreach (BlockReference br in muffer)
            {
                Point3d pt = br.Position.TransformBy(Br.BlockTransform);
                splitPts.Add(
                    Run.GetParameterAtPoint(
                        Run.GetClosestPointTo(pt, false)));
            }

            splitPts.Sort();

            try
            {
                DBObjectCollection objs = Run
                    .GetSplitCurves(new DoubleCollection(splitPts.ToArray()));

                if (objs.Count != 3) throw new System.Exception(
                    $"Unexpected number of split curves for polyline {Run.Handle}!");

                for (int i = 0; i < 3; i++)
                {
                    if (i == 1) continue;
                    Polyline pl = objs[i] as Polyline;

                    pl.AddEntityToDbModelSpace(Db);
                    pl.Layer = Run.Layer;
                    pl.ConstantWidth = Run.ConstantWidth;
                    PropertySetManager.CopyAllProperties(Run, pl);
                }

                Run.CheckOrOpenForWrite();
                Run.Erase(true);
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                Application.ShowAlertDialog(ex.Message + "\n" + ex.StackTrace);
                throw new System.Exception("Splitting of pline failed!");
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
