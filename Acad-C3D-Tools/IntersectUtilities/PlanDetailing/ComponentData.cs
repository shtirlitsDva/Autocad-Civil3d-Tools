﻿using Autodesk.AutoCAD.ApplicationServices;
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

namespace IntersectUtilities.PlanDetailing
{
    internal abstract class ComponentData
    {
        private readonly Polyline OriginalHost;
        private readonly Point3d Location;
        private readonly Database Db;
        private readonly Transaction Tx;
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
        }
        internal void ReadData(string pathToData)
        {
            Data = CsvReader.ReadCsvToDataTable(pathToData, "Data");
        }
        internal virtual ResultBuffer ;
    }
    internal class Elbow : ComponentData
    {
        public Elbow(Polyline originalHost, Point3d location) : base(originalHost, location)
        {
            string pathToData =
                @"X:\AutoCAD DRI - 01 Civil 3D\DynBlokke\Isoplus tabeller\Twin_90gr_Alle_S.csv";

            if (File.Exists(pathToData)) this.ReadData(pathToData);
            else throw new System.Exception("Class Elbow:ComponentData cannot find " + pathToData + "!");

            #region Test to see if pline has good constant width
            double kOd = PipeSchedule.GetPipeKOd(originalHost, true);
            #endregion

            #region Test to see if point coincides with a vertice
            bool verticeFound = false;
            for (int i = 0; i < originalHost.NumberOfVertices; i++)
            {
                Point3d vert = originalHost.GetPoint3dAt(i);
                if (vert.IsEqualTo(location, Tolerance.Global))
                    verticeFound = true;
                if (verticeFound) break;
            }

            if (!verticeFound)
            {
                prdDbg("Not a vertice! The location must be a vertice.");
                tx.Abort();
                continue;
            }
            #endregion
        }
    }
}
