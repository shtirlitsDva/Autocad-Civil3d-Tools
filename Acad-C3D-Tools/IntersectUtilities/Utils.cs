using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

using oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using static IntersectUtilities.HelperMethods;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;
using ObjectId = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using DataType = Autodesk.Gis.Map.Constants.DataType;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using BlockReference = Autodesk.AutoCAD.DatabaseServices.BlockReference;
using ObjectIdCollection = Autodesk.AutoCAD.DatabaseServices.ObjectIdCollection;

namespace IntersectUtilities
{
    public static class Consts
    {
        /// <summary>
        /// Universal tolerance.
        /// </summary>
        public const double Epsilon = 0.001;
    }
    public static class Utils
    {
        public static TValue GetValueOrDefault<TKey, TValue>
                            (this IDictionary<TKey, TValue> dictionary, TKey key, TValue defaultValue)
        {
            TValue value;
            return dictionary.TryGetValue(key, out value) ? value : defaultValue;
        }

        public static IEnumerable<T> ExceptWhere<T>(
            this IEnumerable<T> source, Predicate<T> predicate) => source.Where(x => !predicate(x));

        public static Dictionary<TValue, TKey> ToInvertedDictionary
                <TKey, TValue>(this IDictionary<TKey, TValue> source) =>
                source.ToDictionary(x => x.Value, x => x.Key);

        public static void ClrFile(string fullPathAndName)
        {
            //Clear the output file
            System.IO.File.WriteAllBytes(fullPathAndName, new byte[0]);
        }
        public static void OutputWriter(string fullPathAndName, string sr)
        {
            //Create filename
            //string filename = fullpathand;

            // Write to output file
            using (StreamWriter w = new StreamWriter(fullPathAndName, true, Encoding.UTF8))
            {
                w.Write(sr);
                w.Close();
            }
        }
        /// <summary>
        /// Reads a string value from supplied datatable.
        /// </summary>
        /// <param name="key">Key column.</param>
        /// <param name="table">DataTable to read.</param>
        /// <param name="parameter">Column name to read from.</param>
        /// <param name="keyColumnIdx">Usually 0, but can be set to any column index.</param>
        /// <returns>The read value or NULL.</returns>
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
            else return null;
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

        public static System.Data.DataTable READExcel(string path)
        {
            Microsoft.Office.Interop.Excel.Application objXL = null;
            Microsoft.Office.Interop.Excel.Workbook objWB = null;
            objXL = new Microsoft.Office.Interop.Excel.Application();
            objWB = objXL.Workbooks.Open(path);
            Microsoft.Office.Interop.Excel.Worksheet objSHT = objWB.Worksheets[1];

            int rows = objSHT.UsedRange.Rows.Count;
            int cols = objSHT.UsedRange.Columns.Count;
            System.Data.DataTable dt = new System.Data.DataTable();
            int noofrow = 1;

            for (int c = 1; c <= cols; c++)
            {
                string colname = objSHT.Cells[1, c].Text;
                dt.Columns.Add(colname);
                noofrow = 2;
            }

            for (int r = noofrow; r <= rows; r++)
            {
                DataRow dr = dt.NewRow();
                for (int c = 1; c <= cols; c++)
                {
                    dr[c - 1] = objSHT.Cells[r, c].Text;
                }

                dt.Rows.Add(dr);
            }

            objWB.Close();
            objXL.Quit();
            return dt;
        }

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
                                          oid id, MapValue[] values)
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
        public static bool AddEmptyODRecord(Autodesk.Gis.Map.ObjectData.Table table, oid id)
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
                                          oid id, MapValue originalValue)
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
                                          oid id, T value)
        {
            try
            {
                Autodesk.Gis.Map.ObjectData.Table table = tables[tableName];

                ErrorCode errCode = ErrorCode.OK;

                bool success = true;

                // Get and Initialize Records
                using (Records records = tables.GetObjectRecords
                    (0, id, Autodesk.Gis.Map.Constants.OpenMode.OpenForWrite, false))
                {
                    if (records.Count == 0)
                    {
                        //Editor.WriteMessage($"\nThere is no ObjectData record attached on the entity.");
                        return false;
                    }

                    // Iterate through all records
                    foreach (Record record in records)
                    {
                        // Get record info
                        for (int i = 0; i < record.Count; i++)
                        {
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
                                          oid id, MapValue value)
        {
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;

            try
            {
                Autodesk.Gis.Map.ObjectData.Table table = tables[tableName];

                ErrorCode errCode = ErrorCode.OK;

                bool success = false;

                // Get and Initialize Records
                using (Records records = tables.GetObjectRecords
                    (0, id, Autodesk.Gis.Map.Constants.OpenMode.OpenForWrite, false))
                {
                    if (records.Count == 0)
                    {
                        Editor.WriteMessage($"\nThere is no ObjectData record attached on the entity.");
                        return false;
                    }

                    // Iterate through all records
                    foreach (Record record in records)
                    {
                        // Get record info
                        for (int i = 0; i < record.Count; i++)
                        {
                            FieldDefinitions tableDef = table.FieldDefinitions;
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
            catch (MapException)
            {
                return false;
            }
        }

        /// <summary>
        /// Prints the records obtained by id.
        /// </summary>
        public static MapValue ReadRecordData(Tables tables, oid id, string tableName, string columnName)
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
                        //Editor.WriteMessage($"\nThere is no ObjectData record attached on the entity.");
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

        public static int ReadIntPropertyValue(Tables tables, oid id, string tableName, string columnName)
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

        public static double ReadDoublePropertyValue(Tables tables, oid id, string tableName, string columnName)
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

        public static string ReadStringPropertyValue(Tables tables, oid id, string tableName, string columnName)
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

        public static string ReadPropertyToStringValue(Tables tables, oid id, string tableName, string columnName)
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

        public static bool DoesRecordExist(Tables tables, oid id, string columnName)
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

        public static void CopyAllOD(Tables tables, oid sourceId, oid targetId)
        {
            using (Records records = tables.GetObjectRecords(
                   0, sourceId, Autodesk.Gis.Map.Constants.OpenMode.OpenForRead, false))
            {
                if (records == null || records.Count == 0) return;

                //Editor.WriteMessage($"\nEntity: {entSource.Handle}");

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
                        Editor.WriteMessage($"\n{ErrorText}: {ex.Message}: {ex.Source}: {ex.StackTrace}");
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
                    if (DoesRecordExist(tables, entTarget.ObjectId, item.columnName))
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

        public static Editor Editor
        {
            get
            {
                return Application.DocumentManager.MdiActiveDocument.Editor;
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
        #endregion

        /// <summary>
        /// Gets all vertices of a polyline.
        /// </summary>
        /// <remarks>
        /// For a polyline, the difference between this method and `GetPoints()` is when `IsClosed=true`.
        /// </remarks>
        /// <param name="poly">The polyline.</param>
        /// <returns>The points.</returns>
        public static IEnumerable<Point3d> GetPolyPoints(this Polyline poly)
        {
            for (int i = 0; i < poly.NumberOfVertices; i++)
            {
                yield return poly.GetPoint3dAt(i);
            }
        }

        /// <summary>
        /// Cleans up a polyline by removing duplicate points.
        /// </summary>
        /// <param name="poly">The polyline.</param>
        /// <returns>The number of points removed.</returns>
        public static int PolyClean_RemoveDuplicatedVertex(this Polyline poly)
        {
            var points = poly.GetPolyPoints().ToArray();
            var dupIndices = new List<int>();
            for (int i = points.Length - 2; i >= 0; i--)
            {
                if (points[i].DistanceTo(points[i + 1]) < Consts.Epsilon)
                {
                    dupIndices.Add(i);
                }
            }

            if (dupIndices.Count > 0)
            {
                poly.UpgradeOpen();
                dupIndices.ForEach(index => poly.RemoveVertexAt(index));
                poly.DowngradeOpen();
            }
            return dupIndices.Count;
        }

        #region Poly3d modify vertices
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
        #endregion

        public static List<T> FilterForCrossingEntities<T>(List<T> entList, Alignment alignment) where T : Entity
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database db = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Plane plane = new Plane();
            //Gather the intersected objectIds
            List<Entity> returnList = new List<Entity>();
            foreach (Entity ent in entList)
            {
                using (Point3dCollection p3dcol = new Point3dCollection())
                {
                    alignment.IntersectWith(ent, 0, plane, p3dcol, new IntPtr(0), new IntPtr(0));

                    //Create feature line if there's an intersection and
                    //if the type of the layer is not "IGNORE"
                    if (p3dcol.Count > 0)
                    {
                        returnList.Add(ent);
                    }
                }
            }
            return returnList.Cast<T>().ToList();
        }

        public static HashSet<T> FilterForCrossingEntities<T>(HashSet<T> entList, Alignment alignment) where T : Entity
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database db = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Plane plane = new Plane();
            //Gather the intersected objectIds
            HashSet<Entity> returnList = new HashSet<Entity>();
            foreach (Entity ent in entList)
            {
                using (Point3dCollection p3dcol = new Point3dCollection())
                {
                    alignment.IntersectWith(ent, 0, plane, p3dcol, new IntPtr(0), new IntPtr(0));

                    if (p3dcol.Count > 0)
                    {
                        returnList.Add(ent);
                    }
                }
            }
            return returnList.Cast<T>().ToHashSet();
        }

        public static void prdDbg(string msg)
        {
            DocumentCollection docCol = Application.DocumentManager;
            Editor editor = docCol.MdiActiveDocument.Editor;
            editor.WriteMessage("\n" + msg);
        }

        /// <summary>
        /// Returns the matched string and value with curly braces removed.
        /// </summary>
        /// <param name="input">String where to find groups.</param>
        /// <returns>List of tuples or empty list.</returns>
        public static List<(string, string)> FindDescriptionParts(string input)
        {
            List<(string, string)> list = new List<(string, string)>();
            Regex regex = new Regex(@"({[a-zæøåA-ZÆØÅ_:-]*})");

            MatchCollection mc = regex.Matches(input);

            if (mc.Count > 0)
            {
                foreach (Match match in mc)
                {
                    string toReplace = match.Groups[0].Value;
                    string columnName = toReplace.Replace("{", "").Replace("}", "");
                    list.Add((toReplace, columnName));
                }
                return list;
            }
            else return list;
        }

        public static string ReadDescriptionPartsFromOD(Tables tables, Entity ent,
                                                        string ColumnName, System.Data.DataTable dataTable)
        {
            string readStructure = ReadStringParameterFromDataTable(ent.Layer, dataTable, ColumnName, 0);
            if (readStructure != null)
            {
                List<(string ToReplace, string Data)> list = FindDescriptionParts(readStructure);

                if (list.Count > 0)
                {
                    //Assume only one result
                    string[] parts = list[0].Data.Split(':');
                    string value = ReadPropertyToStringValue(tables, ent.ObjectId, parts[0], parts[1]);
                    if (value.IsNotNoE())
                    {
                        string result = readStructure.Replace(list[0].ToReplace, value);
                        return result;
                    }
                }
            }
            return "";
        }

        public static ObjectId AddToBlock(Entity entity, ObjectId btrId)
        {
            using (Transaction tr = btrId.Database.TransactionManager.StartTransaction())
            {
                try
                {
                    BlockTableRecord btr = tr.GetObject(
                        btrId,
                        OpenMode.ForWrite)
                            as BlockTableRecord;

                    ObjectId id = btr.AppendEntity(entity);

                    tr.AddNewlyCreatedDBObject(entity, true);

                    tr.Commit();

                    return id;
                }
                catch (Autodesk.AutoCAD.Runtime.Exception ex)
                {
                    Autodesk.AutoCAD.Runtime.ErrorStatus es = ex.ErrorStatus;

                    tr.Abort();
                    return ObjectId.Null;
                }
                catch (System.Exception ex)
                {
                    string error = ex.Message;

                    tr.Abort();
                    return ObjectId.Null;
                }
            }
        }

        public static bool EraseBlock(Document doc, string blkName)
        {
            Database db = doc.Database;
            Editor ed = doc.Editor;

            try
            {
                var blkId = GetBlkId(db, blkName);

                if (!blkId.IsNull)
                {
                    EraseBlkRefs(blkId);
                    EraseBlk(blkId);
                }
                return true;
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage(ex.Message);
                return false;
            }
        }

        private static ObjectId GetBlkId(Database db, string blkName)
        {

            ObjectId blkId = ObjectId.Null;

            if (db == null)
                return ObjectId.Null;

            if (string.IsNullOrWhiteSpace(blkName))
                return ObjectId.Null;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                if (bt.Has(blkName))
                    blkId = bt[blkName];
                tr.Commit();
            }
            return blkId;
        }
        private static bool EraseBlkRefs(ObjectId blkId)
        {
            bool blkRefsErased = false;

            if (blkId.IsNull)
                return false;

            Database db = blkId.Database;
            if (db == null)
                return false;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTableRecord blk = (BlockTableRecord)tr.GetObject(blkId, OpenMode.ForRead);
                var blkRefs = blk.GetBlockReferenceIds(true, true);
                if (blkRefs != null && blkRefs.Count > 0)
                {
                    foreach (ObjectId blkRefId in blkRefs)
                    {
                        Autodesk.AutoCAD.DatabaseServices.BlockReference blkRef =
                            (Autodesk.AutoCAD.DatabaseServices.BlockReference)tr.GetObject(blkRefId, OpenMode.ForWrite);
                        blkRef.Erase();
                    }
                    blkRefsErased = true;
                }
                tr.Commit();
            }
            return blkRefsErased;
        }
        private static bool EraseBlk(ObjectId blkId)
        {
            bool blkIsErased = false;

            if (blkId.IsNull)
                return false;

            Database db = blkId.Database;
            if (db == null)
                return false;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {

                BlockTableRecord blk = (BlockTableRecord)tr.GetObject(blkId, OpenMode.ForRead);
                var blkRefs = blk.GetBlockReferenceIds(true, true);
                if (blkRefs == null || blkRefs.Count == 0)
                {
                    blk.UpgradeOpen();
                    blk.Erase();
                    blkIsErased = true;
                }
                tr.Commit();
            }
            return blkIsErased;
        }
        public static string GetEtapeName(Editor editor)
        {
            #region Read Csv for paths
            string pathStier = "X:\\AutoCAD DRI - 01 Civil 3D\\Stier.csv";
            System.Data.DataTable dtStier = CsvReader.ReadCsvToDataTable(pathStier, "Stier");
            #endregion

            List<string> kwds = new List<string>(dtStier.Rows.Count);
            foreach (DataRow row in dtStier.Rows)
                kwds.Add((string)row["Etape"]);

            PromptKeywordOptions pKeyOpts = new PromptKeywordOptions("");
            pKeyOpts.Message = "\nVælg etape: ";
            foreach (string kwd in kwds)
            {
                pKeyOpts.Keywords.Add(kwd);
            }
            pKeyOpts.AllowNone = true;
            pKeyOpts.Keywords.Default = kwds[0];
            PromptResult pKeyRes = editor.GetKeywords(pKeyOpts);

            return pKeyRes.StringResult;
        }
        /// <summary>
        /// Returns path to dwg file.
        /// </summary>
        /// <param name="etapeName">4.1 .. 4.12</param>
        /// <param name="pathType">Ler, Surface</param>
        /// <returns>Path as string</returns>
        public static string GetPathToDataFiles(string etapeName, string pathType)
        {
            #region Read Csv Data for paths
            string pathStier = "X:\\AutoCAD DRI - 01 Civil 3D\\Stier.csv";
            System.Data.DataTable dtStier = CsvReader.ReadCsvToDataTable(pathStier, "Stier");
            #endregion

            return ReadStringParameterFromDataTable(etapeName, dtStier, pathType, 0);
        }
        public static int GetLineNumber([CallerLineNumber] int lineNumber = 0) => lineNumber;
        public static bool IsFileLockedOrReadOnly(FileInfo fi)
        {
            FileStream fs = null;
            try
            {
                fs = fi.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }

            catch (System.Exception ex)
            {
                if (ex is IOException || ex is UnauthorizedAccessException)
                {
                    return true;
                }
                throw;
            }
            finally
            {
                if (fs != null) fs.Close();
            }
            // File is accessible
            return false;
        }
    }
    public static class Enums
    {
        public enum ElevationInputMethod
        {
            None,
            Manual,
            Text,
            OnOtherPl3d,
            CalculateFromSlope
        }
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

    public class StationPoint
    {
        public CogoPoint CogoPoint { get; }
        public double Station;
        private double Offset;
        public int ProfileViewNumber { get; set; } = 0;

        public StationPoint(CogoPoint cogoPoint, Alignment alignment)
        {
            CogoPoint = cogoPoint;
            alignment.StationOffset(cogoPoint.Location.X, cogoPoint.Location.Y,
                                              ref Station, ref Offset);
        }
    }

    public class SizeManager
    {
        public string FirstPosition { get; private set; }
        public string SecondPosition { get; private set; } = "0";
        public SizeManager(string input)
        {
            if (input.Contains("x"))
            {
                var output = input.Split('x');
                int firstNumber = int.Parse(output[0]);
                int secondNumber = int.Parse(output[1]);
                FirstPosition = firstNumber >= secondNumber ? firstNumber.ToString() : secondNumber.ToString();
                SecondPosition = firstNumber >= secondNumber ? secondNumber.ToString() : firstNumber.ToString();
            }
            else FirstPosition = input;
        }
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

        public static double DistanceHorizontalTo(this Point3d sourceP3d, Point3d targetP3d)
        {
            double X1 = sourceP3d.X;
            double Y1 = sourceP3d.Y;
            double X2 = targetP3d.X;
            double Y2 = targetP3d.Y;
            return Math.Sqrt(Math.Pow((X2 - X1), 2) + Math.Pow((Y2 - Y1), 2));
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
                if (br.Name == "MuffeIntern" ||
                    br.Name == "MuffeIntern2" ||
                    br.Name == "MuffeIntern3")
                {
                    tr.Abort();
                    return new ObjectIdCollection();
                }
                ids = br.ExplodeToOwnerSpace3();

                if (erase)
                {
                    Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage(
                        $"\n{br.Name}");
                    br.UpgradeOpen();
                    br.Erase();
                }

                tr.Commit();
            }

            return ids;
        }
    }

    public static class ExtensionMethods
    {
        public static T Go<T>(this oid Oid, Transaction tx,
            Autodesk.AutoCAD.DatabaseServices.OpenMode openMode =
            Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead) where T : Autodesk.AutoCAD.DatabaseServices.Entity
        {
            return (T)tx.GetObject(Oid, openMode, false);
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
            foreach (oid objectId in modelSpace)
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
            foreach (oid objectId in modelSpace)
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

            using (Transaction _trans = db.TransactionManager.StartTransaction())
            {

                BlockTable blkTable = _trans.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord blkRecord;

                if (blkTable.Has(_BlockName))
                {
                    ObjectId BlkRecId = blkTable[_BlockName];

                    if (BlkRecId != null)
                    {
                        blkRecord = _trans.GetObject(BlkRecId, OpenMode.ForRead) as BlockTableRecord;

                        Autodesk.AutoCAD.DatabaseServices.ObjectIdCollection blockRefIds =
                            blkRecord.GetBlockReferenceIds(true, true);

                        foreach (ObjectId blockRefId in blockRefIds)
                        {
                            if ((_trans.GetObject(blockRefId, OpenMode.ForRead) as
                                Autodesk.AutoCAD.DatabaseServices.BlockReference).Name ==
                                _BlockName && (_trans.GetObject(blockRefId, OpenMode.ForRead) != null))
                            {
                                set.Add(_trans.GetObject(blockRefId, OpenMode.ForRead) as
                                    Autodesk.AutoCAD.DatabaseServices.BlockReference);
                            }
                        }
                    }

                }
                _trans.Commit();
            }
            return set;
        }
    }

    public static class HelperMethods
    {
        public static bool IsFullPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || path.IndexOfAny(Path.GetInvalidPathChars()) != -1 || !Path.IsPathRooted(path))
                return false;

            var pathRoot = Path.GetPathRoot(path);
            if (pathRoot.Length <= 2 && pathRoot != "/") // Accepts X:\ and \\UNC\PATH, rejects empty string, \ and X:, but accepts / to support Linux
                return false;

            return !(pathRoot == path && pathRoot.StartsWith("\\\\") && pathRoot.IndexOf('\\', 2) == -1); // A UNC server name without a share name (e.g "\\NAME") is invalid
        }

        public static string GetAbsolutePath(String basePath, String path)
        {
            if (path == null)
                return null;
            if (basePath == null)
                basePath = Path.GetFullPath("."); // quick way of getting current working directory
            else
                basePath = GetAbsolutePath(null, basePath); // to be REALLY sure ;)
            string finalPath;
            // specific for windows paths starting on \ - they need the drive added to them.
            // I constructed this piece like this for possible Mono support.
            if (!Path.IsPathRooted(path) || "\\".Equals(Path.GetPathRoot(path)))
            {
                if (path.StartsWith(Path.DirectorySeparatorChar.ToString()))
                    finalPath = Path.Combine(Path.GetPathRoot(basePath), path.TrimStart(Path.DirectorySeparatorChar));
                else
                    finalPath = Path.Combine(basePath, path);
            }
            else
                finalPath = path;
            // resolves any internal "..\" to get the true full path.
            return Path.GetFullPath(finalPath);
        }
    }
}
