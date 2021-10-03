using System;
using System.Diagnostics;
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
using DBObject = Autodesk.AutoCAD.DatabaseServices.DBObject;
using Autodesk.AutoCAD.Colors;

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
                                          oid id, MapValue value)
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
                        Editor.WriteMessage($"\nThere is no ObjectData record attached on the entity.");
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

        public static bool DoesRecordExist(Tables tables, oid id, string tableName, string columnName)
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

        public static bool CheckAddUpdateRecordValue(
            Tables tables,
            oid entId,
            string m_tableName,
            string columnName,
            MapValue value)
        {
            if (DoesRecordExist(tables, entId, m_tableName, columnName))
            {
                Editor.WriteMessage($"\nRecord {columnName} already exists, updating...");

                if (UpdateODRecord(tables, m_tableName, columnName, entId, value))
                {
                    Editor.WriteMessage($"\nUpdating record {columnName} succeded!");
                    return true;
                }
                else
                {
                    Editor.WriteMessage($"\nUpdating record {columnName} failed!");
                    return false;
                }
            }
            else if (AddODRecord(tables, m_tableName, columnName, entId, value))
            {
                Editor.WriteMessage($"\nAdding record {columnName} succeded!");
                return true;
            }
            return false;
        }
        #endregion
        #region Xrecord functions
        public static void XrecordCreateWriteUpdateString(
            Autodesk.AutoCAD.DatabaseServices.DBObject obj,
            string xRecName, string[] valuesToWrite)
        {
            oid extId = obj.ExtensionDictionary;
            if (extId == oid.Null)
            {
                obj.CheckOrOpenForWrite();
                obj.CreateExtensionDictionary();
                extId = obj.ExtensionDictionary;
            }

            Transaction tx = obj.Database.TransactionManager.TopTransaction;

            DBDictionary dbExt = extId.Go<DBDictionary>(tx, OpenMode.ForWrite);

            Xrecord xRec;
            if (dbExt.Contains(xRecName))
            {
                oid xRecId = dbExt.GetAt(xRecName);
                xRec = xRecId.Go<Xrecord>(tx, OpenMode.ForWrite);
            }
            else
            {
                xRec = new Xrecord();
                dbExt.SetAt(xRecName, xRec);
                tx.AddNewlyCreatedDBObject(xRec, true);
            }

            ResultBuffer rb = new ResultBuffer();
            for (int i = 0; i < valuesToWrite.Length; i++)
            {
                rb.Add(new TypedValue(
                    (int)DxfCode.ExtendedDataAsciiString, valuesToWrite[i]));
            }

            xRec.Data = rb;
        }
        public static bool XrecCopyTo(DBObject sourceObj, DBObject targetObj, string xRecordName)
        {
            Transaction sourceTx = sourceObj.Database.TransactionManager.TopTransaction;
            oid sourceExtId = sourceObj.ExtensionDictionary;
            if (sourceExtId == oid.Null)
            {
                prdDbg($"DBObject with handle {sourceObj.Handle} does not have extension dictionary!");
                return false;
            }
            DBDictionary sourceDbExt = sourceExtId.Go<DBDictionary>(sourceTx);
            Xrecord sourceXrec;
            if (sourceDbExt.Contains(xRecordName))
            {
                oid sourceXrecId = sourceDbExt.GetAt(xRecordName);
                sourceXrec = sourceXrecId.Go<Xrecord>(sourceTx);

                Transaction targetTx = targetObj.Database.TransactionManager.TopTransaction;
                oid targetExtId = targetObj.ExtensionDictionary;
                if (targetExtId == oid.Null)
                {
                    targetObj.CheckOrOpenForWrite();
                    targetObj.CreateExtensionDictionary();
                    targetExtId = targetObj.ExtensionDictionary;
                }
                DBDictionary targetDbExt = targetExtId.Go<DBDictionary>(targetTx, OpenMode.ForWrite);
                Xrecord targetXrec;
                if (targetDbExt.Contains(xRecordName))
                {
                    oid targetXrecId = targetDbExt.GetAt(xRecordName);
                    targetXrec = targetXrecId.Go<Xrecord>(targetTx, OpenMode.ForWrite);
                }
                else
                {
                    targetXrec = new Xrecord();
                    targetDbExt.SetAt(xRecordName, targetXrec);
                    targetTx.AddNewlyCreatedDBObject(targetXrec, true);
                }

                targetXrec.Data = sourceXrec.Data;
                return true;
            }
            else
            {
                prdDbg($"DBObject with handle {sourceObj.Handle} does not have xrecord {xRecordName}!");
                return false;
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
        public static string GetEtapeName(string projectName)
        {
            DocumentCollection docCol = Application.DocumentManager;
            Editor editor = docCol.MdiActiveDocument.Editor;

            #region Read Csv for paths
            string pathStier = "X:\\AutoCAD DRI - 01 Civil 3D\\Stier.csv";
            System.Data.DataTable dtStier = CsvReader.ReadCsvToDataTable(pathStier, "Stier");
            #endregion

            var query = dtStier.AsEnumerable()
                .Where(row => (string)row["PrjId"] == projectName);

            HashSet<string> kwds = new HashSet<string>();
            foreach (DataRow row in query)
                kwds.Add((string)row["Etape"]);

            PromptKeywordOptions pKeyOpts = new PromptKeywordOptions("");
            pKeyOpts.Message = "\nVælg etape: ";
            foreach (string kwd in kwds)
            {
                pKeyOpts.Keywords.Add(kwd);
            }
            pKeyOpts.AllowNone = true;
            pKeyOpts.Keywords.Default = kwds.First();
            PromptResult pKeyRes = editor.GetKeywords(pKeyOpts);

            return pKeyRes.StringResult;
        }
        /// <summary>
        /// Returns path to dwg file.
        /// </summary>
        /// <param name="etapeName">4.1 .. 4.12</param>
        /// <param name="pathType">Ler, Surface</param>
        /// <returns>Path as string</returns>
        public static string GetPathToDataFiles(
            string projectName, string etapeName, string pathType)
        {
            #region Read Csv Data for paths
            string pathStier = "X:\\AutoCAD DRI - 01 Civil 3D\\Stier.csv";
            System.Data.DataTable dtStier = CsvReader.ReadCsvToDataTable(pathStier, "Stier");
            #endregion

            var query = dtStier.AsEnumerable()
                .Where(row =>
                (string)row["PrjId"] == projectName &&
                (string)row["Etape"] == etapeName);

            if (query.Count() != 1)
            {
                prdDbg("GetPathToDataFiles could not determine Etape!");
                return "";
            }

            return (string)query.First()[pathType];
        }
        public static string GetProjectName()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Editor editor = docCol.MdiActiveDocument.Editor;

            #region Read Csv for paths
            string pathStier = "X:\\AutoCAD DRI - 01 Civil 3D\\Stier.csv";
            System.Data.DataTable dtStier =
                CsvReader.ReadCsvToDataTable(pathStier, "Stier");
            #endregion

            HashSet<string> kwds = new HashSet<string>();
            foreach (DataRow row in dtStier.Rows)
                kwds.Add(((string)row["PrjId"]));

            string msg = "\nVælg projekt [";
            string keywordsJoined = string.Join("/", kwds);
            msg = msg + keywordsJoined + "]: ";

            string displayKewords = string.Join(" ", kwds);

            PromptKeywordOptions pKeyOpts = new PromptKeywordOptions(msg, displayKewords);
            //pKeyOpts.Message = "\nVælg projekt: ";
            //foreach (string kwd in kwds)
            //{
            //    pKeyOpts.Keywords.Add(kwd, kwd, kwd);
            //}
            pKeyOpts.AllowNone = false;
            pKeyOpts.AllowArbitraryInput = true;
            //for (int i = 0; i < pKeyOpts.Keywords.Count; i++)
            //{
            //    prdDbg("\nLocal name: " + pKeyOpts.Keywords[i].LocalName);
            //    prdDbg("\nGlobal name: " + pKeyOpts.Keywords[i].GlobalName);
            //    prdDbg("\nDisplay name: " + pKeyOpts.Keywords[i].DisplayName);
            //}

            //pKeyOpts.Keywords.Default = kwds[0];
            PromptResult pKeyRes = editor.GetKeywords(pKeyOpts);
            //For some reason keywords returned are only the first part, so this is a workaround
            //Depends on what input is
            //The project name must start with the project number
            //If the code returns wrong values, there must be something wrong with project names
            //Like same project number and/or occurence of same substring in two or more keywords
            //This is a mess...
            string returnedPartOfTheKeyword = pKeyRes.StringResult;
            foreach (string kwd in kwds)
            {
                if (kwd.Contains(returnedPartOfTheKeyword)) return kwd;
            }
            return "";
        }
        /// <summary>
        /// Gets the working folder path for selected project
        /// </summary>
        /// <param name="projectName">The name of the project</param>
        /// <param name="workingFolder">The name of the column to read</param>
        /// <returns>Returns the path to working folder</returns>
        public static string GetWorkingFolder(string projectName, string workingFolder = "WorkingFolder")
        {
            #region Read Csv for paths
            string pathWF = "X:\\AutoCAD DRI - 01 Civil 3D\\Stier.csv";
            System.Data.DataTable dtWF = CsvReader.ReadCsvToDataTable(pathWF, "WF");
            #endregion

            return ReadStringParameterFromDataTable(projectName, dtWF, workingFolder, 0);
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
        public static string ConvertStringEncoding(string txt, Encoding srcEncoding, Encoding dstEncoding)
        {
            //From here: https://stevemcgill.nl/blog/convert-windows1252-and-iso88591-to-utf8/
            if (string.IsNullOrEmpty(txt)) return txt;
            if (srcEncoding == null) throw new System.ArgumentNullException(nameof(srcEncoding));
            if (dstEncoding == null) throw new System.ArgumentNullException(nameof(dstEncoding));

            var srcBytes = srcEncoding.GetBytes(txt);
            var dstBytes = Encoding.Convert(srcEncoding, dstEncoding, srcBytes);
            return dstEncoding.GetString(dstBytes);
        }
        private static void GetAllXrefNames(GraphNode i_root, List<string> list, Transaction i_Tx)
        {
            for (int o = 0; o < i_root.NumOut; o++)
            {
                XrefGraphNode child = i_root.Out(o) as XrefGraphNode;
                if (child.XrefStatus == XrefStatus.Resolved)
                {
                    BlockTableRecord bl = i_Tx.GetObject(child.BlockTableRecordId, OpenMode.ForRead) as BlockTableRecord;
                    list.Add(child.Database.Filename);
                    // Name of the Xref (found name)
                    // You can find the original path too:
                    //if (bl.IsFromExternalReference == true)
                    // i_ed.WriteMessage("\n" + i_indent + "Xref path name: "
                    //                      + bl.PathName);
                    GetAllXrefNames(child, list, i_Tx);
                }
            }
        }
        public static Profile CreateProfileFromPolyline(
            string profileName,
            ProfileView profileView,
            string alignmentName,
            string layerName,
            string styleName,
            string labelSetName,
            Polyline sourcePolyline
            )
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            oid profByLayout = Profile.CreateByLayout(
                profileName,
                civilDoc,
                alignmentName,
                layerName,
                styleName,
                labelSetName
                );

            Transaction tx = localDb.TransactionManager.TopTransaction;

            Profile profile = tx.GetObject(profByLayout, OpenMode.ForWrite) as Profile;

            if (sourcePolyline != null)
            {
                int numOfVert = sourcePolyline.NumberOfVertices - 1;
                Point2d point2d1;
                Point2d point2d2;
                Point2d point2d3;
                double x = 0.0, y = 0.0;

                if (profileView.ElevationRangeMode == ElevationRangeType.Automatic)
                {
                    profileView.ElevationRangeMode = ElevationRangeType.UserSpecified;
                    profileView.FindXYAtStationAndElevation(profileView.StationStart, profileView.ElevationMin, ref x, ref y);
                }
                else
                    profileView.FindXYAtStationAndElevation(profileView.StationStart, profileView.ElevationMin, ref x, ref y);

                ProfileViewStyle profileViewStyle = tx
                       .GetObject(profileView.StyleId, OpenMode.ForRead) as ProfileViewStyle;

                for (int i = 0; i < numOfVert; i++)
                {
                    switch (sourcePolyline.GetSegmentType(i))
                    {
                        case SegmentType.Line:
                            LineSegment2d lineSegment2dAt = sourcePolyline.GetLineSegment2dAt(i);
                            point2d1 = lineSegment2dAt.StartPoint;
                            double x1 = point2d1.X;
                            double y1 = point2d1.Y;
                            double num4 = x1 - x;
                            double num5 = (y1 - y) / profileViewStyle.GraphStyle.VerticalExaggeration + profileView.ElevationMin;
                            point2d2 = new Point2d(num4, num5);

                            point2d1 = lineSegment2dAt.EndPoint;
                            double x2 = point2d1.X;
                            double y2 = point2d1.Y;
                            double num6 = x2 - x;
                            double num7 = (y2 - y) / profileViewStyle.GraphStyle.VerticalExaggeration + profileView.ElevationMin;
                            point2d3 = new Point2d(num6, num7);

                            profile.Entities.AddFixedTangent(point2d2, point2d3);
                            break;
                        case SegmentType.Arc:
                            CircularArc2d arcSegment2dAt = sourcePolyline.GetArcSegment2dAt(i);

                            point2d1 = arcSegment2dAt.StartPoint;
                            double x3 = point2d1.X;
                            double y3 = point2d1.Y;
                            point2d1 = arcSegment2dAt.EndPoint;
                            double x4 = point2d1.X;
                            double y4 = point2d1.Y;

                            double num8 = x3 - x;
                            double num9 = (y3 - y) / profileViewStyle.GraphStyle.VerticalExaggeration + profileView.ElevationMin;
                            double num10 = x4 - x;
                            double num11 = (y4 - y) / profileViewStyle.GraphStyle.VerticalExaggeration + profileView.ElevationMin;

                            Point2d samplePoint = ((Curve2d)arcSegment2dAt).GetSamplePoints(11)[5];
                            double num12 = samplePoint.X - x;
                            double num13 = (samplePoint.Y - y) / profileViewStyle.GraphStyle.VerticalExaggeration + profileView.ElevationMin;

                            Point2d point2d4 = new Point2d(num12, num13);
                            point2d3 = new Point2d(num10, num11);
                            point2d2 = new Point2d(num8, num9);
                            profile.Entities.AddFixedSymmetricParabolaByThreePoints(point2d2, point2d4, point2d3);

                            break;
                        case SegmentType.Coincident:
                            break;
                        case SegmentType.Point:
                            break;
                        case SegmentType.Empty:
                            break;
                        default:
                            break;
                    }
                }
            }
            return profile;
        }
        public static HashSet<(Entity ent, double dist)> CreateDistTuples(Point3d location, HashSet<Entity> ents)
        {
            HashSet<(Entity ent, double dist)> distTuples = new HashSet<(Entity ent, double dist)>();
            foreach (Entity ent in ents)
            {
                switch (ent)
                {
                    case Curve curve:
                        Point3d result = curve.GetClosestPointTo(location, false);
                        distTuples.Add((ent, location.DistanceHorizontalTo(result)));
                        break;
                    case BlockReference br:
                        distTuples.Add((ent, br.Position.DistanceHorizontalTo(location)));
                        break;
                    default:
                        break;
                }
            }
            return distTuples;
        }
        public static T GetFirstEntityOfType<T>(
            Alignment al, HashSet<Entity> ents, Enums.TypeOfIteration forwardOrBackward) where T : DBObject
        {
            double length = al.Length;
            double step = 0.05;
            int nrOfSteps = (int)(length / step);

            for (int i = 0; i < length + 1; i++)
            {
                double station = 0;
                if (forwardOrBackward == Enums.TypeOfIteration.Forward)
                    station = i * step;
                else station = length - i * step;
                Point3d location = al.GetPointAtDist(station);
                var tuples = CreateDistTuples(location, ents);
                var min = tuples.OrderBy(x => x.dist);
                if (min.First().ent is T) return min.First().ent as T;
            }
            return null;
        }
        public static bool CheckOrCreateLayer(this Database db, string layerName)
        {
            Transaction txLag = db.TransactionManager.TopTransaction;
            LayerTable lt = txLag.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;
            if (!lt.Has(layerName))
            {
                LayerTableRecord ltr = new LayerTableRecord();
                ltr.Name = layerName;

                //Make layertable writable
                lt.CheckOrOpenForWrite();

                //Add the new layer to layer table
                oid ltId = lt.Add(ltr);
                txLag.AddNewlyCreatedDBObject(ltr, true);
                return true;
            }
            else return true;
        }
    }
    public static class OdTables
    {
        public static class Gas
        {
            public static string GetTableName() => "GasDimOgMat";
            public static string[] GetColumnNames() => new string[3] { "Dimension", "Material", "Bemærk" };
            public static string[] GetColumnDescriptions() =>
                new string[3] { "Pipe diameter", "Pipe material", "Bemærkning til ledning" };
            public static DataType[] GetDataTypes() =>
                new DataType[3] { DataType.Integer, DataType.Character, DataType.Character };
            public static string GetTableDescription() => "Gas data";
        }
        public static class Labels
        {
            public static string GetTableName() => "DRILabelData";
            public static string[] GetColumnNames() => new string[1] { "EntityHandle" };
            public static string[] GetColumnDescriptions() =>
                new string[1] { "Handle of the labeled entity" };
            public static DataType[] GetDataTypes() => new DataType[1] { DataType.Character };
            public static string GetTableDescription() => "Labels data";
        }
    }
    public static class DataQa
    {
        public static class Gas
        {
            public static HashSet<string> ForbiddenValues = new HashSet<string>()
            {
                "SÆNKET",
                "ANV. SOM TRÆKRØR",
                "ANVENDT SOM TRÆKRØR",
                "FRITST.M/R SKAB",
                "G10",
                "M/R SKAB",
                "T=0.2",
                "T=0.3",
                "TYPE G65",
                "SÆNKET 40 CM",
                "FRIT R-SKAB",
                "M/R SKAB TYPE G25",
                "REG.1843B",
                "T=0.0",
                "T=0.1",
                "TYPE G 16/25"
            };

            public static Dictionary<string, string> ReplaceLabelParts = new Dictionary<string, string>()
            {
                { "B-RØR 63 PM", "63 PM" },
                { "40 PC 026", "40 PC" },
                { "40 PM 026", "40 PM" },
                { "40 PM 20 MBAR", "40 PM" },
                { "63 PM 026", "63 PM" },
                { "63 PM 50 MB", "63 PM" },
                { "90 PM 50 MBAR", "63 PM" },
                { "75 ST/63 PM 026", "75 ST" }
            };
        }
        public static class Gis
        {
            public static bool ContainsForbiddenValues(string input)
            {
                foreach (string forbiddenValue in ForbiddenValues)
                {
                    if (input.Contains(forbiddenValue)) return true;
                }

                return false;
            }

            public static HashSet<string> ForbiddenValues = new HashSet<string>()
            {
                "GGF_ledninger",
                "REV",
            };
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
        public enum TypeOfEntity
        {
            None,
            Curve,
            BlockReference
        }
        public enum TypeOfIteration
        {
            None,
            Forward,
            Backward
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
                    Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage(
                        $"\n{br.Name}");
                    br.UpgradeOpen();
                    br.Erase();
                }

                tr.Commit();
            }

            return ids;
        }
        public static LayerTableRecord GetLayerByName(this LayerTable lt, string layerName)
        {
            foreach (oid id in lt)
            {
                LayerTableRecord ltr = id.GetObject(OpenMode.ForRead) as LayerTableRecord;
                if (ltr.Name == layerName) return ltr;
            }
            return null;
        }
        public static string ToEnglishName(this char c)
        {
            int i = (int)c;
            if (i < CharInfo.lookup.Length) return CharInfo.lookup[i];
            return "Unknown";
        }
        public static BlockTableRecord GetModelspaceForWrite(this Database db) =>
            db.BlockTableId.Go<BlockTable>(db.TransactionManager.TopTransaction)[BlockTableRecord.ModelSpace]
            .Go<BlockTableRecord>(db.TransactionManager.TopTransaction, OpenMode.ForWrite);
        public static string RealName(this BlockReference br)
        {
            Transaction tx = br.Database.TransactionManager.TopTransaction;
            string effectiveName = br.IsDynamicBlock
                ? ((BlockTableRecord)tx.GetObject(br.DynamicBlockTableRecord, OpenMode.ForRead)).Name
                : br.Name;
            return effectiveName;
        }
        public static bool XrecFilter(this Autodesk.AutoCAD.DatabaseServices.DBObject obj,
            string xRecordName, string[] filterValues)
        {
            Transaction tx = obj.Database.TransactionManager.TopTransaction;
            oid extId = obj.ExtensionDictionary;
            if (extId == oid.Null) return false;
            DBDictionary dbExt = extId.Go<DBDictionary>(tx);
            oid xrecId = oid.Null;
            try { xrecId = dbExt.GetAt(xRecordName); }
            catch (System.Exception) { return false; }
            if (xrecId == oid.Null) return false;
            Xrecord xrec = xrecId.Go<Xrecord>(tx);
            TypedValue[] data = xrec.Data.AsArray();
            bool[] resArray = new bool[0];
            for (int i = 0; i < filterValues.Length; i++)
            {
                if (data.Length <= i) break;
                if (data[i].Value.ToString() == filterValues[i]) resArray = resArray.Append(true).ToArray();
                else resArray = resArray.Append(false).ToArray();
            }
            if (resArray.Length == 0) return false;
            return resArray.All(x => x);
        }
        public static string XrecReadStringAtIndex(this Autodesk.AutoCAD.DatabaseServices.DBObject obj,
            string xRecordName, int indexToRead)
        {
            Transaction tx = obj.Database.TransactionManager.TopTransaction;
            oid extId = obj.ExtensionDictionary;
            if (extId == oid.Null) return "";
            DBDictionary dbExt = extId.Go<DBDictionary>(tx);
            oid xrecId = dbExt.GetAt(xRecordName);
            if (xrecId == oid.Null) return "";
            Xrecord xrec = xrecId.Go<Xrecord>(tx);
            TypedValue[] data = xrec.Data.AsArray();
            if (data.Length <= indexToRead) return "";
            return data[indexToRead].Value.ToString();
        }
        public static void SetAttributeStringValue(this BlockReference br, string attributeName, string value)
        {
            Database db = br.Database;
            Transaction tx = db.TransactionManager.TopTransaction;
            foreach (oid Oid in br.AttributeCollection)
            {
                AttributeReference ar = Oid.Go<AttributeReference>(tx);
                if (ar.Tag == attributeName)
                {
                    ar.CheckOrOpenForWrite();
                    ar.TextString = value;
                }
            }
        }
        /// <summary>
        /// Remember to check for existence of BlockTableRecord!
        /// </summary>
        public static BlockReference CreateBlockWithAttributes(this Database db, string blockName, Point3d position)
        {
            Transaction tx = db.TransactionManager.TopTransaction;
            BlockTableRecord modelSpace = db.GetModelspaceForWrite();
            BlockTable bt = tx.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
            oid btrId = bt[blockName];
            BlockTableRecord btr = btrId.Go<BlockTableRecord>(tx);

            var br = new BlockReference(position, btrId);

            modelSpace.CheckOrOpenForWrite();
            modelSpace.AppendEntity(br);
            tx.AddNewlyCreatedDBObject(br, true);

            foreach (oid arOid in btr)
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
                            atRef.TextString = at.TextString;
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
    }

    public static class ExtensionMethods
    {
        public static T Go<T>(this oid Oid, Transaction tx,
            Autodesk.AutoCAD.DatabaseServices.OpenMode openMode =
            Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead) where T : Autodesk.AutoCAD.DatabaseServices.DBObject
        {
            return (T)tx.GetObject(Oid, openMode, false);
        }
        public static oid AddEntityToDbModelSpace<T>(this T entity, Database db) where T : Autodesk.AutoCAD.DatabaseServices.Entity
        {
            Transaction tx = db.TransactionManager.TopTransaction;
            BlockTableRecord modelSpace = db.GetModelspaceForWrite();
            oid id = modelSpace.AppendEntity(entity);
            tx.AddNewlyCreatedDBObject(entity, true);
            return id;
        }
        public static string Layer(this oid Oid)
        {
            Transaction tx = Application.DocumentManager.MdiActiveDocument.Database.TransactionManager.TopTransaction;
            if (!Oid.ObjectClass.IsDerivedFrom(RXClass.GetClass(typeof(Entity)))) return "";
            return Oid.Go<Entity>(tx).Layer;
        }
        public static bool IsDerivedFrom<T>(this oid Oid)
        {
            return Oid.ObjectClass.IsDerivedFrom(RXObject.GetClass(typeof(T)));
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
        public static double ToDegrees(this double radians) => (180 / Math.PI) * radians;
        public static double ToRadians(this double degrees) => (Math.PI / 180) * degrees;
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

    public static class CharInfo
    {
        //From here: https://stackoverflow.com/a/8931832/6073998
        public static string[] lookup = new string[128]
        {
            "Null character",
            "Start of Heading",
            "Start of Text",
            "End-of-text character",
            "End-of-transmission character",
            "Enquiry character",
            "Acknowledge character",
            "Bell character",
            "Backspace",
            "Horizontal tab",
            "Line feed",
            "Vertical tab",
            "Form feed",
            "Carriage return",
            "Shift Out",
            "Shift In",
            "Data Link Escape",
            "Device Control 1",
            "Device Control 2",
            "Device Control 3",
            "Device Control 4",
            "Negative-acknowledge character",
            "Synchronous Idle",
            "End of Transmission Block",
            "Cancel character",
            "End of Medium",
            "Substitute character",
            "Escape character",
            "File Separator",
            "Group Separator",
            "Record Separator",
            "Unit Separator",
            "Space",
            "Exclamation mark",
            "Quotation mark",
            "Number sign",
            "Dollar sign",
            "Percent sign",
            "Ampersand",
            "Apostrophe",
            "Left parenthesis",
            "Right parenthesis",
            "Asterisk",
            "Plus sign",
            "Comma",
            "Hyphen-minus",
            "Full stop",
            "Slash",
            "Digit Zero",
            "Digit One",
            "Digit Two",
            "Digit Three",
            "Digit Four",
            "Digit Five",
            "Digit Six",
            "Digit Seven",
            "Digit Eight",
            "Digit Nine",
            "Colon",
            "Semicolon",
            "Less-than sign",
            "Equal sign",
            "Greater-than sign",
            "Question mark",
            "At sign",
            "Latin Capital letter A",
            "Latin Capital letter B",
            "Latin Capital letter C",
            "Latin Capital letter D",
            "Latin Capital letter E",
            "Latin Capital letter F",
            "Latin Capital letter G",
            "Latin Capital letter H",
            "Latin Capital letter I",
            "Latin Capital letter J",
            "Latin Capital letter K",
            "Latin Capital letter L",
            "Latin Capital letter M",
            "Latin Capital letter N",
            "Latin Capital letter O",
            "Latin Capital letter P",
            "Latin Capital letter Q",
            "Latin Capital letter R",
            "Latin Capital letter S",
            "Latin Capital letter T",
            "Latin Capital letter U",
            "Latin Capital letter V",
            "Latin Capital letter W",
            "Latin Capital letter X",
            "Latin Capital letter Y",
            "Latin Capital letter Z",
            "Left Square Bracket",
            "Backslash",
            "Right Square Bracket",
            "Circumflex accent",
            "Low line",
            "Grave accent",
            "Latin Small Letter A",
            "Latin Small Letter B",
            "Latin Small Letter C",
            "Latin Small Letter D",
            "Latin Small Letter E",
            "Latin Small Letter F",
            "Latin Small Letter G",
            "Latin Small Letter H",
            "Latin Small Letter I",
            "Latin Small Letter J",
            "Latin Small Letter K",
            "Latin Small Letter L",
            "Latin Small Letter M",
            "Latin Small Letter N",
            "Latin Small Letter O",
            "Latin Small Letter P",
            "Latin Small Letter Q",
            "Latin Small Letter R",
            "Latin Small Letter S",
            "Latin Small Letter T",
            "Latin Small Letter U",
            "Latin Small Letter V",
            "Latin Small Letter W",
            "Latin Small Letter X",
            "Latin Small Letter Y",
            "Latin Small Letter Z",
            "Left Curly Bracket",
            "Vertical bar",
            "Right Curly Bracket",
            "Tilde",
            "Delete"
            #region OriginalDefinition
            //lookup[0x00] = "Null character",
            //          lookup[0x01] = "Start of Heading",
            //          lookup[0x02] = "Start of Text",
            //          lookup[0x03] = "End-of-text character",
            //          lookup[0x04] = "End-of-transmission character",
            //          lookup[0x05] = "Enquiry character",
            //          lookup[0x06] = "Acknowledge character",
            //          lookup[0x07] = "Bell character",
            //          lookup[0x08] = "Backspace",
            //          lookup[0x09] = "Horizontal tab",
            //          lookup[0x0A] = "Line feed",
            //          lookup[0x0B] = "Vertical tab",
            //          lookup[0x0C] = "Form feed",
            //          lookup[0x0D] = "Carriage return",
            //          lookup[0x0E] = "Shift Out",
            //          lookup[0x0F] = "Shift In",
            //          lookup[0x10] = "Data Link Escape",
            //          lookup[0x11] = "Device Control 1",
            //          lookup[0x12] = "Device Control 2",
            //          lookup[0x13] = "Device Control 3",
            //          lookup[0x14] = "Device Control 4",
            //          lookup[0x15] = "Negative-acknowledge character",
            //          lookup[0x16] = "Synchronous Idle",
            //          lookup[0x17] = "End of Transmission Block",
            //          lookup[0x18] = "Cancel character",
            //          lookup[0x19] = "End of Medium",
            //          lookup[0x1A] = "Substitute character",
            //          lookup[0x1B] = "Escape character",
            //          lookup[0x1C] = "File Separator",
            //          lookup[0x1D] = "Group Separator",
            //          lookup[0x1E] = "Record Separator",
            //          lookup[0x1F] = "Unit Separator",
            //          lookup[0x20] = "Space",
            //          lookup[0x21] = "Exclamation mark",
            //          lookup[0x22] = "Quotation mark",
            //          lookup[0x23] = "Number sign",
            //          lookup[0x24] = "Dollar sign",
            //          lookup[0x25] = "Percent sign",
            //          lookup[0x26] = "Ampersand",
            //          lookup[0x27] = "Apostrophe",
            //          lookup[0x28] = "Left parenthesis",
            //          lookup[0x29] = "Right parenthesis",
            //          lookup[0x2A] = "Asterisk",
            //          lookup[0x2B] = "Plus sign",
            //          lookup[0x2C] = "Comma",
            //          lookup[0x2D] = "Hyphen-minus",
            //          lookup[0x2E] = "Full stop",
            //          lookup[0x2F] = "Slash",
            //          lookup[0x30] = "Digit Zero",
            //          lookup[0x31] = "Digit One",
            //          lookup[0x32] = "Digit Two",
            //          lookup[0x33] = "Digit Three",
            //          lookup[0x34] = "Digit Four",
            //          lookup[0x35] = "Digit Five",
            //          lookup[0x36] = "Digit Six",
            //          lookup[0x37] = "Digit Seven",
            //          lookup[0x38] = "Digit Eight",
            //          lookup[0x39] = "Digit Nine",
            //          lookup[0x3A] = "Colon",
            //          lookup[0x3B] = "Semicolon",
            //          lookup[0x3C] = "Less-than sign",
            //          lookup[0x3D] = "Equal sign",
            //          lookup[0x3E] = "Greater-than sign",
            //          lookup[0x3F] = "Question mark",
            //          lookup[0x40] = "At sign",
            //          lookup[0x41] = "Latin Capital letter A",
            //          lookup[0x42] = "Latin Capital letter B",
            //          lookup[0x43] = "Latin Capital letter C",
            //          lookup[0x44] = "Latin Capital letter D",
            //          lookup[0x45] = "Latin Capital letter E",
            //          lookup[0x46] = "Latin Capital letter F",
            //          lookup[0x47] = "Latin Capital letter G",
            //          lookup[0x48] = "Latin Capital letter H",
            //          lookup[0x49] = "Latin Capital letter I",
            //          lookup[0x4A] = "Latin Capital letter J",
            //          lookup[0x4B] = "Latin Capital letter K",
            //          lookup[0x4C] = "Latin Capital letter L",
            //          lookup[0x4D] = "Latin Capital letter M",
            //          lookup[0x4E] = "Latin Capital letter N",
            //          lookup[0x4F] = "Latin Capital letter O",
            //          lookup[0x50] = "Latin Capital letter P",
            //          lookup[0x51] = "Latin Capital letter Q",
            //          lookup[0x52] = "Latin Capital letter R",
            //          lookup[0x53] = "Latin Capital letter S",
            //          lookup[0x54] = "Latin Capital letter T",
            //          lookup[0x55] = "Latin Capital letter U",
            //          lookup[0x56] = "Latin Capital letter V",
            //          lookup[0x57] = "Latin Capital letter W",
            //          lookup[0x58] = "Latin Capital letter X",
            //          lookup[0x59] = "Latin Capital letter Y",
            //          lookup[0x5A] = "Latin Capital letter Z",
            //          lookup[0x5B] = "Left Square Bracket",
            //          lookup[0x5C] = "Backslash",
            //          lookup[0x5D] = "Right Square Bracket",
            //          lookup[0x5E] = "Circumflex accent",
            //          lookup[0x5F] = "Low line",
            //          lookup[0x60] = "Grave accent",
            //          lookup[0x61] = "Latin Small Letter A",
            //          lookup[0x62] = "Latin Small Letter B",
            //          lookup[0x63] = "Latin Small Letter C",
            //          lookup[0x64] = "Latin Small Letter D",
            //          lookup[0x65] = "Latin Small Letter E",
            //          lookup[0x66] = "Latin Small Letter F",
            //          lookup[0x67] = "Latin Small Letter G",
            //          lookup[0x68] = "Latin Small Letter H",
            //          lookup[0x69] = "Latin Small Letter I",
            //          lookup[0x6A] = "Latin Small Letter J",
            //          lookup[0x6B] = "Latin Small Letter K",
            //          lookup[0x6C] = "Latin Small Letter L",
            //          lookup[0x6D] = "Latin Small Letter M",
            //          lookup[0x6E] = "Latin Small Letter N",
            //          lookup[0x6F] = "Latin Small Letter O",
            //          lookup[0x70] = "Latin Small Letter P",
            //          lookup[0x71] = "Latin Small Letter Q",
            //          lookup[0x72] = "Latin Small Letter R",
            //          lookup[0x73] = "Latin Small Letter S",
            //          lookup[0x74] = "Latin Small Letter T",
            //          lookup[0x75] = "Latin Small Letter U",
            //          lookup[0x76] = "Latin Small Letter V",
            //          lookup[0x77] = "Latin Small Letter W",
            //          lookup[0x78] = "Latin Small Letter X",
            //          lookup[0x79] = "Latin Small Letter Y",
            //          lookup[0x7A] = "Latin Small Letter Z",
            //          lookup[0x7B] = "Left Curly Bracket",
            //          lookup[0x7C] = "Vertical bar",
            //          lookup[0x7D] = "Right Curly Bracket",
            //          lookup[0x7E] = "Tilde",
            //          lookup[0x7F] = "Delete" 
            #endregion
        };
    }

    public static class PipeSchedule
    {
        public static int GetPipeDN(Entity ent)
        {
            string layer = ent.Layer;
            switch (layer)
            {
                case "FJV-TWIN-DN32":
                case "FJV-FREM-DN32":
                case "FJV-RETUR-DN32":
                    return 32;
                case "FJV-TWIN-DN40":
                case "FJV-FREM-DN40":
                case "FJV-RETUR-DN40":
                    return 40;
                case "FJV-TWIN-DN50":
                case "FJV-FREM-DN50":
                case "FJV-RETUR-DN50":
                    return 50;
                case "FJV-TWIN-DN65":
                case "FJV-FREM-DN65":
                case "FJV-RETUR-DN65":
                    return 65;
                case "FJV-TWIN-DN80":
                case "FJV-FREM-DN80":
                case "FJV-RETUR-DN80":
                    return 80;
                case "FJV-TWIN-DN100":
                case "FJV-FREM-DN100":
                case "FJV-RETUR-DN100":
                    return 100;
                case "FJV-TWIN-DN125":
                case "FJV-FREM-DN125":
                case "FJV-RETUR-DN125":
                    return 125;
                case "FJV-TWIN-DN150":
                case "FJV-FREM-DN150":
                case "FJV-RETUR-DN150":
                    return 150;
                case "FJV-TWIN-DN200":
                case "FJV-FREM-DN200":
                case "FJV-RETUR-DN200":
                    return 200;
                case "FJV-FREM-DN250":
                case "FJV-RETUR-DN250":
                    return 250;
                case "FJV-FREM-DN300":
                case "FJV-RETUR-DN300":
                    return 300;
                case "FJV-FREM-DN350":
                case "FJV-RETUR-DN350":
                    return 350;
                case "FJV-FREM-DN400":
                case "FJV-RETUR-DN400":
                    return 400;
                case "FJV-FREM-DN450":
                case "FJV-RETUR-DN450":
                    return 450;
                case "FJV-FREM-DN500":
                case "FJV-RETUR-DN500":
                    return 500;
                case "FJV-FREM-DN600":
                case "FJV-RETUR-DN600":
                    return 600;
                default:
                    Utils.prdDbg("For entity: " + ent.Handle.ToString() + " no pipe dimension could be determined!");
                    return 999;
            }
        }
        public static string GetPipeSystem(Entity ent)
        {
            string layer = ent.Layer;
            switch (layer)
            {
                case "FJV-TWIN-DN32":
                case "FJV-TWIN-DN40":
                case "FJV-TWIN-DN50":
                case "FJV-TWIN-DN65":
                case "FJV-TWIN-DN80":
                case "FJV-TWIN-DN100":
                case "FJV-TWIN-DN125":
                case "FJV-TWIN-DN150":
                case "FJV-TWIN-DN200":
                    return "Twin";
                case "FJV-FREM-DN32":
                case "FJV-RETUR-DN32":
                case "FJV-FREM-DN40":
                case "FJV-RETUR-DN40":
                case "FJV-FREM-DN50":
                case "FJV-RETUR-DN50":
                case "FJV-FREM-DN65":
                case "FJV-RETUR-DN65":
                case "FJV-FREM-DN80":
                case "FJV-RETUR-DN80":
                case "FJV-FREM-DN100":
                case "FJV-RETUR-DN100":
                case "FJV-FREM-DN125":
                case "FJV-RETUR-DN125":
                case "FJV-FREM-DN150":
                case "FJV-RETUR-DN150":
                case "FJV-FREM-DN200":
                case "FJV-RETUR-DN200":
                case "FJV-FREM-DN250":
                case "FJV-RETUR-DN250":
                case "FJV-FREM-DN300":
                case "FJV-RETUR-DN300":
                case "FJV-FREM-DN350":
                case "FJV-RETUR-DN350":
                case "FJV-FREM-DN400":
                case "FJV-RETUR-DN400":
                case "FJV-FREM-DN450":
                case "FJV-RETUR-DN450":
                case "FJV-FREM-DN500":
                case "FJV-RETUR-DN500":
                case "FJV-FREM-DN600":
                case "FJV-RETUR-DN600":
                    return "Enkelt";
                default:
                    Utils.prdDbg("For entity: " + ent.Handle.ToString() + " no system could be determined!");
                    return null;
            }
        }

        public static double GetPipeOd(Entity ent)
        {
            Dictionary<int, double> Ods = new Dictionary<int, double>()
            {
                { 10, 17.2 },
                { 15, 21.3 },
                { 20, 26.9 },
                { 25, 33.7 },
                { 32, 42.4 },
                { 40, 48.3 },
                { 50, 60.3 },
                { 65, 76.1 },
                { 80, 88.9 },
                { 100, 114.3 },
                { 125, 139.7 },
                { 150, 168.3 },
                { 200, 219.1 },
                { 250, 273.0 },
                { 300, 323.9 },
                { 350, 355.6 },
                { 400, 406.4 },
                { 450, 457.0 },
                { 500, 508.0 },
                { 600, 610.0 },
            };

            int dn = GetPipeDN(ent);
            if (Ods.ContainsKey(dn)) return Ods[dn];
            return 0;
        }

        /// <summary>
        /// WARNING! Currently Series 3 only.
        /// </summary>
        public static double GetTwinPipeKOd(Entity ent)
        {
            Dictionary<int, double> kOds = new Dictionary<int, double>
            {
                {  20, 160.0 },
                {  25, 180.0 },
                {  32, 200.0 },
                {  40, 200.0 },
                {  50, 250.0 },
                {  65, 280.0 },
                {  80, 315.0 },
                { 100, 400.0 },
                { 125, 500.0 },
                { 150, 560.0 },
                { 200, 710.0 }
            };

            int dn = GetPipeDN(ent);
            if (kOds.ContainsKey(dn)) return kOds[dn];
            return 0;
        }

        /// <summary>
        /// WARNING! Currently S3 only.
        /// </summary>
        public static double GetBondedPipeKOd(Entity ent)
        {
            Dictionary<int, double> kOds = new Dictionary<int, double>
            {
                { 20, 125.0 },
                { 25, 125.0 },
                { 32, 140.0 },
                { 40, 140.0 },
                { 50, 160.0 },
                { 65, 180.0 },
                { 80, 200.0 },
                { 100, 250.0 },
                { 125, 280.0 },
                { 150, 315.0 },
                { 200, 400.0 },
                { 250, 500.0 },
                { 300, 560.0 },
                { 350, 630.0 },
                { 400, 710.0 },
                { 450, 800.0 },
                { 500, 900.0 },
                { 600, 1000.0 }
            };

            int dn = GetPipeDN(ent);
            if (kOds.ContainsKey(dn)) return kOds[dn];
            return 0;
        }
        public static double GetPipeKOd(Entity ent) =>
            GetPipeSystem(ent) == "Twin" ? GetTwinPipeKOd(ent) : GetBondedPipeKOd(ent);
        public static double GetPipeStdLength(Entity ent) => GetPipeDN(ent) <= 80 ? 12 : 16;
        public static double GetPipeMinElasticRadius(Entity ent)
        {
            Dictionary<int, double> radii = new Dictionary<int, double>
            {
                { 20, 13.0 },
                { 25, 17.0 },
                { 32, 21.0 },
                { 40, 24.0 },
                { 50, 30.0 },
                { 65, 38.0 },
                { 80, 44.0 },
                { 100, 57.0 },
                { 125, 70.0 },
                { 150, 84.0 },
                { 200, 110.0 },
                { 250, 137.0 },
                { 300, 162.0 },
                { 350, 178.0 },
                { 400, 203.0 },
                { 999, 0.0 }
            };
            return radii[GetPipeDN(ent)];
        }
    }
}

