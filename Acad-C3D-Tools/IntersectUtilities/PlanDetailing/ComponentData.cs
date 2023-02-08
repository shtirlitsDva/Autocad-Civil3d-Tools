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
        internal readonly Polyline OriginalHost;
        internal readonly Point3d Location;
        internal readonly Database Db;
        internal readonly Transaction Tx;
        internal readonly string BlockDb = @"X:\AutoCAD DRI - 01 Civil 3D\DynBlokke\Symboler.dwg";
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
        private DataTable Data;
        public ComponentData(Polyline originalHost, Point3d location)
        {
            OriginalHost = originalHost;
            Location = location;
            Db = originalHost.Database;

            if (Db.TransactionManager.TopTransaction != null)
            {
                Tx = Db.TransactionManager.TopTransaction;
            }
            else throw new System.Exception($"Class ComponentData created outside a transaction!");

            Dn = PipeSchedule.GetPipeDN(originalHost);
            PipeSystem = PipeSchedule.GetPipeSystem(originalHost);
            PipeType = PipeSchedule.GetPipeType(originalHost);
            PipeSerie = PipeSchedule.GetPipeSeriesV2(OriginalHost, true);
        }
        internal void ReadData(string pathToData)
        {
            Data = CsvReader.ReadCsvToDataTable(pathToData, "Data");
        }
        internal virtual Result Validate()
        {
            Result result = new Result();
            //Test BlockDb
            if (!File.Exists(BlockDb))
                throw new System.Exception("ComponentData cannot access " + BlockDb + "!");

            //Test Dn
            if (Dn == 999)
            {
                result.Status = ResultStatus.SoftError;
                result.ErrorMsg = $"Pipe {OriginalHost.Handle} fails to report correct DN!";
            }

            //Test pipe system
            if (PipeSystem == PipeSystemEnum.Ukendt)
            {
                result.Status = ResultStatus.SoftError;
                result.ErrorMsg = $"Pipe {OriginalHost.Handle} fails to report correct PipeSystem (Stål, AluFlex osv.)!";
            }

            //Test pipe type
            if (PipeType == PipeTypeEnum.Ukendt)
            {
                result.Status = ResultStatus.SoftError;
                result.ErrorMsg = $"Pipe {OriginalHost.Handle} fails to report correct PipeType (Twin/Enkelt)!";
            }

            //Test pipe series
            if (PipeSerie == PipeSeriesEnum.Undefined)
            {
                result.Status = ResultStatus.SoftError;
                result.ErrorMsg = $"Pipe {OriginalHost.Handle} fails to report correct PipeSerie!";
            }

            return result;
        }
        internal virtual Result Place()
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


        public Elbow(Polyline originalHost, Point3d location) : base(originalHost, location)
        {
            string pathToData =
                @"X:\AutoCAD DRI - 01 Civil 3D\DynBlokke\Isoplus tabeller\Twin_90gr_Alle_S.csv";

            if (File.Exists(pathToData)) this.ReadData(pathToData);
            else throw new System.Exception("Class Elbow:ComponentData cannot find " + pathToData + "!");
        }
        internal override Result Validate()
        {
            Result result = base.Validate();

            #region Test to see if block is present in DB or import
            CheckPresenceOrImportBlock(blockNameTwin);
            CheckPresenceOrImportBlock(blockNameEnkelt);
            #endregion

            #region Test to see if point coincides with a vertice
            bool verticeFound = false;
            for (int i = 0; i < OriginalHost.NumberOfVertices; i++)
            {
                Point3d vert = OriginalHost.GetPoint3dAt(i);
                if (vert.IsEqualTo(Location, Tolerance.Global))
                    verticeFound = true;
                if (verticeFound) break;
            }

            if (!verticeFound)
            {
                result.Status = ResultStatus.SoftError;
                result.ErrorMsg = "Location not a vertice! The location must be a vertice.";
            }
            #endregion

            return result;
        }
        internal override Result Place()
        {

        }
    }
}
