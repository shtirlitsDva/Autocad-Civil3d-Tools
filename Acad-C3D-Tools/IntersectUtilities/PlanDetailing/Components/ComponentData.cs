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
using IntersectUtilities.UtilsCommon.DataManager.CsvData;
using IntersectUtilities.UtilsCommon.Enums;

namespace IntersectUtilities.PlanDetailing.Components
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

            var fk = Csv.FjvDynamicComponents;

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
            var query = fk.Rows
                    .Where(row => row[(int)FjvDynamicComponents.Columns.Navn] == blockName)
                    .Select(row => row[(int)FjvDynamicComponents.Columns.Version])
                    .Select(x => { if (string.IsNullOrEmpty(x)) return "1"; else return x; })
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
}