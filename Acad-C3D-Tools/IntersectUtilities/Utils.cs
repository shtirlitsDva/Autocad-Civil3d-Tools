using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Data;
using System.Globalization;
using System.Threading.Tasks;
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

                if (value.IsNOE() || value == null) return 0;

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
        public static bool CreateTable(Tables tables, string tableName)
        {
            ErrorCode errODCode = ErrorCode.OK;
            Autodesk.Gis.Map.ObjectData.Table table = null;

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

                    // Create a FieldDefinitions and add four FieldDefinition to it 
                    FieldDefinitions tabDefs = app.ActiveProject.MapUtility.NewODFieldDefinitions();

                    FieldDefinition def1 = FieldDefinition.Create("Handle", "Handle to string", "");
                    tabDefs.AddColumn(def1, 0);

                    //FieldDefinition def2 = FieldDefinition.Create("SECOND_FIELD", "Int Type", 0);
                    //tabDefs.AddColumn(def2, 1);

                    //FieldDefinition def3 = FieldDefinition.Create("THIRD_FIELD", "Real Type", 0.0);
                    //tabDefs.AddColumn(def3, 2);

                    //FieldDefinition def4 = FieldDefinition.Create("LAST_FIELD", "Point Type", new Point3d(0, 0, 0));
                    //tabDefs.AddColumn(def4, 3);

                    tables.Add(tableName, tabDefs, "Object handle", true);

                    return true;
                }
                catch (MapException e)
                {
                    // Deal with the exception as your will
                    errODCode = (ErrorCode)(e.ErrorCode);

                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// Adds a record to a Table named tableName, the record is generated automatically.
        /// </summary>
        public static bool AddODRecord(Tables tables, string tableName, oid id, Handle handle)
        {
            try
            {
                Autodesk.Gis.Map.ObjectData.Table table = tables[tableName];

                // Create and initialize an record 
                Record tblRcd = Record.Create();
                table.InitRecord(tblRcd);

                MapValue val = tblRcd[0]; // String type
                val.Assign(handle.ToString().Replace("(", "").Replace(")", ""));

                //val = tblRcd[1]; // integer
                //val.Assign(m_index);
                //msg += m_index.ToString() + "; ";

                //val = tblRcd[2]; // real
                //val.Assign(3.14159);
                //msg += (3.14159).ToString() + "; ";

                //val = tblRcd[3]; // point
                //Point3d pt = new Point3d(10 * m_index, 20 * m_index, 30 * m_index);
                //val.Assign(pt);
                //msg += pt.ToString();

                //m_index++;

                table.AddRecord(tblRcd, id);

                //AcadEditor.WriteMessage("\n The inserted record : [" + msg + "] ");

                return true;
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
                bool success = true;

                // Get and Initialize Records
                using (Records records
                           = tables.GetObjectRecords(0, id, Autodesk.Gis.Map.Constants.OpenMode.OpenForRead, false))
                {
                    if (records.Count == 0)
                    {
                        AcadEditor.WriteMessage($"\nThere is no ObjectData record attached on the entity.");
                        return null;
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
                            if (column.Name == columnName && record.TableName == tableName)
                            {
                                return record[i];

                                //switch (val.Type)
                                //{
                                //    case Autodesk.Gis.Map.Constants.DataType.Integer:
                                //        //valInt = val.Int32Value;
                                //        break;

                                //    case Autodesk.Gis.Map.Constants.DataType.Real:
                                //        //valDouble = val.DoubleValue;
                                //        break;

                                //    case Autodesk.Gis.Map.Constants.DataType.Character:
                                //        //str = val.StrValue;
                                //        break;

                                //    case Autodesk.Gis.Map.Constants.DataType.Point:
                                //        {
                                //            //Point3d pt = val.Point;
                                //            //double x = pt.X;
                                //            //double y = pt.Y;
                                //            //double z = pt.Z;
                                //            //msg = string.Format("Point({0},{1},{2}); ", x, y, z);
                                //        }
                                //        break;

                                //    default:
                                //        AcadEditor.WriteMessage("\nWrong data type\n");
                                //        success = false;
                                //        break;
                                //}
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

                return null;
            }
        }

        public static bool DoesRecordExist(Tables tables, oid id)
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
                            if (column.Name == "Id") return true;
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

        public static Editor AcadEditor
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
    }

    public static class Extensions
    {
        public static bool IsNOE(this string s) => string.IsNullOrEmpty(s);
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

        public static List<T> ListOfType<T>(this Database database, Transaction tr) where T : Autodesk.AutoCAD.DatabaseServices.Entity
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
                    objs.Add(entity);
                }
            }
            return objs;
            //tr.Commit();
            //}
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
