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
using System.Xml.Serialization;
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
using static IntersectUtilities.PipeSchedule;

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
    public class SimpleLogger
    {
        public bool EchoToEditor { get; set; } = true;
        public string LogFileName { get; set; } = "C:\\Temp\\ShapeExportLog.txt";
        public void ClearLog()
        {
            File.WriteAllText(LogFileName, string.Empty);
        }
        public void log(string msg)
        {
            File.AppendAllLines(LogFileName, new string[] { $"{DateTime.Now}: {msg}" });
            if (EchoToEditor) prdDbg(msg);
        }
    }
    public static class Utils
    {
        public static void prdDbg(string msg)
        {
            DocumentCollection docCol = Application.DocumentManager;
            Editor editor = docCol.MdiActiveDocument.Editor;
            editor.WriteMessage("\n" + msg);
        }

        public static Dictionary<string, Color> AutocadStdColors = new Dictionary<string, Color>()
        {
            {"byblock", Color.FromColorIndex(ColorMethod.ByAci, 0) },
            {"red", Color.FromColorIndex(ColorMethod.ByAci, 1) },
            {"yellow", Color.FromColorIndex(ColorMethod.ByAci, 2) },
            {"green", Color.FromColorIndex(ColorMethod.ByAci, 3) },
            {"cyan", Color.FromColorIndex(ColorMethod.ByAci, 4) },
            {"blue", Color.FromColorIndex(ColorMethod.ByAci, 5) },
            {"magenta", Color.FromColorIndex(ColorMethod.ByAci, 6) },
            {"white", Color.FromColorIndex(ColorMethod.ByAci, 7) },
            {"grey", Color.FromColorIndex(ColorMethod.ByAci, 8) },
            {"bylayer", Color.FromColorIndex(ColorMethod.ByAci, 256) },
        };

        /// <summary>
        /// Parses one of the following patterns to an Autocad Color:
        /// Index Color: ddd
        /// RGB Color: ddd*ddd*ddd
        /// Color name: [a-zA-Z]+
        /// </summary>
        /// <param name="colorString"></param>
        /// <returns>Autocad Color, null on fail.</returns>
        public static Color ParseColorString(string colorString)
        {
            if (colorString.IsNoE()) return null;
            
            Regex indexColorRegex = new Regex(@"^\d{1,3}$");
            Regex rgbRegex = new Regex(@"^(?<R>\d+)\*(?<G>\d+)\*(?<B>\d+)$");
            Regex nameRegex = new Regex(@"^[a-zA-Z]+$");

            if (indexColorRegex.IsMatch(colorString))
            {
                if (colorString == "0") return Color.FromColorIndex(ColorMethod.ByAci, 0);
                short index = -1;
                short.TryParse(colorString, out index);
                if (index == 0) return null;
                return Color.FromColorIndex(ColorMethod.ByAci, index);
            }
            if (rgbRegex.IsMatch(colorString))
            {
                Match match = rgbRegex.Match(colorString);
                byte R = Convert.ToByte(int.Parse(match.Groups["R"].Value));
                byte G = Convert.ToByte(int.Parse(match.Groups["G"].Value));
                byte B = Convert.ToByte(int.Parse(match.Groups["B"].Value));
                Color color = Color.FromRgb(R, G, B);
                return color;
            }
            if (nameRegex.IsMatch(colorString))
            {
                return AutocadStdColors[colorString];
            }
            prdDbg($"Parsing of color string {colorString} failed!");
            return null;
        }

        public static double GetRotation(Vector3d vector, Vector3d normal)
        {
            var plane = new Plane();
            var ocsXAxis = Vector3d.XAxis.TransformBy(Matrix3d.PlaneToWorld(plane));
            return ocsXAxis.GetAngleTo(vector.ProjectTo(normal, normal), normal);
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
        public static double GetHorizontalLength(this Line line) => line.StartPoint.DistanceHorizontalTo(line.EndPoint);
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
                    prdDbg("Segment type: " + next.ToString() + ". Lav om til circular!");
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

        //public static string XrecReadStringAtIndex(this Autodesk.AutoCAD.DatabaseServices.DBObject obj,
        //    string xRecordName, int indexToRead)
        //{
        //    Transaction tx = obj.Database.TransactionManager.TopTransaction;
        //    Oid extId = obj.ExtensionDictionary;
        //    if (extId == Oid.Null) return "";
        //    DBDictionary dbExt = extId.Go<DBDictionary>(tx);
        //    Oid xrecId = Oid.Null;
        //    if (!dbExt.Contains(xRecordName)) return "";
        //    xrecId = dbExt.GetAt(xRecordName);
        //    Xrecord xrec = xrecId.Go<Xrecord>(tx);
        //    TypedValue[] data = xrec.Data.AsArray();
        //    if (data.Length <= indexToRead) return "";
        //    return data[indexToRead].Value.ToString();
        //}
        #endregion
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

            BlockTableRecord btr = br.BlockTableRecord.Go<BlockTableRecord>(tx);
            foreach (Oid oid in btr)
            {
                if (oid.IsDerivedFrom<AttributeDefinition>())
                {
                    AttributeDefinition attDef = oid.Go<AttributeDefinition>(tx);
                    if (attDef.Constant && string.Equals(attDef.Tag, attributeName, StringComparison.OrdinalIgnoreCase))
                    {
                        return attDef.TextString;
                    }
                }
            }

            return "";
        }
        /// <summary>
        /// Requires active transaction!
        /// </summary>
        public static void CheckOrImportBlockRecord(this Database db, string pathToLibrary, string blockName)
        {
            Transaction tx = db.TransactionManager.TopTransaction;
            if (tx == null) throw new System.Exception("CheckOrImportBlockRecord requires active Transaction!");
            BlockTable bt = tx.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;

            if (!bt.Has(blockName))
            {
                ObjectIdCollection idsToClone = new ObjectIdCollection();

                Database blockDb = new Database(false, true);
                blockDb.ReadDwgFile(pathToLibrary,
                    FileOpenMode.OpenForReadAndAllShare, false, null);
                Transaction blockTx = blockDb.TransactionManager.StartTransaction();

                Oid sourceMsId = SymbolUtilityServices.GetBlockModelSpaceId(blockDb);
                Oid destDbMsId = SymbolUtilityServices.GetBlockModelSpaceId(db);

                BlockTable sourceBt = blockTx.GetObject(blockDb.BlockTableId, OpenMode.ForRead) as BlockTable;

                prdDbg($"Importing block {blockName}.");
                idsToClone.Add(sourceBt[blockName]);

                IdMapping mapping = new IdMapping();
                blockDb.WblockCloneObjects(idsToClone, destDbMsId, mapping, DuplicateRecordCloning.Replace, false);
                blockTx.Commit();
                blockTx.Dispose();
                blockDb.Dispose();
            }
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

        public static List<string> ListLayers(this Database db)
        {
            List<string> lstlay = new List<string>();

            LayerTableRecord layer;
            using (Transaction tr = db.TransactionManager.StartOpenCloseTransaction())
            {
                LayerTable lt = tr.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;
                foreach (ObjectId layerId in lt)
                {
                    layer = tr.GetObject(layerId, OpenMode.ForWrite) as LayerTableRecord;
                    lstlay.Add(layer.Name);
                }

            }
            return lstlay;
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
        public static Polyline DrawExtents(this Entity entity)
        {
            Extents3d extents = entity.GeometricExtents;
            if (extents == null) return null;
            Polyline pline = new Polyline(4);
            List<Point2d> point2Ds = new List<Point2d>();
            point2Ds.Add(new Point2d(extents.MinPoint.X, extents.MinPoint.Y));
            point2Ds.Add(new Point2d(extents.MinPoint.X, extents.MaxPoint.Y));
            point2Ds.Add(new Point2d(extents.MaxPoint.X, extents.MaxPoint.Y));
            point2Ds.Add(new Point2d(extents.MaxPoint.X, extents.MinPoint.Y));
            foreach (Point2d p2d in point2Ds)
            {
                pline.AddVertexAt(pline.NumberOfVertices, p2d, 0, 0, 0);
            }
            pline.Closed = true;
            using (Transaction tx = entity.Database.TransactionManager.StartTransaction())
            {
                pline.AddEntityToDbModelSpace(entity.Database);
                tx.Commit();
            }
            return pline;
        }
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
        public static Point3d To3D(this Point2d p2d, double Z = 0.0) => new Point3d(p2d.X, p2d.Y, Z);
        public static Point2d To2D(this Point3d p3d) => new Point2d(p3d.X, p3d.Y);
        public static bool IsOnCurve(this Point3d pt, Curve cv, double tol)
        {
            try
            {
                // Return true if operation succeeds
                Point3d p = cv.GetClosestPointTo(pt, false);
                //return (p - pt).Length <= Tolerance.Global.EqualPoint;
                return (p - pt).Length <= tol;
            }
            catch { }
            // Otherwise we return false
            return false;
        }
        public static bool IsConnectedTo(this Polyline pl1, Polyline pl2, double tol = 0.025)
        {
            if (pl1.StartPoint.IsOnCurve(pl2, tol)) return true;
            if (pl1.EndPoint.IsOnCurve(pl2, tol)) return true;
            return false;
        }
        public static bool EndIsConnectedTo(this Polyline pl1, Polyline pl2, double tol = 0.025)
        {
            if (pl1.EndPoint.HorizontalEqualz(pl2.StartPoint, tol)) return true;
            if (pl1.EndPoint.HorizontalEqualz(pl2.EndPoint, tol)) return true;
            return false;
        }
        public static bool BothEndsAreConnectedTo(this Polyline pl1, Polyline pl2, double tol = 0.025)
        {
            if (pl1.EndPoint.HorizontalEqualz(pl2.StartPoint, tol)) return true;
            if (pl1.EndPoint.HorizontalEqualz(pl2.EndPoint, tol)) return true;
            if (pl1.StartPoint.HorizontalEqualz(pl2.StartPoint, tol)) return true;
            if (pl1.StartPoint.HorizontalEqualz(pl2.EndPoint, tol)) return true;
            return false;
        }
        public static HashSet<Point3d> GetAllEndPoints(this BlockReference br)
        {
            HashSet<Point3d> result = new HashSet<Point3d>();

            Transaction tx = GetTopTx(br);

            BlockTableRecord btr = br.BlockTableRecord.Go<BlockTableRecord>(tx);

            foreach (Oid oid in btr)
            {
                if (!oid.IsDerivedFrom<BlockReference>()) continue;
                BlockReference nestedBr = oid.Go<BlockReference>(tx);
                if (!nestedBr.Name.Contains("MuffeIntern")) continue;

                Point3d wPt = nestedBr.Position;
                wPt = wPt.TransformBy(br.BlockTransform);

                result.Add(wPt);
            }

            return result;
        }
        public static T[] ConcatAr<T>(this T[] x, T[] y)
        {
            if (x == null) throw new ArgumentNullException("x");
            if (y == null) throw new ArgumentNullException("y");
            int oldLen = x.Length;
            Array.Resize<T>(ref x, x.Length + y.Length);
            Array.Copy(y, 0, x, oldLen, y.Length);
            return x;
        }
        public static string GetXmlEnumAttributeValueFromEnum<TEnum>(this TEnum value) where TEnum : struct, IConvertible
        {
            var enumType = typeof(TEnum);
            if (!enumType.IsEnum) return string.Empty;//or string.Empty, or throw exception

            var member = enumType.GetMember(value.ToString()).FirstOrDefault();
            if (member == null) return string.Empty;//or string.Empty, or throw exception

            var attribute = member.GetCustomAttributes(false).OfType<XmlEnumAttribute>().FirstOrDefault();
            if (attribute == null) return value.ToString();//or string.Empty, or throw exception
            return attribute.Name;
        }
        public static IOrderedEnumerable<T> OrderByAlphaNumeric<T>(this IEnumerable<T> source, Func<T, string> selector)
        {
            int max = source
                .SelectMany(i => Regex.Matches(selector(i), @"\d+").Cast<Match>().Select(m => (int?)m.Value.Length))
                .Max() ?? 0;

            return source.OrderBy(i => Regex.Replace(selector(i), @"\d+", m => m.Value.PadLeft(max, '0')));
        }
        public static double StationAtPoint(this Alignment al, Point3d p)
        {
            double station = 0.0;
            double offset = 0.0;
            Point3d cP = default;

            try
            {
                cP = al.GetClosestPointTo(p, false);
                al.StationOffset(cP.X, cP.Y, ref station, ref offset);
            }
            catch (System.Exception ex)
            {
                prdDbg($"Alignment {al.Name} threw an exception when sampling station at point:\n" +
                    $"Entity position: {p}\n" +
                    $"Sampled position: {cP}");
                prdDbg(ex.ToString());
                throw;
            }

            return station;
        }
        public static double StationAtPoint(this Alignment al, BlockReference br)
            => StationAtPoint(al, br.Position);
        public static void UpdateElevationZ(this PolylineVertex3d vert, double newElevation)
        {
            if (!vert.Position.Z.Equalz(newElevation, Tolerance.Global.EqualPoint))
                vert.Position = new Point3d(
                    vert.Position.X, vert.Position.Y, newElevation);
        }
        public static bool IsAtZeroElevation(this PolylineVertex3d vert) => vert.Position.Z < 0.0001 && vert.Position.Z > -0.0001;
        public static bool IsAtZeroElevation(this Polyline3d p3d)
        {
            PolylineVertex3d[] vertices = p3d.GetVertices(p3d.GetTopTx());
            bool atZero = true;
            foreach (var vert in vertices)
            {
                if (vert.IsAtZeroElevation()) continue;
                else atZero = false;
            }
            return atZero;
        }
        public static bool IsZero(this double d, double tol) => d > -tol && d < tol;
        public static bool IsZero(this double d) => d > -Tolerance.Global.EqualPoint && d < Tolerance.Global.EqualPoint;
    }
    public static class ExtensionMethods
    {
        public static T Go<T>(this Oid oid, Transaction tx,
            Autodesk.AutoCAD.DatabaseServices.OpenMode openMode =
            Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead) where T : Autodesk.AutoCAD.DatabaseServices.DBObject
        {
            return (T)tx.GetObject(oid, openMode, false);
        }
        public static T Go<T>(this Handle handle, Database database) where T : Autodesk.AutoCAD.DatabaseServices.DBObject
        {
            Oid id = database.GetObjectId(false, handle, 0);
            if (database.TransactionManager.TopTransaction == null)
                throw new System.Exception("Handle.Go<DBObject> -> no top transaction found! Call inside transaction.");
            return id.Go<T>(database.TransactionManager.TopTransaction);
        }
        public static T Go<T>(this Database db, string handle) where T : Autodesk.AutoCAD.DatabaseServices.DBObject
        {
            Handle h = new Handle(Convert.ToInt64(handle, 16));
            return h.Go<T>(db);
        }
        public static Oid AddEntityToDbModelSpace<T>(this T entity, Database db) where T : Autodesk.AutoCAD.DatabaseServices.Entity
        {
            if (db.TransactionManager.TopTransaction == null)
            {
                using (Transaction tx = db.TransactionManager.StartTransaction())
                {
                    try
                    {

                        BlockTableRecord modelSpace = db.GetModelspaceForWrite();
                        Oid id = modelSpace.AppendEntity(entity);
                        tx.AddNewlyCreatedDBObject(entity, true);
                        tx.Commit();
                        return id;
                    }
                    catch (System.Exception)
                    {
                        prdDbg("Adding element to database failed!");
                        tx.Abort();
                        return Oid.Null;
                    }
                }
            }
            else
            {
                Transaction tx = db.TransactionManager.TopTransaction;

                BlockTableRecord modelSpace = db.GetModelspaceForWrite();
                Oid id = modelSpace.AppendEntity(entity);
                tx.AddNewlyCreatedDBObject(entity, true);
                return id;
            }
        }
        public static bool CheckOrCreateLayer(this Database db, string layerName, short colorIdx = -1)
        {
            Transaction txLag = db.TransactionManager.TopTransaction;
            LayerTable lt = txLag.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;
            if (!lt.Has(layerName))
            {
                LayerTableRecord ltr = new LayerTableRecord();
                ltr.Name = layerName;
                if (colorIdx != -1)
                {
                    ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, colorIdx);
                }

                //Make layertable writable
                lt.CheckOrOpenForWrite();

                //Add the new layer to layer table
                Oid ltId = lt.Add(ltr);
                txLag.AddNewlyCreatedDBObject(ltr, true);
                return true;
            }
            else
            {
                if (colorIdx == -1) return true;
                LayerTableRecord ltr = lt[layerName].Go<LayerTableRecord>(txLag, OpenMode.ForWrite);
                if (ltr.Color.ColorIndex != colorIdx)
                    ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, colorIdx);
                return true;
            }
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
        public static HashSet<Entity> GetFjvEntities(this Database db, Transaction tr, System.Data.DataTable fjvKomponenter,
            bool discardWelds = true, bool discardFrozen = false)
        {
            HashSet<Entity> entities = new HashSet<Entity>();

            var rawPlines = db.ListOfType<Polyline>(tr, discardFrozen);
            var plineQuery = rawPlines.Where(pline => GetPipeSystem(pline) != PipeSystemEnum.Ukendt);

            var rawBrefs = db.ListOfType<BlockReference>(tr, discardFrozen);
            var brQuery = rawBrefs.Where(x => UtilsDataTables.ReadStringParameterFromDataTable(
                            x.RealName(), fjvKomponenter, "Navn", 0) != default);

            HashSet<string> forbiddenBlocks = new HashSet<string>()
            {
                "SVEJSEPUNKT", "SVEJSEPUNKT-NOTXT", "STIKAFGRENING", "STIKTEE"
            };

            if (discardWelds) brQuery = brQuery.Where(x => !forbiddenBlocks.Contains(x.RealName()));

            entities.UnionWith(brQuery);
            entities.UnionWith(plineQuery);
            return entities;
        }
        public static HashSet<Polyline> GetFjvPipes(this Database db, Transaction tr, bool discardFrozen = false)
        {
            HashSet<Polyline> entities = new HashSet<Polyline>();

            var rawPlines = db.ListOfType<Polyline>(tr, discardFrozen);
            entities = rawPlines
                .Where(pline => GetPipeSystem(pline) != PipeSystemEnum.Ukendt)
                .ToHashSet();

            return entities;
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
