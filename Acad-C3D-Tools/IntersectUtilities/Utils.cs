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
using IntersectUtilities.DynamicBlocks;

using static IntersectUtilities.UtilsCommon.Utils;
using static IntersectUtilities.UtilsCommon.UtilsDataTables;
using static IntersectUtilities.UtilsCommon.UtilsODData;
using static IntersectUtilities.PipeScheduleV2.PipeScheduleV2;

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
using Dreambuild.AutoCAD;

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
        public static void OutputWriter(string fullPathAndName, string sr, bool clearFile = false, bool useBOM = true)
        {
            if (clearFile) System.IO.File.WriteAllBytes(fullPathAndName, new byte[0]);

            if (useBOM)
            {
                // Write to output file
                using (StreamWriter w = new StreamWriter(fullPathAndName, true, Encoding.UTF8))
                {
                    w.Write(sr);
                    w.Close();
                }
            }
            else
            {
                // Create UTF-8 encoding without BOM
                var utf8WithoutBom = new System.Text.UTF8Encoding(false);

                // Write to output file
                using (StreamWriter w = new StreamWriter(fullPathAndName, true, utf8WithoutBom))
                {
                    w.Write(sr);
                    w.Close();
                }
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
                prdDbg(ex);
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
            double station = 0;

            for (int i = 0; i < length + 1; i++)
            {
                try
                {
                    station = 0;
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
                catch (System.Exception)
                {
                    prdDbg($"Failing ST: {station}");
                    throw;
                }
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
                        if (allowedValues.Length == 0)
                        {
                            prop.Value = propertyValue;
                        }
                        else
                        {
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

            int firstDn = GetPipeDN(firstEnd);
            int secondDn = GetPipeDN(secondEnd);

            if (firstDn == secondDn)
            {
                prdDbg(
                    $"ADVARSEL: Alignment {al.Name} har kun én dimension! " +
                    $"Vend polylines manuelt. " +
                    $"Brug kommando \"TOGGLEFJVDIR\" fra AcadOverrules.dll til at kunne se retningen på linjerne.");

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
                    tempP3d = al.GetPolyline()
                        .Go<Polyline>(al.Database.TransactionManager.TopTransaction)
                        .GetClosestPointTo(tempP3d, false);
                    double curveStartStation = 0;
                    double offset = 0;

                    try
                    {
                        al.StationOffset(tempP3d.X, tempP3d.Y, ref curveStartStation, ref offset);
                    }
                    catch (System.Exception)
                    {
                        prdDbg(al.Name);
                        prdDbg(curve.Handle);
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
                        prdDbg($"Error ent: {curve.Handle}, TempP3d: " + tempP3d.ToString());
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
                            double minRadius = GetPipeMinElasticRadius(pline);

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
        public static void RemoveColinearVerticesPolyline(
            Polyline pline, ref int guiltyPlineCount, ref int removedVerticesCount)
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

            for (int i = 0; i < pline.NumberOfVertices; i++)
            {
                SegmentType st1 = pline.GetSegmentType(i);
                if (st1 == SegmentType.Coincident) verticesToRemove.Add(i);
            }

            if (verticesToRemove.Count > 0)
            {
                guiltyPlineCount++;
                removedVerticesCount += verticesToRemove.Count;

                verticesToRemove.Sort();
                verticesToRemove.Reverse();
                pline.CheckOrOpenForWrite();
                for (int j = 0; j < verticesToRemove.Count; j++)
                    pline.RemoveVertexAt(verticesToRemove[j]);
            }
        }
        public static void RemoveColinearVerticesPolyline(
            Polyline pline)
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

            for (int i = 0; i < pline.NumberOfVertices; i++)
            {
                SegmentType st1 = pline.GetSegmentType(i);
                if (st1 == SegmentType.Coincident) verticesToRemove.Add(i);
            }

            if (verticesToRemove.Count > 0)
            {
                verticesToRemove.Sort();
                verticesToRemove.Reverse();
                pline.CheckOrOpenForWrite();
                for (int j = 0; j < verticesToRemove.Count; j++)
                    pline.RemoveVertexAt(verticesToRemove[j]);
            }
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
        public static string ConstructStringFromPSByRecipe(
            Entity ent, string stringToProcess)
        {
            //Construct pattern which matches the parameter definition
            Regex variablePattern = new Regex(@"{(?<psname>[a-zæøåA-ZÆØÅ0-9_-]*):(?<propname>[a-zæøåA-ZÆØÅ0-9_-]*)}");

            //Test if a pattern matches in the input string
            if (variablePattern.IsMatch(stringToProcess))
            {
                //Get the first match
                Match match = variablePattern.Match(stringToProcess);
                //Get the first capture, it needs to be replaced by the property value
                string capture = match.Captures[0].Value;
                //Retreive PS name and property name from match
                string psName = match.Groups["psname"].Value;
                string propName = match.Groups["propname"].Value;
                //Read the value from PS
                string parameterValue =
                    PropertySetManager.ReadNonDefinedPropertySetString(
                        ent, psName, propName);
                //Replace the captured group in original string with the parameter value
                stringToProcess = stringToProcess.Replace(capture, parameterValue);
                //Recursively call current function
                //It runs on the string until no more captures remain
                //Then it returns
                stringToProcess = ConstructStringFromPSByRecipe(ent, stringToProcess);
            }

            return stringToProcess;
        }
        public static string ProcessDescription(Entity ent,
            string descrFromKrydsninger, System.Data.DataTable dtKrydsninger)
        {
            Regex columnNameRegex = new Regex(@"{(?<column>[a-zæøåA-ZÆØÅ_-]*)}");

            if (columnNameRegex.IsMatch(descrFromKrydsninger))
            {
                //Get the first match
                Match match = columnNameRegex.Match(descrFromKrydsninger);
                //Get the first capture, it needs to be replaced by the property value
                string capture = match.Captures[0].Value;
                //Get the column name
                string columnName = match.Groups["column"].Value;
                //Retreive recipe from column
                string recipe = ReadStringParameterFromDataTable(
                    ent.Layer, dtKrydsninger, columnName, 0);
                //Replace the captured group in original string with the parameter value
                descrFromKrydsninger = descrFromKrydsninger
                    .Replace(capture, recipe);
                //Recursively call current function
                //It runs on the string until no more captures remain
                descrFromKrydsninger =
                    ProcessDescription(
                        ent, descrFromKrydsninger, dtKrydsninger);
            }

            //When all column names are replaced by recipes ->
            //Replace recipes
            descrFromKrydsninger = ConstructStringFromPSByRecipe(
                ent, descrFromKrydsninger);

            return descrFromKrydsninger;
        }
        /// <summary>
        /// Assumes no bulges, eg. works only on plines without bulges.
        /// </summary>
        public static OverlapStatusEnum GetOverlapStatus(Polyline fP, Polyline sP)
        {
            Extents3d ext1 = fP.GeometricExtents;
            Extents3d ext2 = sP.GeometricExtents;
            if (!ext1.ToExtents2d().IsOverlapping(ext2.ToExtents2d()))
                return OverlapStatusEnum.None;

            Tolerance tol = new Tolerance(0.0001, 0.0001);

            var points1 = fP.GetPoints();
            var points2 = sP.GetPoints();

            var pointsOnLine1 = points1.Where(x => sP.GetDistToPoint(x) < tol.EqualPoint);
            int numberOfPoints1 = pointsOnLine1.Count();
            var pointsOnLine2 = points2.Where(x => fP.GetDistToPoint(x) < tol.EqualPoint);
            int numberOfPoints2 = pointsOnLine2.Count();

            //Case 1: No overlap detected
            if (numberOfPoints1 == 0 && numberOfPoints2 == 0) return OverlapStatusEnum.None;
            //Case 2: Partial overlap only by one segment
            //Case 2: Needs testing to discern from end-to-end connection
            if (numberOfPoints1 == 1 || numberOfPoints2 == 1)
            {
                var point1 = pointsOnLine1.FirstOrDefault();
                var point2 = pointsOnLine2.FirstOrDefault();

                if (point1 == default || point2 == default) return OverlapStatusEnum.None;

                if (point1.IsEqualTo(point2, tol)) return OverlapStatusEnum.None;
                else return OverlapStatusEnum.Partial;
            }
            //Case 3: Full overlap (assumes that all vertices are equal)
            if (points1.All(x => points2.Any(y => y.IsEqualTo(x, tol)))) return OverlapStatusEnum.Full;

            //Case 4: Partial overlap
            //Case 4: Reached by eliminating all other cases
            return OverlapStatusEnum.Partial;
        }
        public enum OverlapStatusEnum
        {
            None,
            Partial,
            Full
        }
        public static double GetTransitionLength(Transaction tx, BlockReference transition)
        {
            if (transition == null) return 0;
            if (transition.GetPipelineType() != PipelineElementType.Reduktion)
            {
                throw new System.Exception(
                    $"GetTransitionLength recieved non-transition block " +
                    $"{transition.RealName()}, {transition.Handle}!");
            }

            BlockTableRecord btr = transition.BlockTableRecord.Go<BlockTableRecord>(tx);

            var points = btr
                .ToIEnumerable()
                .Select(id => id.Go<BlockReference>(tx))
                .Where(br => br != null && br.Name.Contains("MuffeIntern"))
                .Select(br => br.Position)
                .Take(2)
                .ToArray();

            if (points.Length != 2) throw new System.Exception(
                $"Transition {transition.Handle} does not have EXACTLY two MuffeIntern blocks!");

            return points[0].DistanceHorizontalTo(points[1]);
        }
    }

    public static class UtilsExtensions
    {
        public static List<T> ListOfType<T>(this Database database, Transaction tr,
            string propertySetNameFilter, string propertyNameFilter, string propertyValueFilter,
            PropertySetManager.MatchTypeEnum filterType, bool discardFrozen = false)
            where T : Autodesk.AutoCAD.DatabaseServices.Entity
        {
            //Init the list of the objects
            List<T> objs = new List<T>();

            // Get the block table for the current database
            var blockTable = (BlockTable)tr.GetObject(database.BlockTableId, OpenMode.ForRead);

            // Get the model space block table record
            var modelSpace = (BlockTableRecord)tr.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead);

            RXClass theClass = RXObject.GetClass(typeof(T));

            // Loop through the entities in model space
            foreach (Oid oid in modelSpace)
            {
                // Look for entities of the correct type
                if (oid.ObjectClass.IsDerivedFrom(theClass))
                {
                    var entity = (T)tr.GetObject(oid, OpenMode.ForRead);

                    if (discardFrozen)
                    {
                        LayerTableRecord layer = (LayerTableRecord)tr.GetObject(entity.LayerId, OpenMode.ForRead);
                        if (layer.IsFrozen) continue;
                    }

                    //Filter for PS
                    switch (filterType)
                    {
                        case PropertySetManager.MatchTypeEnum.Equals:
                            if (!PropertySetManager.ReadNonDefinedPropertySetString(entity, propertySetNameFilter, propertyNameFilter)
                                .Equals(propertyValueFilter, StringComparison.OrdinalIgnoreCase))
                                continue;
                            break;
                        case PropertySetManager.MatchTypeEnum.Contains:
                            if (!PropertySetManager.ReadNonDefinedPropertySetString(
                                entity, propertySetNameFilter, propertyNameFilter)
                                .Contains(propertyValueFilter, StringComparison.OrdinalIgnoreCase))
                                continue;
                            break;
                        default:
                            throw new System.Exception($"MatchTypeEnum {filterType} undefined!");
                    }

                    objs.Add(entity);
                }
            }
            return objs;
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
    public static class CommonScheduleExtensions
    {
        public static PipeTypeEnum GetEntityPipeType(this Entity ent, bool frToEnkelt = false)
        {
            PipeTypeEnum pipeTypeEnum = PipeTypeEnum.Ukendt;
            switch (ent)
            {
                case Polyline pline:
                    pipeTypeEnum = GetEntityPipeType(pline);
                    break;
                case BlockReference br:
                    string system = br.ReadDynamicCsvProperty(DynamicProperty.System);
                    if (!Enum.TryParse(system, out pipeTypeEnum)) return PipeTypeEnum.Ukendt;
                    break;
            }

            if (frToEnkelt && (pipeTypeEnum == PipeTypeEnum.Frem || pipeTypeEnum == PipeTypeEnum.Retur))
                pipeTypeEnum = PipeTypeEnum.Enkelt;
            return pipeTypeEnum;
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
        public string Serie { get; set; }
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

