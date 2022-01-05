using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using MoreLinq;
using System.Text;
using System.Text.RegularExpressions;
using System.Data;
using System.Globalization;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using Autodesk.Aec.DatabaseServices;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;
using Autodesk.Gis.Map;
using Autodesk.Gis.Map.ObjectData;
using Autodesk.Gis.Map.Constants;
using Autodesk.Gis.Map.Utilities;
using Autodesk.Aec.PropertyData;
using Autodesk.Aec.PropertyData.DatabaseServices;
using Autodesk.AutoCAD.Colors;
using static IntersectUtilities.UtilsCommon.Utils;

using AcRx = Autodesk.AutoCAD.Runtime;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;
using ObjectId = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using DataType = Autodesk.Gis.Map.Constants.DataType;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using BlockReference = Autodesk.AutoCAD.DatabaseServices.BlockReference;
using ObjectIdCollection = Autodesk.AutoCAD.DatabaseServices.ObjectIdCollection;
using DBObject = Autodesk.AutoCAD.DatabaseServices.DBObject;
using ErrorStatus = Autodesk.AutoCAD.Runtime.ErrorStatus;
using PsDataType = Autodesk.Aec.PropertyData.DataType;

namespace IntersectUtilities.UtilsCommon
{
    public static class Utils
    {
        public static void prdDbg(string msg)
        {
            DocumentCollection docCol = Application.DocumentManager;
            Editor editor = docCol.MdiActiveDocument.Editor;
            editor.WriteMessage("\n" + msg);
        }
    }
    public static class UtilsDataTables
    {
        public static string ReadStringParameterFromDataTable(string key, System.Data.DataTable table, string parameter, int keyColumnIdx)
        {
            //Test if value exists
            if (table.AsEnumerable().Any(row => row.Field<string>(keyColumnIdx) == key))
            {
                var query = from row in table.AsEnumerable()
                            where row.Field<string>(keyColumnIdx) == key
                            select row.Field<string>(parameter);

                string value = query.FirstOrDefault();

                //if (value.IsNullOrEmpty()) return null;
                return value;
            }
            else return default;
        }
        public static double ReadDoubleParameterFromDataTable(string key, System.Data.DataTable table, string parameter, int keyColumnIdx)
        {
            //Test if value exists
            if (table.AsEnumerable().Any(row => row.Field<string>(keyColumnIdx) == key))
            {
                var query = from row in table.AsEnumerable()
                            where row.Field<string>(keyColumnIdx) == key
                            select row.Field<string>(parameter);

                string value = query.FirstOrDefault();

                if (value.IsNoE() || value == null) return 0;

                double result;

                if (double.TryParse(value, NumberStyles.AllowDecimalPoint,
                                    CultureInfo.InvariantCulture, out result))
                {
                    return result;
                }
                return 0;
            }
            else return 0;
        }
        public static int ReadIntParameterFromDataTable(string key, System.Data.DataTable table, string parameter, int keyColumnIdx)
        {
            //Test if value exists
            if (table.AsEnumerable().Any(row => row.Field<string>(keyColumnIdx) == key))
            {
                var query = from row in table.AsEnumerable()
                            where row.Field<string>(keyColumnIdx) == key
                            select row.Field<string>(parameter);

                string value = query.FirstOrDefault();

                if (value.IsNoE() || value == null) return 0;

                int result;

                if (int.TryParse(value, NumberStyles.AllowDecimalPoint,
                                    CultureInfo.InvariantCulture, out result))
                {
                    return result;
                }
                return 0;
            }
            else return 0;
        }
    }
    public static class UtilsODData
    {
        #region "Map 3D Utility Function"
        /// <summary>
        /// Removes the Table named tableName.
        /// </summary>
        /// <remarks>
        /// Throws no exception in common conditions.
        /// </remarks>
        public static bool RemoveTable(Tables tables, string tableName)
        {
            try
            {
                tables.RemoveTable(tableName);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Creates a Table named tableName.
        /// </summary>
        /// <remarks>
        /// Throws no exception in common conditions.
        /// </remarks>
        public static bool CreateTable(
            Tables tables, string tableName, string tableDescription,
            string[] columnNames, string[] columnDescriptions,
            Autodesk.Gis.Map.Constants.DataType[] dataTypes)
        {
            ErrorCode errODCode = ErrorCode.OK;
            Autodesk.Gis.Map.ObjectData.Table table = null;

            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;

            try
            {
                table = tables[tableName];
            }
            catch (MapException e)
            {
                errODCode = (ErrorCode)(e.ErrorCode);
            }

            if (ErrorCode.ErrorObjectNotFound == errODCode)
            {
                try
                {
                    MapApplication app = HostMapApplicationServices.Application;

                    FieldDefinitions tabDefs = app.ActiveProject.MapUtility.NewODFieldDefinitions();

                    for (int i = 0; i < columnNames.Length; i++)
                    {
                        FieldDefinition def = FieldDefinition.Create(columnNames[i], columnDescriptions[i], dataTypes[i]);
                        if (!def.IsValid)
                        {
                            ed.WriteMessage($"\nField Definition {def.Name} is not valid!");
                        }
                        tabDefs.AddColumn(def, i);
                    }

                    tables.Add(tableName, tabDefs, tableDescription, true);

                    return true;
                }
                catch (MapException e)
                {
                    // Deal with the exception as your will
                    errODCode = (ErrorCode)(e.ErrorCode);

                    ed.WriteMessage($"\nCreating table failed with error: {errODCode} and stacktrace: {e.StackTrace}.");
                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// Adds a record to a Table named tableName, the record is generated automatically.
        /// </summary>
        public static bool AddODRecord(Tables tables, string tableName,
                                          Oid id, MapValue[] values)
        {
            try
            {
                Autodesk.Gis.Map.ObjectData.Table table = tables[tableName];

                // Create and initialize an record 
                Record tblRcd = Record.Create();
                table.InitRecord(tblRcd);

                for (int i = 0; i < tblRcd.Count; i++)
                {
                    switch (tblRcd[i].Type)
                    {
                        case Autodesk.Gis.Map.Constants.DataType.UnknownType:
                            return false;
                        case Autodesk.Gis.Map.Constants.DataType.Integer:
                            if (tblRcd[i].Type == values[i].Type)
                            {
                                tblRcd[i].Assign(values[i]);
                            }
                            else return false;
                            break;
                        case Autodesk.Gis.Map.Constants.DataType.Real:
                            if (tblRcd[i].Type == values[i].Type)
                            {
                                tblRcd[i].Assign(values[i]);
                            }
                            else return false;
                            break;
                        case Autodesk.Gis.Map.Constants.DataType.Character:
                            if (tblRcd[i].Type == values[i].Type)
                            {
                                tblRcd[i].Assign(values[i]);
                            }
                            else return false;
                            break;
                        case Autodesk.Gis.Map.Constants.DataType.Point:
                            if (tblRcd[i].Type == values[i].Type)
                            {
                                tblRcd[i].Assign(values[i]);
                            }
                            else return false;
                            break;
                        default:
                            return false;
                    }
                }

                table.AddRecord(tblRcd, id);

                return true;
            }
            catch (MapException)
            {
                return false;
            }
        }
        public static bool AddEmptyODRecord(Autodesk.Gis.Map.ObjectData.Table table, Oid id)
        {
            try
            {
                // Create and initialize an record 
                Record tblRcd = Record.Create();
                table.InitRecord(tblRcd);
                table.AddRecord(tblRcd, id);

                return true;
            }
            catch (MapException)
            {
                return false;
            }
        }

        public static bool AddODRecord(Tables tables, string tableName, string columnName,
                                          Oid id, MapValue originalValue)
        {
            try
            {
                Autodesk.Gis.Map.ObjectData.Table table = tables[tableName];
                FieldDefinitions tableDef = table.FieldDefinitions;

                // Create and initialize an record B
                Record tblRcd = Record.Create();
                table.InitRecord(tblRcd);

                for (int i = 0; i < tblRcd.Count; i++)
                {
                    FieldDefinition column = tableDef[i];
                    if (column.Name == columnName && table.Name == tableName)
                    {
                        MapValue newValue = tblRcd[i];
                        newValue.Assign(originalValue);
                        //switch (newValue.Type)
                        //{
                        //    case Autodesk.Gis.Map.Constants.DataType.UnknownType:
                        //        return false;
                        //    case Autodesk.Gis.Map.Constants.DataType.Integer:
                        //        if (originalValue.Type == newValue.Type)
                        //        {
                        //            newValue.Assign(originalValue.Int32Value);
                        //        }
                        //        break;
                        //    case Autodesk.Gis.Map.Constants.DataType.Real:
                        //        if (originalValue.Type == newValue.Type)
                        //        {
                        //            newValue.Assign(originalValue.DoubleValue);
                        //        }
                        //        break;
                        //    case Autodesk.Gis.Map.Constants.DataType.Character:
                        //        if (originalValue.Type == newValue.Type)
                        //        {
                        //            newValue.Assign(originalValue.StrValue);
                        //        }
                        //        break;
                        //    case Autodesk.Gis.Map.Constants.DataType.Point:
                        //        if (originalValue.Type == newValue.Type)
                        //        {
                        //            newValue.Assign(originalValue.Point);
                        //        }
                        //        break;
                        //    default:
                        //        return false;
                        //}
                    }
                }

                table.AddRecord(tblRcd, id);
                return true;
            }
            catch (MapException)
            {
                return false;
            }
        }

        /// <summary>
        /// Updates a record to a Table named tableName, the record is generated automatically.
        /// </summary>
        public static bool UpdateODRecord<T>(Tables tables, string tableName, string columnName,
                                          Oid id, T value)
        {
            try
            {
                ErrorCode errCode = ErrorCode.OK;

                bool success = true;

                // Get and Initialize Records
                using (Records records = tables.GetObjectRecords
                    (0, id, Autodesk.Gis.Map.Constants.OpenMode.OpenForWrite, false))
                {
                    if (records.Count == 0)
                    {
                        //prdDbg($"\nThere is no ObjectData record attached on the entity.");
                        return false;
                    }

                    // Iterate through all records
                    foreach (Record record in records)
                    {
                        // Get record info
                        for (int i = 0; i < record.Count; i++)
                        {
                            Autodesk.Gis.Map.ObjectData.Table table = tables[record.TableName];
                            FieldDefinitions tableDef = table.FieldDefinitions;
                            FieldDefinition column = tableDef[i];
                            if (column.Name == columnName && record.TableName == tableName)
                            {
                                MapValue val = record[i];

                                switch (value)
                                {
                                    case int integer:
                                        if (val.Type == Autodesk.Gis.Map.Constants.DataType.Integer)
                                        {
                                            val = val.Assign(integer);
                                        }
                                        else return false;
                                        break;
                                    case string str:
                                        if (val.Type == Autodesk.Gis.Map.Constants.DataType.Character)
                                        {
                                            val = val.Assign(str);
                                        }
                                        else return false;
                                        break;
                                    case double real:
                                        if (val.Type == Autodesk.Gis.Map.Constants.DataType.Real)
                                        {
                                            val = val.Assign(real);
                                        }
                                        else return false;
                                        break;
                                    default:
                                        return false;
                                }
                            }
                        }
                    }
                }
                return true;
            }
            catch (MapException)
            {
                return false;
            }
        }

        public static bool UpdateODRecord(Tables tables, string tableName, string columnName,
                                          Oid id, MapValue value)
        {
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;

            try
            {
                ErrorCode errCode = ErrorCode.OK;

                bool success = false;

                // Get and Initialize Records
                using (Records records = tables.GetObjectRecords
                    (0, id, Autodesk.Gis.Map.Constants.OpenMode.OpenForWrite, false))
                {
                    if (records.Count == 0)
                    {
                        ed.WriteMessage($"\nThere is no ObjectData record attached on the entity.");
                        return false;
                    }

                    // Iterate through all records
                    foreach (Record record in records)
                    {
                        Autodesk.Gis.Map.ObjectData.Table table = tables[record.TableName];
                        FieldDefinitions tableDef = table.FieldDefinitions;
                        // Get record info
                        for (int i = 0; i < record.Count; i++)
                        {
                            FieldDefinition column = tableDef[i];
                            if (column.Name == columnName && record.TableName == tableName)
                            {
                                MapValue val = record[i];
                                val.Assign(value);
                                records.UpdateRecord(record);
                                return true;
                            }
                        }
                    }
                }
                return false;
            }
            catch (MapException ex)
            {
                prdDbg(((Autodesk.Gis.Map.Constants.ErrorCode)ex.ErrorCode).ToString());
                return false;
            }
        }

        /// <summary>
        /// Prints the records obtained by id.
        /// </summary>
        public static MapValue ReadRecordData(Tables tables, Oid id, string tableName, string columnName)
        {
            ErrorCode errCode = ErrorCode.OK;
            try
            {
                // Get and Initialize Records
                using (Records records
                           = tables.GetObjectRecords(0, id, Autodesk.Gis.Map.Constants.OpenMode.OpenForRead, false))

                {
                    if (records.Count == 0)
                    {
                        prdDbg("records.Count is 0!");
                        //prdDbg($"\nThere is no ObjectData record attached on the entity.");
                        return null;
                    }
                    // Iterate through all records
                    foreach (Record record in records)
                    {
                        // Get the table
                        var table = tables[record.TableName];
                        // Get record info
                        for (int i = 0; i < record.Count; i++)
                        {
                            FieldDefinitions tableDef = table.FieldDefinitions;
                            FieldDefinition column = tableDef[i];
                            if (column.Name == columnName && record.TableName == tableName)
                            {
                                return record[i];
                            }
                        }
                    }
                }

                return null;
            }
            catch (MapException e)
            {
                errCode = (ErrorCode)(e.ErrorCode);
                // Deal with the exception here as your will
                prdDbg("Exception in ReadIntPropertyValue -> ReadRecordData!");
                prdDbg(e.Message);
                prdDbg(((ErrorCode)e.ErrorCode).ToString());
                return null;
            }
        }

        public static int ReadIntPropertyValue(Tables tables, Oid id, string tableName, string columnName)
        {
            ErrorCode errCode = ErrorCode.OK;
            try
            {
                MapValue value = ReadRecordData(tables, id, tableName, columnName);
                if (value != null) return value.Int32Value;
                else return 0;
            }
            catch (MapException e)
            {
                errCode = (ErrorCode)(e.ErrorCode);
                // Deal with the exception here as your will
                prdDbg("MapException in ReadIntPropertyValue!");
                prdDbg(e.Message);
                prdDbg(((ErrorCode)e.ErrorCode).ToString());

                return 0;
            }
            catch (System.Exception e)
            {
                prdDbg("System.Exception in ReadIntPropertyValue!");
                prdDbg(e.Message);
                return 0;
            }
        }

        public static double ReadDoublePropertyValue(Tables tables, Oid id, string tableName, string columnName)
        {
            ErrorCode errCode = ErrorCode.OK;
            try
            {
                MapValue value = ReadRecordData(tables, id, tableName, columnName);
                if (value != null) return value.DoubleValue;
                else return 0;
            }
            catch (MapException e)
            {
                errCode = (ErrorCode)(e.ErrorCode);
                // Deal with the exception here as your will

                return 0;
            }
        }

        public static string ReadStringPropertyValue(Tables tables, Oid id, string tableName, string columnName)
        {
            ErrorCode errCode = ErrorCode.OK;
            try
            {
                MapValue value = ReadRecordData(tables, id, tableName, columnName);
                if (value != null) return value.StrValue;
                else return "";
            }
            catch (MapException e)
            {
                errCode = (ErrorCode)(e.ErrorCode);
                // Deal with the exception here as your will

                return "";
            }
        }

        public static string ReadPropertyToStringValue(Tables tables, Oid id, string tableName, string columnName)
        {
            ErrorCode errCode = ErrorCode.OK;
            try
            {
                MapValue value = ReadRecordData(tables, id, tableName, columnName);
                if (value != null)
                {
                    switch (value.Type)
                    {
                        case Autodesk.Gis.Map.Constants.DataType.UnknownType:
                            return "";
                        case Autodesk.Gis.Map.Constants.DataType.Integer:
                            return value.Int32Value.ToString();
                        case Autodesk.Gis.Map.Constants.DataType.Real:
                            return value.DoubleValue.ToString();
                        case Autodesk.Gis.Map.Constants.DataType.Character:
                            return value.StrValue;
                        case Autodesk.Gis.Map.Constants.DataType.Point:
                            return value.Point.ToString();
                        default:
                            return "";
                    }
                }
                else return "";
            }
            catch (MapException e)
            {
                errCode = (ErrorCode)(e.ErrorCode);
                // Deal with the exception here as your will

                return "";
            }
        }

        public static bool DoesRecordExist(Tables tables, Oid id, string tableName, string columnName)
        {
            ErrorCode errCode = ErrorCode.OK;
            try
            {
                bool success = true;

                // Get and Initialize Records
                using (Records records
                           = tables.GetObjectRecords(0, id, Autodesk.Gis.Map.Constants.OpenMode.OpenForRead, false))
                {
                    if (records.Count == 0)
                    {
                        return false;
                    }

                    // Iterate through all records
                    foreach (Record record in records)
                    {
                        if (record.TableName == tableName)
                        {
                            // Get the table
                            Autodesk.Gis.Map.ObjectData.Table table = tables[record.TableName];

                            // Get record info
                            for (int i = 0; i < record.Count; i++)
                            {
                                FieldDefinitions tableDef = table.FieldDefinitions;
                                FieldDefinition column = tableDef[i];
                                if (column.Name == columnName) return true;
                            }
                        }
                    }
                }
                return false;
            }
            catch (MapException e)
            {
                errCode = (ErrorCode)(e.ErrorCode);
                // Deal with the exception here as your will

                return false;
            }
        }

        public static void CopyAllOD(Tables tables, Entity entSource, Entity entTarget)
        {
            CopyAllOD(tables, entSource.Id, entTarget.Id);
        }

        public static void CopyAllOD(Tables tables, Oid sourceId, Oid targetId)
        {
            using (Records records = tables.GetObjectRecords(
                   0, sourceId, Autodesk.Gis.Map.Constants.OpenMode.OpenForRead, false))
            {
                if (records == null || records.Count == 0) return;

                //prdDbg($"\nEntity: {entSource.Handle}");

                foreach (Record record in records)
                {
                    Autodesk.Gis.Map.ObjectData.Table table = tables[record.TableName];

                    Record newRecord = Record.Create();
                    table.InitRecord(newRecord);

                    for (int i = 0; i < record.Count; i++)
                    {
                        MapValue sourceValue = record[i];
                        MapValue newVal = null;
                        switch (sourceValue.Type)
                        {
                            case Autodesk.Gis.Map.Constants.DataType.UnknownType:
                                continue;
                            case Autodesk.Gis.Map.Constants.DataType.Integer:
                                newVal = newRecord[i];
                                newVal.Assign(sourceValue.Int32Value);
                                break;
                            case Autodesk.Gis.Map.Constants.DataType.Real:
                                newVal = newRecord[i];
                                newVal.Assign(sourceValue.DoubleValue);
                                break;
                            case Autodesk.Gis.Map.Constants.DataType.Character:
                                newVal = newRecord[i];
                                newVal.Assign(sourceValue.StrValue);
                                break;
                            case Autodesk.Gis.Map.Constants.DataType.Point:
                                newVal = newRecord[i];
                                newVal.Assign(sourceValue.Point);
                                break;
                            default:
                                break;
                        }
                    }
                    try
                    {
                        table.AddRecord(newRecord, targetId);
                    }
                    catch (Autodesk.Gis.Map.MapException ex)
                    {
                        string ErrorText = ((Autodesk.Gis.Map.Constants.ErrorCode)ex.ErrorCode).ToString();
                        prdDbg($"\n{ErrorText}: {ex.Message}: {ex.Source}: {ex.StackTrace}");
                        throw;
                    }
                }
            }
        }

        public static void TryCopySpecificOD(Tables tables, Entity entSource, Entity entTarget,
            List<(string tableName, string columnName)> odList)
        {
            foreach (var item in odList)
            {
                MapValue originalValue = ReadRecordData(tables, entSource.ObjectId, item.tableName, item.columnName);
                if (originalValue != null)
                {
                    if (DoesRecordExist(tables, entTarget.ObjectId, item.tableName, item.columnName))
                    {
                        UpdateODRecord(tables, item.tableName, item.columnName, entTarget.ObjectId, originalValue);
                    }
                    else
                    {
                        AddODRecord(tables, item.tableName, item.columnName, entTarget.ObjectId, originalValue);
                    }
                }
            }

        }
        public static bool DoesTableExist(Tables tables, string tableName)
        {
            try
            {
                Autodesk.Gis.Map.ObjectData.Table table = tables[tableName];
                return table != null;
            }
            catch
            {
                return false;
            }
        }

        public static bool DoAllColumnsExist(Tables tables, string m_tableName, string[] columnNames)
        {
            // Get the table
            Autodesk.Gis.Map.ObjectData.Table table = tables[m_tableName];
            // Get tabledef info
            FieldDefinitions tableDef = table.FieldDefinitions;
            List<string> existingColumnNames = new List<string>(tableDef.Count);
            for (int k = 0; k < tableDef.Count; k++)
            {
                FieldDefinition column = tableDef[k];
                existingColumnNames.Add(column.Name);
            }
            foreach (string name in columnNames)
            {
                if (existingColumnNames.Any(x => x == name)) continue;
                else return false;
            }
            return true;
        }

        public static bool CreateMissingColumns(Tables tables, string m_tableName, string[] columnNames, string[] columnDescriptions,
            DataType[] dataTypes)
        {
            // Get the table
            Autodesk.Gis.Map.ObjectData.Table table = tables[m_tableName];
            // Get tabledef info
            FieldDefinitions tableDef = table.FieldDefinitions;
            List<string> existingColumnNames = new List<string>(tableDef.Count);
            for (int k = 0; k < tableDef.Count; k++)
            {
                FieldDefinition column = tableDef[k];
                existingColumnNames.Add(column.Name);
            }
            for (int i = 0; i < columnNames.Length; i++)
            {
                if (existingColumnNames.Any(x => x == columnNames[i])) continue;
                else
                {
                    table.FieldDefinitions.AddColumn(
                        FieldDefinition.Create(columnNames[i], columnDescriptions[i], dataTypes[i]),
                        table.FieldDefinitions.Count);
                    tables.UpdateTable(m_tableName, table.FieldDefinitions);
                }
            }
            return true;
        }

        public static void CheckOrCreateTable(Tables tables, string tableName, string tableDescription,
                                               string[] columnNames, string[] columnDescrs, DataType[] dataTypes)
        {
            //Check or create table, or check or create all columns
            if (DoesTableExist(tables, tableName))
            {//Table exists
                if (DoAllColumnsExist(tables, tableName, columnNames))
                {
                    //The table is in order, continue to data creation
                }
                //If not create missing columns
                else CreateMissingColumns(tables, tableName, columnNames, columnDescrs, dataTypes);
            }
            else
            {
                //Table does not exist
                if (CreateTable(tables, tableName, tableDescription,
                    columnNames, columnDescrs, dataTypes))
                {
                    //Table ready for populating with data
                }
            }
        }

        public static bool CheckAddUpdateRecordValue(
            Tables tables,
            Oid entId,
            string m_tableName,
            string columnName,
            MapValue value)
        {
            if (DoesRecordExist(tables, entId, m_tableName, columnName))
            {
                prdDbg($"\nRecord {columnName} already exists, updating...");

                if (UpdateODRecord(tables, m_tableName, columnName, entId, value))
                {
                    prdDbg($"\nUpdating record {columnName} succeded!");
                    return true;
                }
                else
                {
                    prdDbg($"\nUpdating record {columnName} failed!");
                    return false;
                }
            }
            else if (AddODRecord(tables, m_tableName, columnName, entId, value))
            {
                prdDbg($"\nAdding record {columnName} succeded!");
                return true;
            }
            return false;
        }
        #endregion
    }
    public static class Extensions
    {
        public static bool IsNoE(this string s) => string.IsNullOrEmpty(s);
        public static bool IsNotNoE(this string s) => !string.IsNullOrEmpty(s);
        public static bool Equalz(this double a, double b, double tol) => Math.Abs(a - b) <= tol;
        public static bool HorizontalEqualz(this Point3d a, Point3d b, double tol = 0.01) =>
            null != a && null != b && a.X.Equalz(b.X, tol) && a.Y.Equalz(b.Y, tol);
        public static void CheckOrOpenForWrite(this Autodesk.AutoCAD.DatabaseServices.DBObject dbObject)
        {
            if (dbObject.IsWriteEnabled == false)
            {
                if (dbObject.IsReadEnabled == true)
                {
                    dbObject.UpgradeOpen();
                }
                else if (dbObject.IsReadEnabled == false)
                {
                    dbObject.UpgradeOpen();
                    dbObject.UpgradeOpen();
                }
            }
        }
        public static void CheckOrOpenForRead(this Autodesk.AutoCAD.DatabaseServices.DBObject dbObject,
            bool DowngradeIfWriteEnabled = false)
        {
            if (dbObject.IsReadEnabled == false)
            {
                if (dbObject.IsWriteEnabled == true)
                {
                    if (DowngradeIfWriteEnabled)
                    {
                        dbObject.DowngradeOpen();
                    }
                    return;
                }
                dbObject.UpgradeOpen();
            }
        }
        public static double GetHorizontalLength(this Polyline3d poly3d, Transaction tx)
        {
            poly3d.CheckOrOpenForRead();
            var vertices = poly3d.GetVertices(tx);
            double totalLength = 0;
            for (int i = 0; i < vertices.Length - 1; i++)
            {
                totalLength += vertices[i].Position.DistanceHorizontalTo(vertices[i + 1].Position);
            }
            return totalLength;
        }
        public static double GetHorizontalLengthBetweenIdxs(this Polyline3d poly3d, int startIdx, int endIdx)
        {
            Transaction tx = poly3d.Database.TransactionManager.TopTransaction;
            poly3d.CheckOrOpenForRead();
            var vertices = poly3d.GetVertices(tx);
            double totalLength = 0;
            for (int i = startIdx; i < endIdx; i++)
            {
                totalLength += vertices[i].Position.DistanceHorizontalTo(vertices[i + 1].Position);
            }
            return totalLength;
        }
        public static double DistanceHorizontalTo(this Point3d sourceP3d, Point3d targetP3d)
        {
            double X1 = sourceP3d.X;
            double Y1 = sourceP3d.Y;
            double X2 = targetP3d.X;
            double Y2 = targetP3d.Y;
            return Math.Sqrt(Math.Pow((X2 - X1), 2) + Math.Pow((Y2 - Y1), 2));
        }
        public static double Pow(this double value, double exponent)
        {
            return Math.Pow(value, exponent);
        }
        public static double GetBulge(this ProfileCircular profileCircular, ProfileView profileView)
        {
            Point2d startPoint = profileView.GetPoint2dAtStaAndEl(profileCircular.StartStation, profileCircular.StartElevation);
            Point2d endPoint = profileView.GetPoint2dAtStaAndEl(profileCircular.EndStation, profileCircular.EndElevation);
            //Calculate bugle
            double r = profileCircular.Radius;
            double u = startPoint.GetDistanceTo(endPoint);
            double b = (2 * (r - Math.Sqrt(r.Pow(2) - u.Pow(2) / 4))) / u;
            if (profileCircular.CurveType == VerticalCurveType.Crest) b *= -1;
            return b;
        }
        public static double GetBulge(this ProfileEntity profileEntity, ProfileView profileView)
        {
            switch (profileEntity)
            {
                case ProfileTangent tan:
                    return 0;
                case ProfileCircular circ:
                    return circ.GetBulge(profileView);
                default:
                    throw new System.Exception($"GetBulge: ProfileEntity unknown type encountered!");
            }
        }
        public static double LookAheadAndGetBulge(this ProfileEntityCollection collection, ProfileEntity currentEntity, ProfileView profileView)
        {
            ProfileEntity next;
            try
            {
                next = collection.EntityAtId(currentEntity.EntityAfter);
            }
            catch (System.Exception)
            {
                return 0;
            }

            switch (next)
            {
                case ProfileTangent tan:
                    return 0;
                case ProfileCircular circular:
                    return circular.GetBulge(profileView);
                default:
                    throw new System.Exception($"LookAheadAndGetBulge: ProfileEntity unknown type encountered!");
            }
        }
        public static bool Contains(this string source, string toCheck, StringComparison comp)
        {
            return source?.IndexOf(toCheck, comp) >= 0;
        }
        public static void ExplodeToOwnerSpace2(this Autodesk.AutoCAD.DatabaseServices.BlockReference br)
        {
            ExplodeToOwnerSpace3(br);
        }
        static ObjectIdCollection idsAdded = new ObjectIdCollection();
        public static ObjectIdCollection ExplodeToOwnerSpace3(this BlockReference br)
        {
            idsAdded = new ObjectIdCollection();
            Transaction tr = br.Database.TransactionManager.TopTransaction;
            BlockTableRecord spaceBtr = (BlockTableRecord)tr.GetObject(br.BlockId, OpenMode.ForWrite);
            LoopThroughInsertAndAddEntity2n3(br.BlockTransform, br, spaceBtr);

            return idsAdded;
        }
        public static void LoopThroughInsertAndAddEntity2n3(Matrix3d mat,
            Autodesk.AutoCAD.DatabaseServices.BlockReference br, BlockTableRecord space)
        {
            Transaction tr = space.Database.TransactionManager.TopTransaction;
            BlockTableRecord btr = tr.GetObject(br.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;

            foreach (ObjectId id in btr)
            {
                Autodesk.AutoCAD.DatabaseServices.DBObject obj =
                    tr.GetObject(id, OpenMode.ForRead);
                Entity ent = obj.Clone() as Entity;
                if (ent is BlockReference)
                {
                    BlockReference br1 = (BlockReference)ent;
                    LoopThroughInsertAndAddEntity2n3(br1.BlockTransform.PreMultiplyBy(mat), br1, space);
                }
                else
                {
                    ent.TransformBy(mat);
                    space.AppendEntity(ent);
                    tr.AddNewlyCreatedDBObject(ent, true);

                    idsAdded.Add(ent.ObjectId);
                }
            }
        }
        public static void ExplodeToOwnerSpace2(ObjectId id, bool erase = true)
        {
            ExplodeToOwnerSpace3(id, erase);
        }
        public static ObjectIdCollection ExplodeToOwnerSpace3(ObjectId id, bool erase = true)
        {
            ObjectIdCollection ids;

            using (Transaction tr = id.Database.TransactionManager.StartTransaction())
            {
                BlockReference br = (BlockReference)tr.GetObject(id, OpenMode.ForRead);
                if (br.Name.Contains("MuffeIntern")) //||
                                                     //br.Name == "MuffeIntern2" ||
                                                     //br.Name == "MuffeIntern3")
                {
                    tr.Abort();
                    return new ObjectIdCollection();
                }
                ids = br.ExplodeToOwnerSpace3();

                if (erase)
                {
                    prdDbg($"\n{br.Name}");
                    br.UpgradeOpen();
                    br.Erase();
                }

                tr.Commit();
            }

            return ids;
        }
        public static LayerTableRecord GetLayerByName(this LayerTable lt, string layerName)
        {
            foreach (Oid id in lt)
            {
                LayerTableRecord ltr = id.GetObject(OpenMode.ForRead) as LayerTableRecord;
                if (ltr.Name == layerName) return ltr;
            }
            return null;
        }
        public static BlockTableRecord GetModelspaceForWrite(this Database db) =>
            db.BlockTableId.Go<BlockTable>(db.TransactionManager.TopTransaction)[BlockTableRecord.ModelSpace]
            .Go<BlockTableRecord>(db.TransactionManager.TopTransaction, OpenMode.ForWrite);
        public static string RealName(this BlockReference br)
        {
            Transaction tx = br.Database.TransactionManager.TopTransaction;
            return br.IsDynamicBlock
                ? ((BlockTableRecord)tx.GetObject(br.DynamicBlockTableRecord, OpenMode.ForRead)).Name
                : br.Name;
        }
        #region XrecFilter
        //public static bool XrecFilter(this Autodesk.AutoCAD.DatabaseServices.DBObject obj,
        //    string xRecordName, string[] filterValues)
        //{
        //    Transaction tx = obj.Database.TransactionManager.TopTransaction;
        //    Oid extId = obj.ExtensionDictionary;
        //    if (extId == Oid.Null) return false;
        //    DBDictionary dbExt = extId.Go<DBDictionary>(tx);
        //    if (!dbExt.Contains(xRecordName)) return false;
        //    Oid xrecId = dbExt.GetAt(xRecordName);
        //    if (xrecId == Oid.Null) return false;
        //    Xrecord xrec = xrecId.Go<Xrecord>(tx);
        //    TypedValue[] data = xrec.Data.AsArray();
        //    bool[] resArray = new bool[0];
        //    for (int i = 0; i < filterValues.Length; i++)
        //    {
        //        if (data.Length <= i) break;
        //        if (data[i].Value.ToString() == filterValues[i]) resArray = resArray.Append(true).ToArray();
        //        else resArray = resArray.Append(false).ToArray();
        //    }
        //    if (resArray.Length == 0) return false;
        //    return resArray.All(x => x);
        //} 
        #endregion
        public static string XrecReadStringAtIndex(this Autodesk.AutoCAD.DatabaseServices.DBObject obj,
            string xRecordName, int indexToRead)
        {
            Transaction tx = obj.Database.TransactionManager.TopTransaction;
            Oid extId = obj.ExtensionDictionary;
            if (extId == Oid.Null) return "";
            DBDictionary dbExt = extId.Go<DBDictionary>(tx);
            Oid xrecId = Oid.Null;
            if (!dbExt.Contains(xRecordName)) return "";
            xrecId = dbExt.GetAt(xRecordName);
            Xrecord xrec = xrecId.Go<Xrecord>(tx);
            TypedValue[] data = xrec.Data.AsArray();
            if (data.Length <= indexToRead) return "";
            return data[indexToRead].Value.ToString();
        }
        public static void SetAttributeStringValue(this BlockReference br, string attributeName, string value)
        {
            Database db = br.Database;
            Transaction tx = db.TransactionManager.TopTransaction;
            foreach (Oid oid in br.AttributeCollection)
            {
                AttributeReference ar = oid.Go<AttributeReference>(tx);
                if (string.Equals(ar.Tag, attributeName, StringComparison.OrdinalIgnoreCase))
                {
                    ar.CheckOrOpenForWrite();
                    ar.TextString = value;
                }
            }
        }
        public static string GetAttributeStringValue(this BlockReference br, string attributeName)
        {
            Database db = br.Database;
            Transaction tx = db.TransactionManager.TopTransaction;
            foreach (Oid oid in br.AttributeCollection)
            {
                AttributeReference ar = oid.Go<AttributeReference>(tx);
                if (string.Equals(ar.Tag, attributeName, StringComparison.OrdinalIgnoreCase))
                {
                    return ar.TextString;
                }
            }

            return "";
        }
        /// <summary>
        /// Remember to check for existence of BlockTableRecord!
        /// </summary>
        public static BlockReference CreateBlockWithAttributes(this Database db, string blockName, Point3d position, double rotation = 0)
        {
            Transaction tx = db.TransactionManager.TopTransaction;
            BlockTableRecord modelSpace = db.GetModelspaceForWrite();
            BlockTable bt = tx.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
            Oid btrId = bt[blockName];
            BlockTableRecord btr = btrId.Go<BlockTableRecord>(tx);

            var br = new BlockReference(position, btrId);

            modelSpace.CheckOrOpenForWrite();
            modelSpace.AppendEntity(br);
            tx.AddNewlyCreatedDBObject(br, true);
            br.Rotation = rotation;

            foreach (Oid arOid in btr)
            {
                if (arOid.IsDerivedFrom<AttributeDefinition>())
                {
                    AttributeDefinition at = arOid.Go<AttributeDefinition>(tx);
                    if (!at.Constant)
                    {
                        using (AttributeReference atRef = new AttributeReference())
                        {
                            atRef.SetAttributeFromBlock(at, br.BlockTransform);
                            atRef.Position = at.Position.TransformBy(br.BlockTransform);
                            atRef.TextString = at.getTextWithFieldCodes();
                            br.AttributeCollection.AppendAttribute(atRef);
                            tx.AddNewlyCreatedDBObject(atRef, true);
                        }
                    }
                }
            }
            return br;
        }
        public static Point2d GetPoint2dAtStaAndEl(this ProfileView pv, double station, double elevation)
        {
            double x = 0, y = 0;
            pv.FindXYAtStationAndElevation(station, elevation, ref x, ref y);
            return new Point2d(x, y);
        }
        public static string ExceptionInfo(this System.Exception exception)
        {
            StackFrame stackFrame = (new StackTrace(exception, true)).GetFrame(0);
            return string.Format("At line {0} column {1} in {2}: {3} {4}{3}{5}  ",
               stackFrame.GetFileLineNumber(), stackFrame.GetFileColumnNumber(),
               stackFrame.GetMethod(), Environment.NewLine, stackFrame.GetFileName(),
               exception.Message);
        }
        static RXClass attDefClass = RXClass.GetClass(typeof(AttributeDefinition));
        public static void SynchronizeAttributes(this BlockTableRecord target)
        {
            if (target == null)
                throw new ArgumentNullException("target");

            Transaction tr = target.Database.TransactionManager.TopTransaction;
            if (tr == null)
                throw new AcRx.Exception(ErrorStatus.NoActiveTransactions);
            List<AttributeDefinition> attDefs = target.GetAttributes(tr);
            foreach (ObjectId id in target.GetBlockReferenceIds(true, false))
            {
                BlockReference br = (BlockReference)tr.GetObject(id, OpenMode.ForWrite);
                br.ResetAttributes(attDefs, tr);
            }
            if (target.IsDynamicBlock)
            {
                target.UpdateAnonymousBlocks();
                foreach (ObjectId id in target.GetAnonymousBlockIds())
                {
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(id, OpenMode.ForRead);
                    attDefs = btr.GetAttributes(tr);
                    foreach (ObjectId brId in btr.GetBlockReferenceIds(true, false))
                    {
                        BlockReference br = (BlockReference)tr.GetObject(brId, OpenMode.ForWrite);
                        br.ResetAttributes(attDefs, tr);
                    }
                }
            }
        }
        private static List<AttributeDefinition> GetAttributes(this BlockTableRecord target, Transaction tr)
        {
            List<AttributeDefinition> attDefs = new List<AttributeDefinition>();
            foreach (ObjectId id in target)
            {
                if (id.ObjectClass == attDefClass)
                {
                    AttributeDefinition attDef = (AttributeDefinition)tr.GetObject(id, OpenMode.ForRead);
                    attDefs.Add(attDef);
                }
            }
            return attDefs;
        }
        private static void ResetAttributes(this BlockReference br, List<AttributeDefinition> attDefs, Transaction tr)
        {
            Dictionary<string, string> attValues = new Dictionary<string, string>();
            foreach (ObjectId id in br.AttributeCollection)
            {
                if (!id.IsErased)
                {
                    AttributeReference attRef = (AttributeReference)tr.GetObject(id, OpenMode.ForWrite);
                    if (attRef.IsMTextAttribute) attValues.Add(attRef.Tag, attRef.MTextAttribute.HasFields ? attRef.MTextAttribute.getMTextWithFieldCodes() : attRef.MTextAttribute.Contents);
                    else attValues.Add(attRef.Tag, attRef.HasFields ? attRef.getTextWithFieldCodes() : attRef.TextString);
                    attRef.Erase();
                }
            }
            foreach (AttributeDefinition attDef in attDefs)
            {
                AttributeReference attRef = new AttributeReference();
                attRef.SetAttributeFromBlock(attDef, br.BlockTransform);
                if (attDef.Constant)
                {
                    string textString;
                    if (attRef.IsMTextAttribute) textString = attRef.MTextAttribute.HasFields ? attRef.MTextAttribute.getMTextWithFieldCodes() : attRef.MTextAttribute.Contents;
                    else textString = attRef.HasFields ? attRef.getTextWithFieldCodes() : attRef.TextString;
                    attRef.TextString = textString;
                    //attRef.TextString = attDef.IsMTextAttributeDefinition ?
                    //    attDef.MTextAttributeDefinition.Contents :
                    //    attDef.TextString;
                }
                else if (attValues.ContainsKey(attRef.Tag))
                {
                    attRef.TextString = attValues[attRef.Tag];
                }
                br.AttributeCollection.AppendAttribute(attRef);
                tr.AddNewlyCreatedDBObject(attRef, true);
            }
        }
        public static bool IsPointInsideXY(this Extents3d extents, Point2d pnt)
        => pnt.X >= extents.MinPoint.X && pnt.X <= extents.MaxPoint.X
            && pnt.Y >= extents.MinPoint.Y && pnt.Y <= extents.MaxPoint.Y;
        public static bool IsPointInsideXY(this Extents3d extents, Point3d pnt)
        => pnt.X >= extents.MinPoint.X && pnt.X <= extents.MaxPoint.X
            && pnt.Y >= extents.MinPoint.Y && pnt.Y <= extents.MaxPoint.Y;
        public static Transaction GetTopTx(this Entity ent) => ent.Database.TransactionManager.TopTransaction;
        public static Transaction StartTx(this Entity ent) => ent.Database.TransactionManager.StartTransaction();
        public static List<PropertySet> GetPropertySets(this Entity ent)
        {
            ObjectIdCollection psIds = PropertyDataServices.GetPropertySets(ent);
            List<PropertySet> pss = new List<PropertySet>();
            foreach (Oid oid in psIds) pss.Add(oid.Go<PropertySet>(ent.GetTopTx()));
            return pss;
        }
        public static PolylineVertex3d[] GetVertices(this Polyline3d poly3d, Transaction tr)
        {
            List<PolylineVertex3d> vertices = new List<PolylineVertex3d>();
            foreach (ObjectId id in poly3d)
            {
                var vertex = (PolylineVertex3d)tr.GetObject(id, OpenMode.ForRead);
                if (vertex.VertexType != Vertex3dType.ControlVertex)
                    vertices.Add(vertex);
            }

            return vertices.ToArray();
        }
    }
    public static class ExtensionMethods
    {
        public static T Go<T>(this Oid oid, Transaction tx,
            Autodesk.AutoCAD.DatabaseServices.OpenMode openMode =
            Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead) where T : Autodesk.AutoCAD.DatabaseServices.DBObject
        {
            return (T)tx.GetObject(oid, openMode, false);
        }
        public static Oid AddEntityToDbModelSpace<T>(this T entity, Database db) where T : Autodesk.AutoCAD.DatabaseServices.Entity
        {
            Transaction tx = db.TransactionManager.TopTransaction;
            BlockTableRecord modelSpace = db.GetModelspaceForWrite();
            Oid id = modelSpace.AppendEntity(entity);
            tx.AddNewlyCreatedDBObject(entity, true);
            return id;
        }
        public static string Layer(this Oid oid)
        {
            Transaction tx = Application.DocumentManager.MdiActiveDocument.Database.TransactionManager.TopTransaction;
            if (!oid.ObjectClass.IsDerivedFrom(RXClass.GetClass(typeof(Entity)))) return "";
            return oid.Go<Entity>(tx).Layer;
        }
        public static bool IsDerivedFrom<T>(this Oid oid)
        {
            return oid.ObjectClass.IsDerivedFrom(RXObject.GetClass(typeof(T)));
        }
        public static void ForEach<T>(this Database database, Action<T> action, Transaction tr) where T : Autodesk.AutoCAD.DatabaseServices.Entity
        {
            //using (var tr = database.TransactionManager.StartTransaction())
            //{
            // Get the block table for the current database
            var blockTable = (BlockTable)tr.GetObject(database.BlockTableId, OpenMode.ForRead);

            // Get the model space block table record
            var modelSpace = (BlockTableRecord)tr.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead);

            RXClass theClass = RXObject.GetClass(typeof(T));

            // Loop through the entities in model space
            foreach (Oid objectId in modelSpace)
            {
                // Look for entities of the correct type
                if (objectId.ObjectClass.IsDerivedFrom(theClass))
                {
                    var entity = (T)tr.GetObject(objectId, OpenMode.ForRead);
                    action(entity);
                }
            }
            //tr.Commit();
            //}
        }
        public static List<T> ListOfType<T>(this Database database, Transaction tr, bool discardFrozen = false) where T : Autodesk.AutoCAD.DatabaseServices.Entity
        {
            //using (var tr = database.TransactionManager.StartTransaction())
            //{

            //Init the list of the objects
            List<T> objs = new List<T>();

            // Get the block table for the current database
            var blockTable = (BlockTable)tr.GetObject(database.BlockTableId, OpenMode.ForRead);

            // Get the model space block table record
            var modelSpace = (BlockTableRecord)tr.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead);

            RXClass theClass = RXObject.GetClass(typeof(T));

            // Loop through the entities in model space
            foreach (Oid objectId in modelSpace)
            {
                // Look for entities of the correct type
                if (objectId.ObjectClass.IsDerivedFrom(theClass))
                {
                    var entity = (T)tr.GetObject(objectId, OpenMode.ForRead);
                    if (discardFrozen)
                    {
                        LayerTableRecord layer = (LayerTableRecord)tr.GetObject(entity.LayerId, OpenMode.ForRead);
                        if (layer.IsFrozen) continue;
                    }

                    objs.Add(entity);
                }
            }
            return objs;
            //tr.Commit();
            //}
        }
        public static HashSet<T> HashSetOfType<T>(this Database db, Transaction tr, bool discardFrozen = false)
            where T : Autodesk.AutoCAD.DatabaseServices.Entity
        {
            return new HashSet<T>(db.ListOfType<T>(tr, discardFrozen));
        }
        // Searches the drawing for a block with the specified name.
        // Returns either the block, or null - check accordingly.
        public static HashSet<Autodesk.AutoCAD.DatabaseServices.BlockReference> GetBlockReferenceByName(
            this Database db, string _BlockName)
        {
            HashSet<Autodesk.AutoCAD.DatabaseServices.BlockReference> set =
                    new HashSet<Autodesk.AutoCAD.DatabaseServices.BlockReference>();

            Transaction tx = db.TransactionManager.TopTransaction;

            BlockTable blkTable = tx.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
            BlockTableRecord btr;

            if (blkTable.Has(_BlockName))
            {
                Utils.prdDbg("Block exists!");
                ObjectId BlkRecId = blkTable[_BlockName];

                if (BlkRecId != null)
                {
                    btr = tx.GetObject(BlkRecId, OpenMode.ForRead) as BlockTableRecord;
                    //Utils.prdDbg("Btr opened!");

                    ObjectIdCollection blockRefIds = btr.IsDynamicBlock ? btr.GetAnonymousBlockIds() : btr.GetBlockReferenceIds(true, true);

                    foreach (ObjectId blockRefId in blockRefIds)
                    {
                        if (btr.IsDynamicBlock)
                        {
                            ObjectIdCollection oids2 = blockRefId.Go<BlockTableRecord>(tx).GetBlockReferenceIds(true, true);
                            foreach (Oid oid in oids2)
                            {
                                set.Add(oid.Go<BlockReference>(tx));
                            }
                        }
                        else { set.Add(blockRefId.Go<BlockReference>(tx)); }
                    }
                    //Utils.prdDbg($"Number of refs: {blockRefIds.Count}.");
                }

            }
            return set;
        }
        public static double ToDegrees(this double radians) => (180 / Math.PI) * radians;
        public static double ToRadians(this double degrees) => (Math.PI / 180) * degrees;
    }

    public class PropertySetManager
    {
        private Database Db { get; }
        private DictionaryPropertySetDefinitions DictionaryPropertySetDefinitions { get; }
        private PropertySetDefinition PropertySetDefinition { get; }
        private PropertySet CurrentPropertySet { get; set; }
        public PropertySetManager(Database database, PSetDefs.DefinedSets propertySetName)
        {
            //1
            Db = database;
            //2.1
            DictionaryPropertySetDefinitions = new DictionaryPropertySetDefinitions(Db);
            //2.2
            if (Db.TransactionManager.TopTransaction == null)
            {
                prdDbg("PropertySetManager: Must be instantiated within a Transaction!");
                throw new System.Exception("PropertySetManager: Must be instantiated within a Transaction!");
            }
            //3
            PropertySetDefinition = GetOrCreatePropertySetDefinition(propertySetName);
        }
        private PropertySetDefinition GetOrCreatePropertySetDefinition(PSetDefs.DefinedSets propertySetName)
        {
            if (PropertySetDefinitionExists(propertySetName))
            {
                return GetPropertySetDefinition(propertySetName);
            }
            else return CreatePropertySetDefinition(propertySetName);
        }
        private PropertySetDefinition CreatePropertySetDefinition(PSetDefs.DefinedSets propertySetName)
        {
            string setName = propertySetName.ToString();
            prdDbg($"Defining PropertySet {propertySetName}.");

            //General properties
            PropertySetDefinition propSetDef = new PropertySetDefinition();
            propSetDef.SetToStandard(Db);
            propSetDef.SubSetDatabaseDefaults(Db);
            propSetDef.Description = setName;
            bool isStyle = false;

            PSetDefs pSetDefs = new PSetDefs();
            PSetDefs.PSetDef currentDef = pSetDefs.GetRequestedDef(propertySetName);

            propSetDef.SetAppliesToFilter(currentDef.GetAppliesTo(), isStyle);

            foreach (PSetDefs.Property property in currentDef.ListOfProperties())
            {
                var propDefManual = new PropertyDefinition();
                propDefManual.SetToStandard(Db);
                propDefManual.SubSetDatabaseDefaults(Db);

                propDefManual.Name = property.Name;
                propDefManual.Description = property.Description;
                propDefManual.DataType = property.DataType;
                propDefManual.DefaultData = property.DefaultValue;
                propSetDef.Definitions.Add(propDefManual);
            }

            using (Transaction defTx = Db.TransactionManager.StartTransaction())
            {
                DictionaryPropertySetDefinitions.AddNewRecord(setName, propSetDef);
                defTx.AddNewlyCreatedDBObject(propSetDef, true);
                defTx.Commit();
            }

            return propSetDef;
        }
        private bool PropertySetDefinitionExists(PSetDefs.DefinedSets propertySetName)
        {
            string setName = propertySetName.ToString();
            if (DictionaryPropertySetDefinitions.Has(setName, Db.TransactionManager.TopTransaction))
            {
                prdDbg($"Property Set {setName} already defined.");
                return true;
            }
            else
            {
                prdDbg($"Property Set {setName} is not defined.");
                return false;
            }
        }
        private PropertySetDefinition GetPropertySetDefinition(PSetDefs.DefinedSets propertySetName)
        {
            return DictionaryPropertySetDefinitions
                .GetAt(propertySetName.ToString())
                .Go<PropertySetDefinition>(Db.TransactionManager.TopTransaction);
        }
        public void GetOrAttachPropertySet(Entity ent)
        {
            ObjectIdCollection propertySetIds = PropertyDataServices.GetPropertySets(ent);

            if (propertySetIds.Count == 0)
            {
                CurrentPropertySet = AttachPropertySet(ent);
            }
            else
            {
                foreach (Oid oid in propertySetIds)
                {
                    PropertySet ps = oid.Go<PropertySet>(Db.TransactionManager.TopTransaction);
                    if (ps.PropertySetDefinitionName == this.PropertySetDefinition.Name)
                    { this.CurrentPropertySet = ps; return; }
                }
                //Property set not attached
                CurrentPropertySet = AttachPropertySet(ent);
            }
        }
        private PropertySet AttachPropertySet(Entity ent)
        {
            ent.CheckOrOpenForWrite();
            PropertyDataServices.AddPropertySet(ent, PropertySetDefinition.Id);

            return PropertyDataServices.GetPropertySet(ent, this.PropertySetDefinition.Id)
                .Go<PropertySet>(Db.TransactionManager.TopTransaction);
        }
        public string ReadPropertyString(PSetDefs.Property property)
        {
            int propertyId = this.CurrentPropertySet.PropertyNameToId(property.Name);
            object value = this.CurrentPropertySet.GetAt(propertyId);
            if (value == null) return "";
            else return value.ToString();
        }
        public int ReadPropertyInt(PSetDefs.Property property)
        {
            int propertyId = this.CurrentPropertySet.PropertyNameToId(property.Name);
            object value = this.CurrentPropertySet.GetAt(propertyId);
            if (value == null) return 0;
            else return (int)value;
        }
        public void WritePropertyString(PSetDefs.Property property, string value)
        {
            int propertyId = this.CurrentPropertySet.PropertyNameToId(property.Name);
            this.CurrentPropertySet.CheckOrOpenForWrite();
            this.CurrentPropertySet.SetAt(propertyId, value);
            this.CurrentPropertySet.DowngradeOpen();
        }
        public void WritePropertyObject(PSetDefs.Property property, object value)
        {
            int propertyId = this.CurrentPropertySet.PropertyNameToId(property.Name);
            this.CurrentPropertySet.CheckOrOpenForWrite();
            this.CurrentPropertySet.SetAt(propertyId, value);
            this.CurrentPropertySet.DowngradeOpen();
        }
        public bool FilterPropetyString(Entity ent, PSetDefs.Property property, string value)
        {
            ObjectIdCollection propertySetIds = PropertyDataServices.GetPropertySets(ent);
            PropertySet set = default;

            if (propertySetIds.Count == 0)
            {
                set = AttachPropertySet(ent);
            }
            else
            {
                foreach (Oid oid in propertySetIds)
                {
                    PropertySet ps = oid.Go<PropertySet>(Db.TransactionManager.TopTransaction);
                    if (ps.PropertySetDefinitionName == this.PropertySetDefinition.Name)
                    { set = ps; }
                }
                //Property set not attached
                set = AttachPropertySet(ent);
            }

            int propertyId = set.PropertyNameToId(property.Name);
            object storedValue = set.GetAt(propertyId);
            return value == storedValue.ToString();
        }
        public static void CopyAllProperties(Entity source, Entity target)
        {
            //Only works within drawing
            //ToDo: implement copying from drawing to drawing
            try
            {
                List<PropertySet> sourcePss = source.GetPropertySets();
                DictionaryPropertySetDefinitions sourcePropDefDict
                    = new DictionaryPropertySetDefinitions(source.Database);
                DictionaryPropertySetDefinitions targetPropDefDict
                    = new DictionaryPropertySetDefinitions(target.Database);

                foreach (PropertySet sourcePs in sourcePss)
                {
                    PropertySetDefinition sourcePropSetDef =
                        sourcePs.PropertySetDefinition.Go<PropertySetDefinition>(source.GetTopTx());
                    //Check to see if table is already attached
                    if (!target.GetPropertySets().Contains(sourcePs, new PropertySetNameComparer()))
                    {
                        //If target entity does not have property set attached -> attach
                        //Here can creating the property set definition in the target database be implemented
                        target.CheckOrOpenForWrite();
                        PropertyDataServices.AddPropertySet(target, sourcePropSetDef.Id);
                    }

                    PropertySet targetPs = target.GetPropertySets()
                        .Find(x => x.PropertySetDefinitionName == sourcePs.PropertySetDefinitionName);

                    if (targetPs == null)
                    {
                        prdDbg("PropertySet attachment failed in PropertySetCopyFromEntToEnt!");
                        throw new System.Exception();
                    }

                    foreach (PropertyDefinition pd in sourcePropSetDef.Definitions)
                    {
                        int sourceId = sourcePs.PropertyNameToId(pd.Name);
                        object value = sourcePs.GetAt(sourceId);

                        int targetId = targetPs.PropertyNameToId(pd.Name);
                        targetPs.CheckOrOpenForWrite();
                        targetPs.SetAt(targetId, value);
                        targetPs.DowngradeOpen();
                    }
                }
            }
            catch (System.Exception ex)
            {
                prdDbg(ex.ToString());
                throw;
            }
        }
        private static object ReadNonDefinedPropertySetObject(Entity ent, string propertySetName, string propertyName)
        {
            ObjectIdCollection psIds = PropertyDataServices.GetPropertySets(ent);
            List<PropertySet> pss = new List<PropertySet>();
            foreach (Oid oid in psIds) pss.Add(oid.Go<PropertySet>(ent.GetTopTx()));

            foreach (PropertySet ps in pss)
            {
                if (ps.PropertySetDefinitionName == propertySetName)
                {
                    try
                    {
                        int id = ps.PropertyNameToId(propertyName);
                        object value = ps.GetAt(id);
                        return value;
                    }
                    catch (System.Exception)
                    {
                        return null;
                    }
                }
            }
            //Fall through
            //If no PS found return null
            return null;
        }
        public static double ReadNonDefinedPropertySetDouble(Entity ent, string propertySetName, string propertyName)
        {
            object value = ReadNonDefinedPropertySetObject(ent, propertySetName, propertyName);
            return Convert.ToDouble(value);
        }
        public static int ReadNonDefinedPropertySetInt(Entity ent, string propertySetName, string propertyName)
        {
            object value = ReadNonDefinedPropertySetObject(ent, propertySetName, propertyName);
            return Convert.ToInt32(value);
        }
        public static string ReadNonDefinedPropertySetString(Entity ent, string propertySetName, string propertyName)
        {
            object value = ReadNonDefinedPropertySetObject(ent, propertySetName, propertyName);
            return Convert.ToString(value);
        }
    }

    public class PSetDefs
    {
        public enum DefinedSets
        {
            None,
            DriPipelineData,
            DriSourceReference,
            DriCrossingData,
            DriGasDimOgMat,
            DriOmråder,
            DriComponentsGisData
        }
        public class DriCrossingData : PSetDef
        {
            public DefinedSets SetName { get; } = DefinedSets.DriCrossingData;
            public Property Diameter { get; } = new Property(
                "Diameter",
                "Stores crossing pipe's diameter.",
                PsDataType.Integer,
                0);
            public Property Alignment { get; } = new Property(
                "Alignment",
                "Stores crossing alignment name.",
                PsDataType.Text,
                "");
            public Property SourceEntityHandle { get; } = new Property(
                "SourceEntityHandle",
                "Stores the handle of the crossing entity.",
                PsDataType.Text,
                "");
            public StringCollection AppliesTo { get; } = new StringCollection()
                {
                    RXClass.GetClass(typeof(CogoPoint)).Name
                };
        }
        public class DriSourceReference : PSetDef
        {
            public DefinedSets SetName { get; } = DefinedSets.DriSourceReference;
            public Property SourceEntityHandle { get; } = new Property(
                "SourceEntityHandle",
                "Handle of the source entity which provided information for this entity.",
                PsDataType.Text,
                "");
            public StringCollection AppliesTo { get; } = new StringCollection()
                {
                    RXClass.GetClass(typeof(BlockReference)).Name
                };
        }
        public class DriPipelineData : PSetDef
        {
            public DefinedSets SetName { get; } = DefinedSets.DriPipelineData;
            public Property BelongsToAlignment { get; } = new Property(
                "BelongsToAlignment",
                "Name of the alignment the component belongs to.",
                PsDataType.Text,
                "");
            public Property BranchesOffToAlignment { get; } = new Property(
                "BranchesOffToAlignment",
                "Name of the alignment the component branches off to.",
                PsDataType.Text,
                "");
            public StringCollection AppliesTo { get; } = new StringCollection()
                {
                    RXClass.GetClass(typeof(Polyline)).Name,
                    RXClass.GetClass(typeof(BlockReference)).Name
                };
        }
        public class DriGasDimOgMat : PSetDef
        {
            public DefinedSets SetName { get; } = DefinedSets.DriGasDimOgMat;
            public Property Dimension { get; } = new Property(
                "Dimension",
                "Dimension of the gas pipe.",
                PsDataType.Integer,
                0);
            public Property Material { get; } = new Property(
                "Material",
                "Material of the gas pipe.",
                PsDataType.Text,
                "");
            public Property Bemærk { get; } = new Property(
                "Bemærk",
                "Bemærkning til ledning.",
                PsDataType.Text,
                "");
            public StringCollection AppliesTo { get; } = new StringCollection()
                {
                    RXClass.GetClass(typeof(Polyline)).Name,
                    RXClass.GetClass(typeof(Polyline3d)).Name,
                    RXClass.GetClass(typeof(Line)).Name
                };
        }
        public class DriOmråder : PSetDef
        {
            public DefinedSets SetName { get; } = DefinedSets.DriOmråder;
            public Property Vejnavn { get; } = new Property(
                "Vejnavn",
                "Name of street.",
                PsDataType.Text,
                "");
            public Property Ejerskab { get; } = new Property(
                "Ejerskab",
                "Owner type of street.",
                PsDataType.Text,
                "");
            public Property Vejklasse { get; } = new Property(
                "Vejklasse",
                "Street/road class.",
                PsDataType.Text,
                "");
            public Property Belægning { get; } = new Property(
                "Belægning",
                "Pavement type.",
                PsDataType.Text,
                "");
            public StringCollection AppliesTo { get; } = new StringCollection()
                {
                    RXClass.GetClass(typeof(Polyline)).Name,
                };
        }
        public class DriComponentsGisData : PSetDef
        {
            public DefinedSets SetName { get; } = DefinedSets.DriComponentsGisData;
            public Property BlockName { get; } = new Property("BlockName", "Name of source block", PsDataType.Text, "");
            public Property DN1 { get; } = new Property("DN1", "Main run dimension", PsDataType.Integer, 0);
            public Property DN2 { get; } = new Property("DN2", "Secondary run dimension", PsDataType.Integer, 0);
            public Property Flip { get; } = new Property("Flip", "Describes block's mirror state", PsDataType.Text, "");
            public Property Height { get; } = new Property("Height", "Height of symbol", PsDataType.Real, 0);
            public Property OffsetX { get; } = new Property("OffsetX", "X offset from Origo to CL", PsDataType.Real, 0);
            public Property OffsetY { get; } = new Property("OffsetY", "Y offset from Origo to CL", PsDataType.Real, 0);
            public Property Rotation { get; } = new Property("Rotation", "Rotation of the symbol", PsDataType.Real, 0);
            public Property Serie { get; } = new Property("Serie", "Insulation series of pipes", PsDataType.Text, "");
            public Property System { get; } = new Property("System", "Twin or single", PsDataType.Text, "");
            public Property Type { get; } = new Property("Type", "Type of the component", PsDataType.Text, "");
            public Property Width { get; } = new Property("Width", "Width of symbol", PsDataType.Real, 0);
            public StringCollection AppliesTo { get; } = new StringCollection()
                {
                    RXClass.GetClass(typeof(BlockReference)).Name
                };
        }
        public class PSetDef
        {
            public List<Property> ListOfProperties()
            {
                var propDict = ToPropertyDictionary();
                List<Property> list = new List<Property>();
                foreach (var prop in propDict)
                    if (prop.Value is Property) list.Add((Property)prop.Value);

                return list;
            }
            public Dictionary<string, object> ToPropertyDictionary()
            {
                var dictionary = new Dictionary<string, object>();
                foreach (var propertyInfo in this.GetType().GetProperties())
                    dictionary[propertyInfo.Name] = propertyInfo.GetValue(this, null);
                return dictionary;
            }
            public DefinedSets PSetName()
            {
                var propDict = ToPropertyDictionary();
                return (DefinedSets)propDict["SetName"];
            }
            public StringCollection GetAppliesTo()
            {
                var propDict = ToPropertyDictionary();
                return (StringCollection)propDict["AppliesTo"];
            }
        }
        public class Property
        {
            public string Name { get; }
            public string Description { get; }
            public PsDataType DataType { get; }
            public object DefaultValue { get; }
            public Property(string name, string description, PsDataType dataType, object defaultValue)
            {
                Name = name;
                Description = description;
                DataType = dataType;
                DefaultValue = defaultValue;
            }
        }
        public List<PSetDef> GetPSetClasses()
        {
            var type = this.GetType();
            var types = type.Assembly.GetTypes();
            return types
                .Where(x => x.BaseType != null && x.BaseType.Equals(typeof(PSetDef)))
                .Select(x => Activator.CreateInstance(x))
                .Cast<PSetDef>()
                .ToList();
        }
        public PSetDef GetRequestedDef(DefinedSets requestedSet)
        {
            var list = GetPSetClasses();

            return list.Where(x => x.PSetName() == requestedSet).First();
        }
    }

    public class PropertySetNameComparer : IEqualityComparer<PropertySet>
    {
        public bool Equals(PropertySet x, PropertySet y)
            => x.PropertySetDefinitionName == y.PropertySetDefinitionName;
        public int GetHashCode(PropertySet obj)
            => obj.PropertySetDefinitionName.GetHashCode();
    }
    public class PointDBHorizontalComparer : IEqualityComparer<DBPoint>
    {
        double Tol;

        public PointDBHorizontalComparer(double tol = 0.001)
        {
            Tol = tol;
        }

        public bool Equals(DBPoint a, DBPoint b) => null != a && null != b &&
            a.Position.HorizontalEqualz(b.Position, Tol);

        public int GetHashCode(DBPoint a) => Tuple.Create(
        Math.Round(a.Position.X, 3), Math.Round(a.Position.Y, 3)).GetHashCode();
    }

    public class Point3dHorizontalComparer : IEqualityComparer<Point3d>
    {
        double Tol;

        public Point3dHorizontalComparer(double tol = 0.001)
        {
            Tol = tol;
        }

        public bool Equals(Point3d a, Point3d b) => null != a && null != b &&
            a.HorizontalEqualz(b, Tol);

        public int GetHashCode(Point3d a) => Tuple.Create(
        Math.Round(a.X, 3), Math.Round(a.Y, 3)).GetHashCode();
    }
}
