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
using IntersectUtilities.UtilsCommon;

using static IntersectUtilities.UtilsCommon.Utils;
using static IntersectUtilities.UtilsCommon.UtilsDataTables;
using static IntersectUtilities.UtilsCommon.UtilsODData;

using AcRx = Autodesk.AutoCAD.Runtime;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using static IntersectUtilities.HelperMethods;
using static IntersectUtilities.DynamicBlocks.PropertyReader;
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
        public static void OutputWriter(string fullPathAndName, string sr, bool clearFile = false)
        {
            if (clearFile) System.IO.File.WriteAllBytes(fullPathAndName, new byte[0]);

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
        /// <returns>
        /// List of tuples or empty list. For example for string:
        /// {Diameter} {Material}, Regnvand
        /// It returns list:
        /// ("{Diameter}", "Diameter")
        /// ("{Material}", "Material")
        /// </returns>
        public static List<(string partInCurlyBracesToReplace, string replaceWith)> FindDescriptionParts(string input)
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
        /// <summary>
        /// Use only for single field, no multiple matches supported!
        /// </summary>
        public static (string setName, string propertyName) FindPropertySetParts(string input)
        {
            Regex regex = new Regex(@"({[0-9a-zæøåA-ZÆØÅ_:-]*})");

            if (regex.IsMatch(input))
            {
                Match match = regex.Match(input);
                string result = match.Value;

                result = result.Replace("{", "").Replace("}", "");
                string[] split = result.Split(':');
                string setName = split[0];
                string propertyName = split[1];
                return (setName, propertyName);
            }

            return (default, default);
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
                    string value = PropertySetManager
                        .ReadNonDefinedPropertySetString(ent, parts[0], parts[1]);
                    if (value.IsNotNoE())
                    {
                        string result = readStructure.Replace(list[0].ToReplace, value);
                        return result;
                    }
                }
            }
            return "";
        }
        public static string ReadDescriptionPartValueFromPS(List<PropertySet> pss, Entity ent,
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
                    string tableName = parts[0];
                    string fieldName = parts[1];
                    PropertySet ps = pss.Find(x => x.PropertySetDefinitionName == tableName);
                    if (ps == default)
                    {
                        prdDbg($"PropertySet {parts[0]} could not be found for entity handle {ent.Handle}.");
                        return "";
                    }
                    int propertyId = ps.PropertyNameToId(fieldName);
                    object value = ps.GetAt(propertyId);

                    PropertySetDefinition psDef = ps.PropertySetDefinition.Go<PropertySetDefinition>(
                        ps.Database.TransactionManager.TopTransaction);

                    PropertyDefinitionCollection defs = psDef.Definitions;
                    int defIdx = defs.IndexOf(fieldName);
                    PropertyDefinition def = defs[defIdx];
                    Autodesk.Aec.PropertyData.DataType t = def.DataType;

                    string valueString = PropertySetPropertyValueToString(value, t);
                    if (valueString.IsNotNoE())
                    {
                        string result = readStructure.Replace(list[0].ToReplace, valueString);
                        return result;
                    }
                }
            }
            return "";
        }
        public static object ReadPropertyValueFromPS(
            List<PropertySet> pss, Entity ent, string tableName, string fieldName
            )
        {
            //Assume only one result
            PropertySet ps = pss.Find(x => x.PropertySetDefinitionName == tableName);
            if (ps == default)
            {
                prdDbg($"PropertySet {tableName} could not be found for entity handle {ent.Handle}.");
                return "";
            }
            int propertyId = ps.PropertyNameToId(fieldName);
            object value = ps.GetAt(propertyId);
            if (value != null) return value;
            else return default;

            //PropertySetDefinition psDef = ps.PropertySetDefinition.Go<PropertySetDefinition>(
            //    ps.Database.TransactionManager.TopTransaction);

            //PropertyDefinitionCollection defs = psDef.Definitions;
            //int defIdx = defs.IndexOf(fieldName);
            //PropertyDefinition def = defs[defIdx];
            //Autodesk.Aec.PropertyData.DataType t = def.DataType;

            //string valueString = PropertySetPropertyValueToString(value, t);
            //if (valueString.IsNotNoE())
            //{
            //    string result = readStructure.Replace(list[0].ToReplace, valueString);
            //    return result;
            //}
            //return "";
        }
        //public static object ReadPropertyValueFromPS(Entity ent, string tableName, string fieldName)
        //{
        //    ObjectIdCollection propertySetIds = PropertyDataServices.GetPropertySets(ent);
        //    List<PropertySet> pss = new List<PropertySet>();
        //    foreach (Oid oid in propertySetIds) pss.Add(oid.Go<PropertySet>(
        //        ent.Database.TransactionManager.TopTransaction));

        //    //Assume only one result
        //    PropertySet ps = pss.Find(x => x.PropertySetDefinitionName == tableName);
        //    if (ps == default)
        //    {
        //        prdDbg($"PropertySet {tableName} could not be found for entity handle {ent.Handle}.");
        //        return "";
        //    }
        //    int propertyId = ps.PropertyNameToId(fieldName);
        //    object value = ps.GetAt(propertyId);
        //    if (value != null) return value;
        //    else return default;
        //}
        public static MapValue MapValueFromObject(object input, Autodesk.Gis.Map.Constants.DataType type)
        {
            switch (type)
            {
                case DataType.UnknownType:
                    break;
                case DataType.Integer:
                    return new MapValue((int)input);
                case DataType.Real:
                    return new MapValue((double)input);
                case DataType.Character:
                    return new MapValue((string)input);
                case DataType.Point:
                    return default;
                default:
                    break;
            }
            return default;
        }
        public static string PropertySetPropertyValueToString(object value, Autodesk.Aec.PropertyData.DataType type)
        {
            switch (type)
            {
                case Autodesk.Aec.PropertyData.DataType.Integer:
                    return ((int)value).ToString();
                case Autodesk.Aec.PropertyData.DataType.Real:
                    return ((double)value).ToString();
                case Autodesk.Aec.PropertyData.DataType.Text:
                    return ((string)value);
                case Autodesk.Aec.PropertyData.DataType.TrueFalse:
                    return ((bool)value).ToString();
                case Autodesk.Aec.PropertyData.DataType.AutoIncrement:
                    prdDbg("PropertyValueToString: DataType.AutoIncrement not implemented!");
                    return "";
                case Autodesk.Aec.PropertyData.DataType.AlphaIncrement:
                    prdDbg("PropertyValueToString: DataType.AlphaIncrement not implemented!");
                    return "";
                case Autodesk.Aec.PropertyData.DataType.List:
                    prdDbg("PropertyValueToString: DataType.List not implemented!");
                    return "";
                case Autodesk.Aec.PropertyData.DataType.Graphic:
                    prdDbg("PropertyValueToString: DataType.Graphic not implemented!");
                    return "";
                default:
                    return "";
            }
        }
        public static void PropertySetCopyFromEntToEnt(Entity source, Entity target)
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

            Oid profByLayout = Profile.CreateByLayout(
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
        public static HashSet<(Entity ent, double dist)> CreateDistTuples<T>(Point3d location, HashSet<T> ents) where T : Entity
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
                        prdDbg($"CreateDistTuples received non-supported type for object {ent.Handle}!");
                        break;
                }
            }
            return distTuples;
        }
        public static T GetFirstEntityOfType<T>(
            Alignment al, HashSet<T> ents, Enums.TypeOfIteration forwardOrBackward) where T : Entity
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
                double x = 0;
                double y = 0;
                al.PointLocation(station, 0, ref x, ref y);
                var tuples = CreateDistTuples(new Point3d(x, y, 0), ents);
                var min = tuples.OrderBy(t => t.dist);
                if (min.First().ent is T) return min.First().ent as T;
            }
            return null;
        }
        public static void DisplayDynBlockProperties(Editor ed, BlockReference br, string name)
        {
            // Only continue is we have a valid dynamic block
            if (br != null && br.IsDynamicBlock)
            {
                ed.WriteMessage("\nDynamic properties for \"{0}\"\n", name);
                // Get the dynamic block's property collection
                DynamicBlockReferencePropertyCollection pc = br.DynamicBlockReferencePropertyCollection;
                // Loop through, getting the info for each property
                foreach (DynamicBlockReferenceProperty prop in pc)
                {
                    // Start with the property name, type and description
                    ed.WriteMessage("\nProperty: \"{0}\" : {1}", prop.PropertyName, prop.UnitsType);
                    if (prop.Description != "") ed.WriteMessage("\n  Description: {0}", prop.Description);
                    // Is it read-only?
                    if (prop.ReadOnly) ed.WriteMessage(" (Read Only)");
                    // Get the allowed values, if it's constrained
                    bool first = true;
                    foreach (object value in prop.GetAllowedValues())
                    {
                        ed.WriteMessage((first ? "\n  Allowed values: [" : ", "));
                        ed.WriteMessage("\"{0}\"", value);
                        first = false;
                    }
                    if (!first) ed.WriteMessage("]");
                    // And finally the current value
                    ed.WriteMessage("\n  Current value: \"{0}\"\n", prop.Value);
                }
            }
        }
        public static void SetDynBlockProperty(BlockReference br, string propertyName, string propertyValue)
        {
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            // Only continue is we have a valid dynamic block
            if (br != null && br.IsDynamicBlock)
            {
                // Get the dynamic block's property collection
                DynamicBlockReferencePropertyCollection pc = br.DynamicBlockReferencePropertyCollection;
                // Loop through, getting the info for each property
                foreach (DynamicBlockReferenceProperty prop in pc)
                {
                    if (prop.PropertyName == propertyName)
                    {
                        object[] allowedValues = prop.GetAllowedValues();
                        for (int i = 0; i < allowedValues.Length; i++)
                        {
                            if (allowedValues[i].ToString() == propertyValue)
                            {
                                prop.Value = allowedValues[i];
                                break;
                            }
                        }
                    }
                }
            }
        }
        public static void SetDynBlockPropertyObject(BlockReference br, string propertyName, object propertyValue)
        {
            if (br != null && br.IsDynamicBlock)
            {
                // Get the dynamic block's property collection
                DynamicBlockReferencePropertyCollection pc = br.DynamicBlockReferencePropertyCollection;
                // Loop through, getting the info for each property
                foreach (DynamicBlockReferenceProperty prop in pc)
                {
                    if (prop.PropertyName == propertyName)
                    {
                        //prdDbg(prop.Value.ToString());
                        //prdDbg(prop.UnitsType.ToString());
                        prop.Value = propertyValue;
                        break;
                    }
                }
            }
        }
        /// <summary>
        /// Function returns a sorted queue of member Curves starting with largest DN.
        /// Curves are, if needed, reversed, so the first node is always first in the direction.
        /// </summary>
        public static Queue<Curve> GetSortedQueue(Database localDb, Alignment al, HashSet<Curve> curves,
            ref Enums.TypeOfIteration iterType)
        {
            #region PropertySetManager
            PropertySetManager psm = new PropertySetManager(localDb, PSetDefs.DefinedSets.DriPipelineData);
            PSetDefs.DriPipelineData driPipelineData = new PSetDefs.DriPipelineData();
            #endregion

            #region Detect curves direction
            double alLength = al.Length;
            double stepLength = 0.1;
            int nrOfSteps = (int)(alLength / stepLength);
            HashSet<(Entity ent, double dist)> distTuples = new HashSet<(Entity ent, double dist)>();
            Queue<Curve> kø = new Queue<Curve>();

            //Variables
            Entity previousEnt = default;

            //Find first curve from each end
            Curve firstEnd = GetFirstEntityOfType<Curve>(al, curves, Enums.TypeOfIteration.Forward);
            Curve secondEnd = GetFirstEntityOfType<Curve>(al, curves, Enums.TypeOfIteration.Backward);

            int firstDn = PipeSchedule.GetPipeDN(firstEnd);
            int secondDn = PipeSchedule.GetPipeDN(secondEnd);

            if (firstDn == secondDn)
            {
                prdDbg(
                    $"ADVARSEL: Alignment {al.Name} har samme størrelse i begge ender!\n" +
                    $"ADVARSEL: Polyliner i denne alignment skal vendes manuelt i retning fra forsyning til kunder.\n" +
                    $"ADVARSEL: Brug kommando \"TOGGLEFJVDIR\" fra AcadOverrules.dll til at kunne se retningen på linjerne.");

                //Detect manual iteration type
                double X = 0.0; double Y = 0.0;
                al.PointLocation(0.0, 0.0, ref X, ref Y);

                Point3d testPoint = new Point3d(X, Y, 0.0);

                double startDist = firstEnd.StartPoint.DistanceHorizontalTo(testPoint);
                double endDist = firstEnd.EndPoint.DistanceHorizontalTo(testPoint);

                iterType = startDist < endDist ? Enums.TypeOfIteration.Forward : Enums.TypeOfIteration.Backward;
            }
            else iterType = secondDn < firstDn ? Enums.TypeOfIteration.Forward : Enums.TypeOfIteration.Backward;

            for (int i = 0; i < nrOfSteps + 1; i++)
            {
                double station = iterType == Enums.TypeOfIteration.Forward ? i * stepLength : alLength - i * stepLength;
                double x = 0;
                double y = 0;
                al.PointLocation(station, 0, ref x, ref y);
                distTuples = CreateDistTuples(new Point3d(x, y, 0), curves);
                var sortedTuples = distTuples.OrderBy(t => t.dist);
                if (previousEnt?.Id == sortedTuples.First().ent.Id) continue;
                if (sortedTuples.First().ent is Curve curve)
                {
                    //Determine direction of curve
                    Point3d tempP3d = curve.GetPointAtParameter(curve.StartParam);
                    double curveStartStation = 0;
                    double offset = 0;

                    try
                    {
                        al.StationOffset(tempP3d.X, tempP3d.Y, ref curveStartStation, ref offset);
                    }
                    catch (System.Exception)
                    {
                        prdDbg(tempP3d.ToString());
                        throw;
                    }

                    tempP3d = curve.GetPointAtParameter(curve.EndParam);
                    double curveEndStation = 0;

                    try
                    {
                        al.StationOffset(tempP3d.X, tempP3d.Y, ref curveEndStation, ref offset);
                    }
                    catch (System.Exception)
                    {
                        prdDbg(tempP3d.ToString());
                        throw;
                    }

                    if ((iterType == Enums.TypeOfIteration.Backward && curveStartStation < curveEndStation) ||
                        (iterType == Enums.TypeOfIteration.Forward && curveStartStation > curveEndStation))
                    {//Catches if curve is reversed
                        curve.CheckOrOpenForWrite();
                        curve.ReverseCurve();
                    }

                    #region Detect buerør, split and erase existing and add to kø
                    //Detect buerør and split curves there
                    if (curve is Polyline pline)
                    {
                        List<double> splitPts = new List<double>();

                        //-1 because we are not interested in the last vertice
                        //as it is guaranteed that we don't have a bulge on the last vertice
                        for (int j = 0; j < pline.NumberOfVertices - 1; j++)
                        {
                            //Guard against already cut out curves
                            if (j == 0 && pline.NumberOfVertices == 2) { break; }
                            double b = pline.GetBulgeAt(j);
                            Point2d fP = pline.GetPoint2dAt(j);
                            Point2d sP = pline.GetPoint2dAt(j + 1);
                            double u = fP.GetDistanceTo(sP);
                            double radius = u * ((1 + b.Pow(2)) / (4 * Math.Abs(b)));
                            double minRadius = PipeSchedule.GetPipeMinElasticRadius(pline);

                            //If radius is less than minRadius a buerør is detected
                            //Split the pline in segments delimiting buerør and append
                            if (radius < minRadius)
                            {
                                prdDbg($"Buerør detected {fP.ToString()} and {sP.ToString()}.");
                                splitPts.Add(pline.GetParameterAtPoint(new Point3d(fP.X, fP.Y, 0)));
                                splitPts.Add(pline.GetParameterAtPoint(new Point3d(sP.X, sP.Y, 0)));
                            }
                        }

                        if (splitPts.Count != 0)
                        {
                            DBObjectCollection objs = pline.GetSplitCurves(
                                new DoubleCollection(splitPts.ToArray()));
                            foreach (DBObject obj in objs)
                            {
                                if (obj is Polyline newPline)
                                {
                                    newPline.AddEntityToDbModelSpace(localDb);

                                    PropertySetManager.CopyAllProperties(curve, newPline);
                                    //XrecCopyTo(curve, newPline, "Alignment");

                                    ////Check direction of curve
                                    //curveStartStation = al.GetDistAtPoint(
                                    //    al.GetClosestPointTo(newPline.GetPointAtParameter(newPline.StartParam), false));
                                    //curveEndStation = al.GetDistAtPoint(
                                    //    al.GetClosestPointTo(
                                    //        newPline.GetPointAtParameter(
                                    //            newPline.GetParameterAtDistance(newPline.EndParam)), false));
                                    //if ((iterType == TypeOfIteration.Backward && curveStartStation < curveEndStation) ||
                                    //    (iterType == TypeOfIteration.Forward && curveStartStation > curveEndStation))
                                    //{//Catches if curve is reversed
                                    //    prdDbg("This is useful actually!!!");
                                    //    newPline.ReverseCurve();
                                    //}
                                    kø.Enqueue(newPline);
                                }
                            }

                            curve.CheckOrOpenForWrite();
                            curve.Erase(true);
                        }
                        else kø.Enqueue(curve);
                    }
                    #endregion
                }
                //Hand over current entity to cache
                previousEnt = sortedTuples.First().ent;
            }
            #endregion
            return kø;
        }
        /// <summary>
        /// Works only on blocks with TWO MuffeIntern such as transitions.
        /// For less than TWO it will throw an exception.
        /// For more than TWO it will return distance between two first MuffeIntern.
        /// </summary>
        public static double GetTransitionLength(Transaction tx, BlockReference nearestBlock)
        {
            if (nearestBlock == null) return 0;
            if (nearestBlock.RealName() != "RED KDLR" &&
                nearestBlock.RealName() != "RED KDLR x2")
            {
                prdDbg($"GetTransitionLength recieved non-transition block {nearestBlock.RealName()}, {nearestBlock.Handle}!");
            }

            BlockTableRecord btr = nearestBlock.BlockTableRecord.Go<BlockTableRecord>(tx);
            int count = 0;
            Point3d fp = default;
            Point3d sp = default;
            foreach (Oid oid in btr)
            {
                if (!oid.IsDerivedFrom<BlockReference>()) continue;
                BlockReference nestedBr = oid.Go<BlockReference>(tx);
                if (!nestedBr.Name.Contains("MuffeIntern")) continue;
                count++;
                switch (count)
                {
                    case 1:
                        fp = nestedBr.Position;
                        break;
                    case 2:
                        sp = nestedBr.Position;
                        break;
                    default:
                        break;
                }
            }
            return fp.DistanceHorizontalTo(sp);
        }
        public static void RemoveColinearVerticesPolyline(Polyline pline)
        {
            List<int> verticesToRemove = new List<int>();

            for (int i = 0; i < pline.NumberOfVertices - 1; i++)
            {
                SegmentType st1 = pline.GetSegmentType(i);
                SegmentType st2 = pline.GetSegmentType(i + 1);
                if (st1 == SegmentType.Line && st1 == st2)
                {
                    LineSegment2d ls2d1 = pline.GetLineSegment2dAt(i);
                    LineSegment2d ls2d2 = pline.GetLineSegment2dAt(i + 1);

                    if (ls2d1.IsColinearTo(ls2d2)) verticesToRemove.Add(i + 1);
                }
            }

            verticesToRemove.Reverse();
            pline.CheckOrOpenForWrite();
            for (int j = 0; j < verticesToRemove.Count; j++)
                pline.RemoveVertexAt(verticesToRemove[j]);
        }
        /// <summary>
        /// Requires called inside a transaction and creates new Polyline3d
        /// in the same database as the original, original is the deleted.
        /// </summary>
        public static void RemoveColinearVertices3dPolyline(Polyline3d pline)
        {
            Transaction tx = pline.Database.TransactionManager.TopTransaction;
            HashSet<int> verticesToRemove = new HashSet<int>();

            PolylineVertex3d[] vertices = pline.GetVertices(tx);

            for (int i = 0; i < vertices.Length - 2; i++)
            {
                PolylineVertex3d vertex1 = vertices[i];
                PolylineVertex3d vertex2 = vertices[i + 1];
                PolylineVertex3d vertex3 = vertices[i + 2];

                Vector3d vec1 = vertex1.Position.GetVectorTo(vertex2.Position);
                Vector3d vec2 = vertex2.Position.GetVectorTo(vertex3.Position);

                if (vec1.IsCodirectionalTo(vec2, Tolerance.Global)) verticesToRemove.Add(i + 1);
            }

            Point3dCollection p3ds = new Point3dCollection();

            for (int i = 0; i < vertices.Length; i++)
            {
                if (verticesToRemove.Contains(i)) continue;
                PolylineVertex3d v = vertices[i];
                p3ds.Add(v.Position);
            }

            Polyline3d nyPline = new Polyline3d(Poly3dType.SimplePoly, p3ds, false);
            nyPline.AddEntityToDbModelSpace(pline.Database);

            nyPline.Layer = pline.Layer;

            pline.CheckOrOpenForWrite();
            pline.Erase(true);
        }
        struct Segment
        {
            public Point2d StartPt { get; set; }
            public Point2d EndPt { get; set; }
            public double Bulge { get; set; }
        }
        /// <summary>
        /// Union of two rectangular closed polylines
        /// </summary>
        public static IEnumerable<Polyline> Union(IEnumerable<Polyline> plines)
        {
            foreach (var group in plines.GroupBy(pl => new { pl.Elevation, pl.Normal }))
            {
                if (group.Count() == 1)
                {
                    yield return group.First();
                }
                else
                {
                    var plane = new Plane(Point3d.Origin, group.Key.Normal);
                    var segs = new List<Segment>();
                    using (var dbObjects = new DBObjectCollection())
                    {
                        foreach (var pline in group)
                        {
                            pline.Explode(dbObjects);
                        }
                        using (DBObjectCollection regions = Region.CreateFromCurves(dbObjects))
                        {
                            var region = (Region)regions[0];
                            for (int i = 1; i < regions.Count; i++)
                            {
                                region.BooleanOperation(BooleanOperationType.BoolUnite, (Region)regions[i]);
                                regions[i].Dispose();
                            }
                            foreach (DBObject o in dbObjects) o.Dispose();
                            dbObjects.Clear();
                            region.Explode(dbObjects);
                            region.Dispose();
                            for (int i = 0; i < dbObjects.Count; i++)
                            {
                                if (dbObjects[i] is Region)
                                {
                                    ((Region)dbObjects[i]).Explode(dbObjects);
                                    continue;
                                }
                                var curve = (Curve)dbObjects[i];
                                Point3d start = curve.StartPoint;
                                Point3d end = curve.EndPoint;
                                double bulge = 0.0;
                                if (curve is Arc)
                                {
                                    Arc arc = (Arc)curve;
                                    double angle = arc.Center.GetVectorTo(start).GetAngleTo(arc.Center.GetVectorTo(end), arc.Normal);
                                    bulge = Math.Tan(angle / 4.0);
                                }
                                segs.Add(new Segment { StartPt = start.Convert2d(plane), EndPt = end.Convert2d(plane), Bulge = bulge });
                            }
                            foreach (DBObject obj in dbObjects) obj.Dispose();
                            while (segs.Count > 0)
                            {
                                using (Polyline pline = new Polyline())
                                {
                                    pline.AddVertexAt(0, segs[0].StartPt, segs[0].Bulge, 0.0, 0.0);
                                    Point2d pt = segs[0].EndPt;
                                    segs.RemoveAt(0);
                                    int vtx = 1;
                                    while (true)
                                    {
                                        int i = segs.FindIndex(delegate (Segment s)
                                        {
                                            return s.StartPt.IsEqualTo(pt) || s.EndPt.IsEqualTo(pt);
                                        });
                                        if (i < 0) break;
                                        Segment seg = segs[i];
                                        if (seg.EndPt.IsEqualTo(pt))
                                            seg = new Segment { StartPt = seg.EndPt, EndPt = seg.StartPt, Bulge = -seg.Bulge };
                                        pline.AddVertexAt(vtx, seg.StartPt, seg.Bulge, 0.0, 0.0);
                                        pt = seg.EndPt;
                                        segs.RemoveAt(i);
                                        vtx++;
                                    }
                                    pline.Closed = true;
                                    pline.Normal = group.Key.Normal;
                                    pline.Elevation = group.Key.Elevation;
                                    yield return pline;
                                }
                            }
                        }
                    }
                }
            }
        }

        private static bool Clockwise(Point2d p1, Point2d p2, Point2d p3)
        {
            return ((p2.X - p1.X) * (p3.Y - p1.Y) - (p2.Y - p1.Y) * (p3.X - p1.X)) < 1e-9;
        }
        private static Point2d _p0;
        private static double Cosine(Point2d pt)
        {
            double d = _p0.GetDistanceTo(pt);
            return d == 0.0 ? 1.0 : Math.Round((pt.X - _p0.X) / d, 9);
        }
        private static List<Point2d> ConvexHull(List<Point2d> pts)
        {
            _p0 = pts.OrderBy(p => p.Y).ThenBy(p => p.X).First();
            pts = pts.OrderByDescending(p => Cosine(p)).ThenBy(p => _p0.GetDistanceTo(p)).ToList();
            for (int i = 1; i < pts.Count - 1; i++)
            {
                while (i > 0 && Clockwise(pts[i - 1], pts[i], pts[i + 1]))
                {
                    pts.RemoveAt(i);
                    i--;
                }
            }
            return pts;
        }
        public static Polyline PolylineFromConvexHull(List<Point2d> pts)
        {
            pts = ConvexHull(pts);
            Polyline pline = new Polyline();
            for (int i = 0; i < pts.Count; i++)
            {
                pline.AddVertexAt(i, pts[i], 0.0, 0.0, 0.0);
            }
            pline.Closed = true;
            return pline;
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
        public enum TypeOfSegment
        {
            None,
            Straight,
            ElasticArc,
            CurvedPipe
        }
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

    public class ProfileViewCollection : Collection<ProfileView>
    {
        public ProfileViewCollection(ICollection<ProfileView> sourceCollection)
        {
            foreach (ProfileView pv in sourceCollection) this.Add(pv);
        }

        public ProfileViewCollection(ObjectIdCollection ids)
        {
            foreach (Oid id in ids)
                this.Add(id.Go<ProfileView>(id.Database.TransactionManager.TopTransaction));
        }

        public ProfileView GetProfileViewAtStation(double station)
        {
            return this.Where(x => x.StationStart <= station && x.StationEnd >= station).FirstOrDefault();
        }
    }
    public class DataReferencesOptions
    {
        public const string PathToStierCsv = "X:\\AutoCAD DRI - 01 Civil 3D\\Stier.csv";
        public string ProjectName;
        public string EtapeName;
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
            pKeyOpts.AllowNone = true;
            pKeyOpts.Keywords.Default = kwds.First();
            //pKeyOpts.AllowNone = false;
            //pKeyOpts.AllowArbitraryInput = true;
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
        public DataReferencesOptions()
        {
            ProjectName = GetProjectName();
            EtapeName = GetEtapeName(ProjectName);
        }
        public DataReferencesOptions(string projectName, string etapeName)
        {
            ProjectName = projectName;
            EtapeName = etapeName;
        }
    }
    public class PipelineSizeArray
    {
        public SizeEntry[] SizeArray;
        public int Length { get => SizeArray.Length; }
        public PipelineSizesDirection Direction { get; }
        public int StartingDn { get; }
        public SizeEntry this[int index] { get => SizeArray[index]; }
        /// <summary>
        /// SizeArray listing sizes, station ranges and jacket diameters.
        /// Use empty brs collection or omit it to force size table based on curves.
        /// </summary>
        /// <param name="al">Current alignment.</param>
        /// <param name="brs">All transitions belonging to the current alignment.</param>
        /// <param name="curves">All pipline curves belonging to the current alignment.</param>
        public PipelineSizeArray(Alignment al, HashSet<Curve> curves, HashSet<BlockReference> brs = default)
        {
            #region Read CSV
            System.Data.DataTable dynBlocks = default;
            try
            {
                dynBlocks = CsvReader.ReadCsvToDataTable(
                        @"X:\AutoCAD DRI - 01 Civil 3D\FJV Dynamiske Komponenter.csv", "FjvKomponenter");
            }
            catch (System.Exception ex)
            {
                Utils.prdDbg("Reading of FJV Dynamiske Komponenter.csv failed!");
                Utils.prdDbg(ex.ToString());
                throw;
            }
            if (dynBlocks == default)
            {
                Utils.prdDbg("Reading of FJV Dynamiske Komponenter.csv failed!");
                throw new System.Exception("Failed to read FJV Dynamiske Komponenter.csv");
            }
            #endregion

            #region Direction
            //Determine pipe size direction
            int maxDn = PipeSchedule.GetPipeDN(curves.MaxBy(x => PipeSchedule.GetPipeDN(x)).FirstOrDefault());
            int minDn = PipeSchedule.GetPipeDN(curves.MinBy(x => PipeSchedule.GetPipeDN(x)).FirstOrDefault());

            HashSet<(Curve curve, double dist)> curveDistTuples =
                            new HashSet<(Curve curve, double dist)>();

            Point3d samplePoint = al.GetPointAtDist(0);

            foreach (Curve curve in curves)
            {
                if (curve.GetDistanceAtParameter(curve.EndParam) < 1.0) continue;
                Point3d closestPoint = curve.GetClosestPointTo(samplePoint, false);
                if (closestPoint != default)
                    curveDistTuples.Add(
                        (curve, samplePoint.DistanceHorizontalTo(closestPoint)));
            }

            Curve closestCurve = curveDistTuples.MinBy(x => x.dist).FirstOrDefault().curve;

            StartingDn = PipeSchedule.GetPipeDN(closestCurve);

            if (maxDn == minDn) Direction = PipelineSizesDirection.OneSize;
            else if (StartingDn == minDn) Direction = PipelineSizesDirection.SmallToLargeAscending;
            else if (StartingDn == maxDn) Direction = PipelineSizesDirection.LargeToSmallDescending;
            else Direction = PipelineSizesDirection.Unknown;

            if (Direction == PipelineSizesDirection.Unknown)
                throw new System.Exception($"Alignment {al.Name} could not determine pipeline sizes direction!");
            #endregion

            //Filter brs
            if (brs != default)
                brs = brs.Where(x => IsTransition(x, dynBlocks)).ToHashSet();

            //Dispatcher constructor
            if (brs == default || brs.Count == 0 || Direction == PipelineSizesDirection.OneSize)
                SizeArray = ConstructWithCurves(al, curves);
            else SizeArray = ConstructWithBlocks(al, brs, dynBlocks);
        }
        public PipelineSizeArray(SizeEntry[] sizeArray) { SizeArray = sizeArray; }
        public PipelineSizeArray GetPartialSizeArrayForPV(ProfileView pv)
        {
            var list = this.GetIndexesOfSizesAppearingInProfileView(pv.StationStart, pv.StationEnd);
            SizeEntry[] partialAr = new SizeEntry[list.Count];
            for (int i = 0; i < list.Count; i++) partialAr[i] = this[list[i]];
            return new PipelineSizeArray(partialAr);
        }
        public SizeEntry GetSizeAtStation(double station)
        {
            for (int i = 0; i < SizeArray.Length; i++)
            {
                SizeEntry curEntry = SizeArray[i];
                //(stations are END stations!)
                if (station < curEntry.EndStation) return curEntry;
            }
            return default;
        }
        public override string ToString()
        {
            string output = "";
            for (int i = 0; i < SizeArray.Length; i++)
            {
                output +=
                    $"{SizeArray[i].DN.ToString("D3")} || " +
                    $"{SizeArray[i].StartStation.ToString("0000.00")} - {SizeArray[i].EndStation.ToString("0000.00")} || " +
                    $"{SizeArray[i].Kod.ToString("0")}\n";
            }

            return output;
        }
        private List<int> GetIndexesOfSizesAppearingInProfileView(double pvStationStart, double pvStationEnd)
        {
            List<int> indexes = new List<int>();
            for (int i = 0; i < SizeArray.Length; i++)
            {
                SizeEntry curEntry = SizeArray[i];
                if (pvStationStart < curEntry.EndStation &&
                    curEntry.StartStation < pvStationEnd) indexes.Add(i);
            }
            return indexes;
        }
        private SizeEntry[] ConstructWithCurves(Alignment al, HashSet<Curve> curves)
        {
            List<SizeEntry> sizes = new List<SizeEntry>();
            double stepLength = 0.1;
            double alLength = al.Length;
            int nrOfSteps = (int)(alLength / stepLength);
            int previousDn = 0;
            int currentDn = 0;
            double previousKod = 0;
            double currentKod = 0;
            for (int i = 0; i < nrOfSteps + 1; i++)
            {
                double curStationBA = stepLength * i;
                Point3d curSamplePoint = default;
                try { curSamplePoint = al.GetPointAtDist(curStationBA); }
                catch (System.Exception) { continue; }

                HashSet<(Curve curve, double dist, double kappeOd)> curveDistTuples =
                    new HashSet<(Curve curve, double dist, double kappeOd)>();

                foreach (Curve curve in curves)
                {
                    //if (curve.GetDistanceAtParameter(curve.EndParam) < 1.0) continue;
                    Point3d closestPoint = curve.GetClosestPointTo(curSamplePoint, false);
                    if (closestPoint != default)
                        curveDistTuples.Add(
                            (curve, curSamplePoint.DistanceHorizontalTo(closestPoint),
                                PipeSchedule.GetPipeKOd(curve)));
                }
                var result = curveDistTuples.MinBy(x => x.dist).FirstOrDefault();
                //Detect current dn and kod
                currentDn = PipeSchedule.GetPipeDN(result.curve);
                currentKod = result.kappeOd;
                if (currentDn != previousDn || !currentKod.Equalz(previousKod, 1e-6))
                {
                    //Set the previous segment end station unless there's 0 segments
                    if (sizes.Count != 0)
                    {
                        SizeEntry toUpdate = sizes[sizes.Count - 1];
                        sizes[sizes.Count - 1] = new SizeEntry(toUpdate.DN, toUpdate.StartStation, curStationBA, toUpdate.Kod);
                    }
                    //Add the new segment; remember, 0 is because the station will be set next iteration
                    //see previous line
                    if (i == 0) sizes.Add(new SizeEntry(currentDn, 0, 0, result.kappeOd));
                    else sizes.Add(new SizeEntry(currentDn, sizes[sizes.Count - 1].EndStation, 0, result.kappeOd));
                }
                //Hand over DN to cache in "previous" variable
                previousDn = currentDn;
                previousKod = currentKod;
                if (i == nrOfSteps)
                {
                    SizeEntry toUpdate = sizes[sizes.Count - 1];
                    sizes[sizes.Count - 1] = new SizeEntry(toUpdate.DN, toUpdate.StartStation, al.Length, toUpdate.Kod);
                }
            }

            return sizes.ToArray();
        }
        private SizeEntry[] ConstructWithBlocks(Alignment al, HashSet<BlockReference> brs, System.Data.DataTable dt)
        {
            BlockReference[] brsArray = default;
            if (Direction == PipelineSizesDirection.SmallToLargeAscending)
                brsArray = brs.OrderBy(x => ReadComponentDN2Int(x, dt)).ToArray();
            else if (Direction == PipelineSizesDirection.LargeToSmallDescending)
                brsArray = brs.OrderByDescending(x => ReadComponentDN2Int(x, dt)).ToArray();
            else brs.ToArray();

            List<SizeEntry> sizes = new List<SizeEntry>();
            double alLength = al.Length;

            int dn = 0;
            double start = 0;
            double end = 0;
            double kod = 0;
            double offset = 0;

            for (int i = 0; i < brsArray.Length; i++)
            {
                BlockReference curBr = brsArray[i];

                if (i == 0)
                {
                    //First iteration case
                    dn = GetDirectionallyCorrectDn(curBr, Side.Left, dt);
                    start = 0;
                    Point3d p3d = al.GetClosestPointTo(curBr.Position, false);
                    al.StationOffset(p3d.X, p3d.Y, ref end, ref offset);
                    kod = GetDirectionallyCorrectKod(curBr, Side.Left, dt);

                    sizes.Add(new SizeEntry(dn, start, end, kod));

                    if (brsArray.Length == 1)
                    {
                        //Only one member array case
                        dn = GetDirectionallyCorrectDn(curBr, Side.Right, dt);
                        start = end;
                        end = alLength;
                        kod = GetDirectionallyCorrectKod(curBr, Side.Right, dt);

                        sizes.Add(new SizeEntry(dn, start, end, kod));
                        //This guards against executing further code
                        continue;
                    }
                }

                if (i != brsArray.Length - 1)
                {
                    //General case
                    BlockReference nextBr = brsArray[i + 1];

                    dn = GetDirectionallyCorrectDn(curBr, Side.Right, dt);
                    start = end;
                    Point3d p3d = al.GetClosestPointTo(nextBr.Position, false);
                    al.StationOffset(p3d.X, p3d.Y, ref end, ref offset);
                    kod = GetDirectionallyCorrectKod(curBr, Side.Right, dt);

                    sizes.Add(new SizeEntry(dn, start, end, kod));
                    //This guards against executing further code
                    continue;
                }

                //And here ends the last iteration
                dn = GetDirectionallyCorrectDn(curBr, Side.Right, dt);
                start = end;
                end = alLength;
                kod = GetDirectionallyCorrectKod(curBr, Side.Right, dt);

                sizes.Add(new SizeEntry(dn, start, end, kod));
            }

            return sizes.ToArray();
        }
        private int GetDirectionallyCorrectDn(BlockReference br, Side side, System.Data.DataTable dt)
        {
            switch (Direction)
            {
                case PipelineSizesDirection.SmallToLargeAscending:
                    switch (side)
                    {
                        case Side.Left:
                            return ReadComponentDN2Int(br, dt);
                        case Side.Right:
                            return ReadComponentDN1Int(br, dt);
                    }
                    break;
                case PipelineSizesDirection.LargeToSmallDescending:
                    switch (side)
                    {
                        case Side.Left:
                            return ReadComponentDN1Int(br, dt);
                        case Side.Right:
                            return ReadComponentDN2Int(br, dt);
                    }
                    break;
            }
            return 0;
        }
        private double GetDirectionallyCorrectKod(BlockReference br, Side side, System.Data.DataTable dt)
        {
            switch (Direction)
            {
                case PipelineSizesDirection.SmallToLargeAscending:
                    switch (side)
                    {
                        case Side.Left:
                            return ReadComponentDN2KodDouble(br, dt);
                        case Side.Right:
                            return ReadComponentDN1KodDouble(br, dt);
                    }
                    break;
                case PipelineSizesDirection.LargeToSmallDescending:
                    switch (side)
                    {
                        case Side.Left:
                            return ReadComponentDN1KodDouble(br, dt);
                        case Side.Right:
                            return ReadComponentDN2KodDouble(br, dt);
                    }
                    break;
            }
            return 0;
        }
        private bool IsTransition(BlockReference br, System.Data.DataTable dynBlocks)
        {
            string type = ReadStringParameterFromDataTable(br.RealName(), dynBlocks, "Type", 0);

            if (type == null) throw new System.Exception($"Block with name {br.RealName()} does not exist " +
                $"in Dynamiske Komponenter!");

            return type == "Reduktion";
        }
        internal void Reverse()
        {
            Array.Reverse(this.SizeArray);
        }
        /// <summary>
        /// Unknown - Should throw an exception
        /// OneSize - Cannot be constructed with blocks
        /// SmallToLargeAscending - Small sizes first, blocks preferred
        /// LargeToSmallAscending - Large sizes first, blocks preferred
        /// </summary>
        public enum PipelineSizesDirection
        {
            Unknown, //Should throw an exception
            OneSize, //Cannot be constructed with blocks
            SmallToLargeAscending, //Blocks preferred
            LargeToSmallDescending //Blocks preferred
        }
        private enum Side
        {
            //Left means towards the start of alignment
            Left,
            //Right means towards the end of alignment
            Right
        }
    }
    public struct SizeEntry
    {
        public readonly int DN;
        public readonly double StartStation;
        public readonly double EndStation;
        public readonly double Kod;

        public SizeEntry(int dn, double startStation, double endStation, double kod)
        {
            DN = dn; StartStation = startStation; EndStation = endStation; Kod = kod;
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

    #region Character method
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
    #endregion

    public class WeldPointData
    {
        public Point3d WeldPoint { get; set; }
        public Alignment Alignment { get; set; }
        public Enums.TypeOfIteration IterationType { get; set; }
        public double Station { get; set; }
        public Entity SourceEntity { get; set; }
        public int DN { get; set; }
        public string System { get; set; }

    }

    /// <summary>
    /// From here: https://www.theswamp.org/index.php?topic=42503.msg477118#msg477118
    /// </summary>
    public static class ViewportExtensionMethods
    {
        public static Matrix3d GetModelToPaperTransform(this Viewport vport)
        {
            if (vport.PerspectiveOn)
                throw new NotSupportedException("Perspective views not supported");
            Point3d center = new Point3d(vport.ViewCenter.X, vport.ViewCenter.Y, 0.0);
            return Matrix3d.Displacement(new Vector3d(vport.CenterPoint.X - center.X, vport.CenterPoint.Y - center.Y, 0.0))
               * Matrix3d.Scaling(vport.CustomScale, center)
               * Matrix3d.Rotation(vport.TwistAngle, Vector3d.ZAxis, Point3d.Origin)
               * Matrix3d.WorldToPlane(new Plane(vport.ViewTarget, vport.ViewDirection));
        }

        public static Matrix3d GetPaperToModelTransform(this Viewport vport)
        {
            return GetModelToPaperTransform(vport).Inverse();
        }

        public static Point3d PaperToModel(this Point3d point, Viewport vport)
        {
            return point.TransformBy(GetModelToPaperTransform(vport).Inverse());
        }

        public static Point3d ModelToPaper(this Point3d point, Viewport viewport)
        {
            return point.TransformBy(GetModelToPaperTransform(viewport));
        }

        public static void PaperToModel(this Entity entity, Viewport vport)
        {
            entity.TransformBy(GetModelToPaperTransform(vport).Inverse());
        }

        public static void ModelToPaper(this Entity entity, Viewport viewport)
        {
            entity.TransformBy(GetModelToPaperTransform(viewport));
        }

        public static IEnumerable<Point3d> PaperToModel(this IEnumerable<Point3d> source, Viewport viewport)
        {
            Matrix3d xform = GetModelToPaperTransform(viewport).Inverse();
            return source.Select(p => p.TransformBy(xform));
        }

        public static IEnumerable<Point3d> ModelToPaper(this IEnumerable<Point3d> source, Viewport viewport)
        {
            Matrix3d xform = GetModelToPaperTransform(viewport);
            return source.Select(p => p.TransformBy(xform));
        }

        public static void PaperToModel(this IEnumerable<Entity> src, Viewport viewport)
        {
            Matrix3d xform = GetModelToPaperTransform(viewport).Inverse();
            foreach (Entity ent in src)
                ent.TransformBy(xform);
        }

        public static void ModelToPaper(this IEnumerable<Entity> src, Viewport viewport)
        {
            Matrix3d xform = GetModelToPaperTransform(viewport);
            foreach (Entity ent in src)
                ent.TransformBy(xform);
        }
    }
}

