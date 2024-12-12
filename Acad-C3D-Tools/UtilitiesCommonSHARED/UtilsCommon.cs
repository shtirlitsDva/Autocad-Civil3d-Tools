using Autodesk.Aec.PropertyData.DatabaseServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Gis.Map;
using Autodesk.Gis.Map.Constants;
using Autodesk.Gis.Map.ObjectData;
using Autodesk.Gis.Map.Utilities;

using MoreLinq;

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Serialization;

using static IntersectUtilities.PipeScheduleV2.PipeScheduleV2;
using static IntersectUtilities.UtilsCommon.Utils;

using AcRx = Autodesk.AutoCAD.Runtime;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using BlockReference = Autodesk.AutoCAD.DatabaseServices.BlockReference;
using DataTable = System.Data.DataTable;
using DataType = Autodesk.Gis.Map.Constants.DataType;
using DBObject = Autodesk.AutoCAD.DatabaseServices.DBObject;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using ErrorStatus = Autodesk.AutoCAD.Runtime.ErrorStatus;
using ObjectId = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using ObjectIdCollection = Autodesk.AutoCAD.DatabaseServices.ObjectIdCollection;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;
using DebugHelper = IntersectUtilities.UtilsCommon.Utils.DebugHelper;

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
        public static bool atZero(this double value) => value > -0.0001 && value < 0.0001;
        public static bool at99(this double value) => value < -98.0;
        public static bool is3D(this double value) => !atZero(value) && !at99(value);
        public static bool is3D(this Point3d p) => p.Z.is3D();
        public static bool is3D(this PolylineVertex3d v) => v.Position.is3D();
        public static bool is2D(this double value) => atZero(value) || at99(value);
        /// <summary>
        /// Order of returned coordinates explained here:
        /// https://macwright.com/lonlat/
        /// GeoJson is lon, lat.
        /// </summary>
        /// <param name="latlon">If false, reverses the returned array to lon, lat.</param
        public static double[] ToWGS84FromUtm32N(double X, double Y, bool latlon = true) =>
            Extensions.ToWGS84FromUtm32N(new Point2d(X, Y), latlon);
        /// <summary>
        /// Coords must by X, Y (lat, long).
        /// </summary>
        public static double[] ToWGS84FromUtm32N(double[] coords, bool latlon = true) =>
            Extensions.ToWGS84FromUtm32N(new Point2d(coords[0], coords[1]), latlon);
        public static void prdDbg(string msg = "") => Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\n" + msg);
        public static void prdDbg(object obj)
        {
            if (obj is SystemException ex1) prdDbg(obj.ToString().WrapThis(70));
            else if (obj is System.Exception ex2) prdDbg(obj.ToString().WrapThis(70));
            else prdDbg(obj.ToString());
        }
        /// <summary>
        /// Returns a list of strings no larger than the max length sent in.
        /// </summary>
        /// <remarks>useful function used to wrap string text for reporting.</remarks>
        /// <param name="text">Text to be wrapped into of List of Strings</param>
        /// <param name="maxLength">Max length you want each line to be.</param>
        /// <returns>List of Strings</returns>
        public static string WrapThis(this string s, int maxLength)
        {
            if (string.IsNullOrEmpty(s))
            {
                return s;
            }

            var lines = s.Split(new[] { '\r', '\n' }, StringSplitOptions.None);
            var wrappedLines = new List<string>();

            foreach (var line in lines)
            {
                if (line.Length <= maxLength)
                {
                    wrappedLines.Add(line);
                }
                else
                {
                    int start = 0;
                    while (start < line.Length)
                    {
                        int length = Math.Min(maxLength, line.Length - start);
                        wrappedLines.Add(line.Substring(start, length));
                        start += length;
                    }
                }
            }
            return string.Join("\n", wrappedLines);
        }
        public static void PrintTable(string[] headers, IEnumerable<IEnumerable<object>> rows)
        {
            // Calculate the maximum width of each column
            int[] columnWidths = new int[headers.Length];
            for (int i = 0; i < headers.Length; i++)
            {
                columnWidths[i] = headers[i].Length;  // Start with the header width
                foreach (var row in rows)
                {
                    var rowList = row.ToList();  // Convert to list for easier indexing
                    if (i < rowList.Count && rowList[i] != null)
                    {
                        columnWidths[i] = Math.Max(columnWidths[i], rowList[i].ToString().Length);
                    }
                }
            }

            // Create the format string for each row
            string format = string.Join(" | ", columnWidths.Select((w, i) => $"{{{i},-{w}}}"));

            // Print the header
            prdDbg(string.Format(format, headers.Cast<object>().ToArray()));

            // Print a separator
            prdDbg(string.Join("-+-", columnWidths.Select(w => new string('-', w))));

            // Print each row of data
            foreach (var row in rows)
            {
                var rowData = row.Select(cell => cell?.ToString() ?? string.Empty).ToArray();
                prdDbg(string.Format(format, rowData));
            }
        }
        /// <summary>
        /// Gets keywords.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="keywords">The keywords.</param>
        /// <param name="defaultIndex">The default index.</param>
        /// <returns>The keyword result or null on else than OK.</returns>
        public static string GetKeywords(string message, ICollection<string> keywords)
        {
            if (keywords.Count == 0) return null;

            Dictionary<string, string> keywordsDict = new Dictionary<string, string>();
            int i = 0;
            string dKwd = "";
            foreach (string keyword in keywords)
            {
                string prefix = ColumnNameByIndex(i) + ":";
                string cleanedKeyword = RemoveSpecialCharacters(keyword, "[^a-zA-Z0-9]+");
                keywordsDict.Add(prefix + cleanedKeyword.ToUpper(), keyword);
                if (i == 0) dKwd = prefix + cleanedKeyword.ToUpper();
                i++;
            }

            string messageAndKeywords = message + " [";
            messageAndKeywords += string.Join("/", keywordsDict.Select(x => x.Key).ToArray());
            messageAndKeywords += "]";

            string globalKeywords = string.Join(" ", keywordsDict.Select(x => x.Key).ToArray());

            var ed = Application.DocumentManager.MdiActiveDocument.Editor;
            var opt = new PromptKeywordOptions(message);
            opt.AllowNone = true;

            opt.SetMessageAndKeywords(messageAndKeywords, globalKeywords);
            opt.Keywords.Default = dKwd;

            var res = ed.GetKeywords(opt);
            if (res.Status == PromptStatus.OK)
            {
                return keywordsDict[res.StringResult];
            }

            return null;
        }
        public static string RemoveSpecialCharacters(string str, string regex)
        {
            return Regex.Replace(str, regex, "", RegexOptions.Compiled);
        }
        /// <param name="name">byblock, red, yellow, green, cyan, blue, magenta, white, grey, bylayer</param>
        public static Color ColorByName(string name) => AutocadStdColors[name];
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
        private static Random random = new Random();
        public static string RandomStringLetters(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }
        private static string mColumnLetters = "zabcdefghijklmnopqrstuvwxyz";
        public static string ColumnNameByIndex(int ColumnIndex)
        {
            int ModOf26, Subtract;
            StringBuilder NumberInLetters = new StringBuilder();
            ColumnIndex += 1; // A is the first column, but for calculation it's number is 1 and not 0. however, Index is alsways zero-based.
            while (ColumnIndex > 0)
            {
                if (ColumnIndex <= 26)
                {
                    ModOf26 = ColumnIndex;
                    NumberInLetters.Insert(0, mColumnLetters.Substring(ModOf26, 1));
                    ColumnIndex = 0;
                }
                else
                {
                    ModOf26 = ColumnIndex % 26;
                    Subtract = (ModOf26 == 0) ? 26 : ModOf26;
                    ColumnIndex = (ColumnIndex - Subtract) / 26;
                    NumberInLetters.Insert(0, mColumnLetters.Substring(ModOf26, 1));
                }
            }
            return NumberInLetters.ToString().ToUpper();
        }
        public static Extents2d CreateExtents2DByTwoPoints(Point2d p1, Point2d p2)
        {
            List<double> xs = new List<double>() { p1.X, p2.X };
            List<double> ys = new List<double>() { p1.Y, p2.Y };
            return new Extents2d(
                p1.X < p2.X ? p1.X : p2.X,
                p1.Y < p2.Y ? p1.Y : p2.Y,
                p1.X > p2.X ? p1.X : p2.X,
                p1.Y > p2.Y ? p1.Y : p2.Y);
        }
        public static Point2d GetArcCenter(Point2d p1, Point2d p2, double bulge)
        {
            Vector2d delta = p2 - p1;
            double length = p1.GetDistanceTo(p2);
            double alpha = 4 * Math.Atan(bulge);
            double radius = length / (2d * Math.Abs(Math.Sin(alpha * 0.5)));
            Vector2d lnormalized = delta.GetNormal();
            double bulgeSign = Math.Sign(bulge);
            Vector2d lnormal = new Vector2d(-lnormalized.Y, lnormalized.X) * bulgeSign;
            return Point2d.Origin + ((p2.GetAsVector() + p1.GetAsVector()) * 0.5) +
              lnormal * Math.Cos(alpha * 0.5) * radius;
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

            string result = (string)query.First()[pathType];
            if (result.IsNoE()) throw new System.Exception(
                $"{pathType} mangler at blive defineret i " +
                $"X:\\AutoCAD DRI - 01 Civil 3D\\Stier.csv!");
            if (File.Exists(result) ||
                (pathType == "Ler" && (File.Exists(result) || Directory.Exists(result)))
                ) return result;
            else throw new System.Exception(
                $"Programmet kan ikke finde filen {result} " +
                $"på det specificerede sti: {result}");
        }
        public static void AbortGracefully(object exMsg, params Database[] dbs)
        {
            foreach (var db in dbs)
            {
                while (db.TransactionManager.TopTransaction != null)
                {
                    db.TransactionManager.TopTransaction.Abort();
                    if (db.TransactionManager.TopTransaction != null)
                        db.TransactionManager.TopTransaction.Dispose();
                }
                if (Application.DocumentManager.MdiActiveDocument.Database.Filename != db.Filename) db.Dispose();
            }
            if (exMsg is string str) prdDbg(str);
            else prdDbg(exMsg.ToString());
        }
        public static Entity GetEntityFromLocalDbByHandleString(string handle)
        {
            Database db = Application.DocumentManager.MdiActiveDocument.Database;
            Handle h = new Handle(Convert.ToInt64(handle, 16));
            return h.Go<Entity>(db);
        }
        public static Entity GetEntityFromLocalDbByHandle(Handle handle)
        {
            Database db = Application.DocumentManager.MdiActiveDocument.Database;
            return handle.Go<Entity>(db);
        }
        public static double GetStation(Alignment alignment, Entity entity)
        {
            double station = 0;
            double offset = 0;

            switch (entity)
            {
                case Polyline pline:
                    double l = pline.Length;
                    Point3d p = pline.GetPointAtDist(l / 2);
                    try
                    {
                        alignment.StationOffset(p.X, p.Y, 5.0, ref station, ref offset);
                    }
                    catch (System.Exception ex)
                    {
                        prdDbg($"GetStation: Entity {pline.Handle} throws {ex.Message}!");
                        throw;
                    }
                    break;
                case BlockReference block:
                    try
                    {
                        alignment.StationOffset(block.Position.X, block.Position.Y, 5.0, ref station, ref offset);
                    }
                    catch (Autodesk.Civil.PointNotOnEntityException ex)
                    {
                        prdDbg($"GetStation: Entity {block.Handle} throws {ex.Message}!");
                        throw;
                    }
                    break;
                default:
                    throw new System.Exception("GetStation: Invalid entity type");
            }
            return station;
        }
        #region Enums
        public enum EndType
        {
            None,            //0:
            Start,           //1: For start of pipes
            End,             //2: For ends of pipes
            Main,            //3: For main run in components
            Branch,          //4: For branches in components
            StikAfgrening,   //5: For points where stik are connected to supply pipes
            StikStart,       //6: For stik starts
            StikEnd,         //7: For stik ends
            WeldOn           //8: For elements welded directly on pipe without breaking it
        }
        public enum PipeTypeEnum
        {
            Ukendt,
            Twin,
            Frem,
            Retur,
            Enkelt //Bruges kun til blokke vist
        }
        public enum PipeSeriesEnum
        {
            Undefined,
            S1,
            S2,
            S3
        }
        public enum PipeDnEnum
        {
            ALUPEX26,
            ALUPEX32,
            CU22,
            CU28,
            DN20,
            DN25,
            DN32,
            DN40,
            DN50,
            DN65,
            DN80,
            DN100,
            DN125,
            DN150,
            DN200,
            DN250,
            DN300,
            DN350,
            DN400,
            DN450,
            DN500,
            DN600
        }
        public enum PipeSystemEnum
        {
            Ukendt,
            Stål,
            Kobberflex,
            AluPex,
            PertFlextra,
        }
        public enum DynamicProperty
        {
            None,
            Navn,
            Type,
            DN1,
            DN2,
            System,
            Vinkel,
            Serie,
            Version,
            TBLNavn,
            M1,
            M2,
            Function,
            SysNavn,
        }
        public enum CompanyEnum
        {
            Logstor,
            Isoplus
        }
        public static Dictionary<string, PipelineElementType> PipelineElementTypeDict =
            new Dictionary<string, PipelineElementType>()
            {
                { "Pipe", PipelineElementType.Pipe },
                { "Afgrening med spring", PipelineElementType.AfgreningMedSpring },
                { "Afgrening, parallel", PipelineElementType.AfgreningParallel },
                { "Afgreningsstuds", PipelineElementType.Afgreningsstuds },
                { "Endebund", PipelineElementType.Endebund },
                { "Engangsventil", PipelineElementType.Engangsventil },
                { "F-Model", PipelineElementType.F_Model },
                { "Kedelrørsbøjning", PipelineElementType.Kedelrørsbøjning },
                { "Kedelrørsbøjning, vertikal", PipelineElementType.Kedelrørsbøjning },
                { "Lige afgrening", PipelineElementType.LigeAfgrening },
                { "Parallelafgrening", PipelineElementType.AfgreningParallel },
                { "Præisoleret bøjning, 90gr", PipelineElementType.PræisoleretBøjning90gr },
                { "Præisoleret bøjning, 45gr", PipelineElementType.PræisoleretBøjning45gr },
                { "$Præisoleret bøjning, L {$L1}x{$L2} m, V {$V}°", PipelineElementType.PræisoleretBøjningVariabel },
                { "$Præisoleret bøjning, 90gr, L {$L1}x{$L2} m", PipelineElementType.PræisoleretBøjningVariabel },
                { "Præisoleret bøjning, L {$L1}x{$L2} m, V {$V}°", PipelineElementType.PræisoleretBøjningVariabel },
                { "Præisoleret ventil", PipelineElementType.PræisoleretVentil },
                { "Præventil med udluftning", PipelineElementType.PræventilMedUdluftning },
                { "Reduktion", PipelineElementType.Reduktion },
                { "Svanehals", PipelineElementType.Svanehals },
                { "Svejsetee", PipelineElementType.Svejsetee },
                { "Svejsning", PipelineElementType.Svejsning },
                { "Y-Model", PipelineElementType.Y_Model },
                { "$Buerør V{$Vinkel}° R{$R} L{$L}", PipelineElementType.Buerør },
                { "Stikafgrening", PipelineElementType.Stikafgrening },
                { "Muffetee", PipelineElementType.Muffetee },
                { "Preskobling tee", PipelineElementType.Muffetee },
                { "Materialeskift {#M1}{#DN1}x{#M2}{#DN2}", PipelineElementType.Materialeskift },
            };
        public enum PipelineElementType
        {
            Pipe,
            AfgreningMedSpring,
            AfgreningParallel,
            Afgreningsstuds,
            Endebund,
            Engangsventil,
            F_Model,
            Kedelrørsbøjning,
            LigeAfgrening,
            PreskoblingTee,
            PræisoleretBøjning90gr,
            PræisoleretBøjning45gr,
            PræisoleretBøjningVariabel,
            PræisoleretVentil,
            PræventilMedUdluftning,
            Reduktion,
            Svanehals,
            Svejsetee,
            Svejsning,
            Y_Model,
            Buerør,
            Stikafgrening,
            Muffetee,
            Materialeskift,
        }
        #endregion
        public static class DebugHelper
        {
            public static void CreateDebugLine(Point3d end, Color color)
            {
                CreateDebugLine(Point3d.Origin, end, color);
            }
            public static void CreateDebugLine(Point3d start, Point3d end, Color color)
            {
                CreateDebugLine(start, end, color, "");
            }
            public static void CreateDebugLine(Point3d start, Point3d end, Color color, string layer)
            {
                Database db = Application.DocumentManager.MdiActiveDocument.Database;
                Line line = new Line(start, end);
                line.Color = color;
                if (layer.IsNotNoE()) line.Layer = layer;
                line.AddEntityToDbModelSpace(db);
            }

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
        /// <summary>
        /// Special version to read version specifik data for dynamic block DH components.
        /// </summary>
        /// <param name="key">The key to select row by.</param>
        /// <param name="table">The datatable to read.</param>
        /// <param name="parameter">Column to read value from at the specified row by key.</param>
        /// <param name="keyColumnIdx">Usually 0, but can be any column.</param>
        /// <param name="version">A special column introduced to allow versioning of DH components.</param>
        public static string ReadStringParameterFromDataTable(string key, System.Data.DataTable table, string parameter, int keyColumnIdx = 0, string version = "")
        {
            if (table.AsEnumerable().Any(row => row.Field<string>(keyColumnIdx) == key))
            {
                var query = table.AsEnumerable()
                    .Where(x =>
                    x.Field<string>(keyColumnIdx) == key &&
                    x.Field<string>("Version") == version)
                    .Select(x => x.Field<string>(parameter));

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
        public static bool Equalz(this Point3d a, Point3d b, double tol = 0.01) =>
            null != a && null != b && a.X.Equalz(b.X, tol) && a.Y.Equalz(b.Y, tol) && a.Z.Equalz(b.Z, tol);
        public static bool Equalz(this PolylineVertex3d a, PolylineVertex3d b, double tol = 0.01) =>
            null != a && null != b && Equalz(a.Position, b.Position, tol);
        public static bool IsEqualTo(this PolylineVertex3d a, PolylineVertex3d b, Tolerance tol) =>
            null != a && null != b && a.Position.IsEqualTo(b.Position, tol);
        public static bool HorizontalEqualz(this Point3d a, Point3d b, double tol = 0.01) =>
            null != a && null != b && a.X.Equalz(b.X, tol) && a.Y.Equalz(b.Y, tol);
        public static bool HorizontalEqualz(this PolylineVertex3d a, PolylineVertex3d b, double tol = 0.01) =>
            null != a && null != b && HorizontalEqualz(a.Position, b.Position, tol);
        public static void CheckOrOpenForWrite(this DBObject dbObject)
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
        public static void CheckOrOpenForRead(this DBObject dbObject,
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
        /// <summary>
        /// Finds the index of vertice coincident with given point3d.
        /// If not coincident with any returns -1.
        /// </summary>
        public static int GetCoincidentIndexAtPoint(this Polyline pline, Point3d p3d)
        {
            #region Test to see if point coincides with a vertice
            bool verticeFound = false;
            int idx = -1;
            for (int i = 0; i < pline.NumberOfVertices; i++)
            {
                idx = i;
                Point3d vert = pline.GetPoint3dAt(i);
                if (vert.IsEqualTo(p3d, Tolerance.Global))
                    verticeFound = true;
                if (verticeFound) break;
            }

            if (!verticeFound) return -1;
            else return idx;
            #endregion
        }
        public static double DistanceHorizontalTo(this Point3d sourceP3d, Point3d targetP3d)
        {
            double X1 = sourceP3d.X;
            double Y1 = sourceP3d.Y;
            double X2 = targetP3d.X;
            double Y2 = targetP3d.Y;
            return Math.Sqrt(Math.Pow((X2 - X1), 2) + Math.Pow((Y2 - Y1), 2));
        }
        public static double DistanceHorizontalTo(this PolylineVertex3d v1, PolylineVertex3d v2) =>
            v1.Position.DistanceHorizontalTo(v2.Position);
        public static double Pow(this double value, double exponent)
        {
            return Math.Pow(value, exponent);
        }
        public static double TruncateToDecimalPlaces(double value, int decimalPlaces)
        {
            double factor = Math.Pow(10, decimalPlaces);
            return Math.Truncate(value * factor) / factor;
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
        public static double GetBulge(this ProfileParabolaSymmetric profileParabolaSymmetric, ProfileView profileView)
        {
            Point2d startPoint = profileView.GetPoint2dAtStaAndEl(
                profileParabolaSymmetric.StartStation, profileParabolaSymmetric.StartElevation);
            Point2d endPoint = profileView.GetPoint2dAtStaAndEl(
                profileParabolaSymmetric.EndStation, profileParabolaSymmetric.EndElevation);
            //Calculate bugle
            //Assuming that ProfileParabolaSymmetric can return a radius
            double r = profileParabolaSymmetric.Radius;
            double u = startPoint.GetDistanceTo(endPoint);
            double b = (2 * (r - Math.Sqrt(r.Pow(2) - u.Pow(2) / 4))) / u;
            if (profileParabolaSymmetric.CurveType == VerticalCurveType.Crest) b *= -1;
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
                case ProfileParabolaSymmetric parSym:
                    return parSym.GetBulge(profileView);
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
            catch (System.Exception ex)
            {
                //prdDbg(ex);
                return 0;
            }

            switch (next)
            {
                case ProfileTangent tan:
                    return 0;
                case ProfileCircular circular:
                    return circular.GetBulge(profileView);
                case ProfileParabolaSymmetric parabolaSymmetric:
                    return parabolaSymmetric.GetBulge(profileView);
                default:
                    prdDbg("Segment type: " + next.ToString() + ". Lav om til circular!");
                    throw new System.Exception($"LookAheadAndGetBulge: ProfileEntity unknown type encountered!");
            }
        }
        public static bool Contains(this string source, string toCheck, StringComparison comp)
        {
            return source?.IndexOf(toCheck, comp) >= 0;
        }
        public static void ExplodeToOwnerSpace2(this BlockReference br)
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
            BlockReference br, BlockTableRecord space)
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
        public static bool CheckIfBlockIsLatestVersion(this BlockReference br)
        {
            System.Data.DataTable dt = CsvData.FK;
            Database Db = br.Database;
            if (Db.TransactionManager.TopTransaction == null)
                throw new System.Exception("CheckIfBlockLatestVersion called outside transaction!");
            Transaction tx = Db.TransactionManager.TopTransaction;

            var btr = br.BlockTableRecord.Go<BlockTableRecord>(tx);

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
            var query = dt.AsEnumerable()
                    .Where(x => x["Navn"].ToString() == br.RealName())
                    .Select(x => x["Version"].ToString())
                    .Select(x => { if (x == "") return "1"; else return x; })
                    .Select(x => Convert.ToInt32(x.Replace("v", "")))
                    .OrderBy(x => x);

            if (query.Count() == 0)
                throw new System.Exception($"Block {br.RealName()} is not present in FJV Dynamiske Komponenter.csv!");
            int maxVersion = query.Max();
            #endregion

            if (maxVersion != blockVersion) return false;
            else return true;
        }
        /// <summary>
        /// Requires active transaction!
        /// </summary>
        public static void CheckOrImportBlockRecord(this Database db, string pathToLibrary, string blockName)
        {
            bool localTransaction = false;
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
        public static BlockTableRecord GetBlockTableRecordByName(this Database db, string blockName)
        {
            BlockTable bt = db.BlockTableId.Go<BlockTable>(db.TransactionManager.TopTransaction);
            if (bt.Has(blockName))
                return bt[blockName].Go<BlockTableRecord>(db.TransactionManager.TopTransaction);
            else return null;
        }
        /// <summary>
        /// Remember to check for existence of BlockTableRecord!
        /// </summary>
        public static BlockReference CreateBlockWithAttributes(
            this Database db, string blockName, Point3d position, double rotation = 0)
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
        public static void AttSync(this BlockReference br)
        {
            Transaction tx = br.Database.TransactionManager.TopTransaction;
            BlockTableRecord btr;
            if (br.IsDynamicBlock && br.AnonymousBlockTableRecord != Oid.Null) btr =
                    br.AnonymousBlockTableRecord.Go<BlockTableRecord>(tx);
            else btr = br.BlockTableRecord.Go<BlockTableRecord>(tx);

            Dictionary<string, AttributeDefinition> atDefDict
                = new Dictionary<string, AttributeDefinition>();
            foreach (Oid id in btr)
            {
                if (id.IsDerivedFrom<AttributeDefinition>())
                {
                    AttributeDefinition def = id.Go<AttributeDefinition>(tx);
                    atDefDict.Add(def.Tag, def);
                }
            }

            AttributeCollection ac = br.AttributeCollection;
            foreach (Oid atId in ac)
            {
                AttributeReference ar = atId.Go<AttributeReference>(tx);
                if (!ar.IsConstant && atDefDict.ContainsKey(ar.Tag))
                {
                    ar.CheckOrOpenForWrite();
                    ar.SetAttributeFromBlock(atDefDict[ar.Tag], br.BlockTransform);
                }
            }
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
        public static bool SampleElevation(this Profile profile, double station, ref double sampledElevation)
        {
            try { sampledElevation = profile.ElevationAt(station); }
            catch (System.Exception)
            {
                prdDbg($"Station {station} threw an exception when sampling {profile.Name}!");
                return false;
            }
            return true;
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
        public static BlockReference[] GetNestedBlocksByName(this BlockTableRecord btr, string blockName)
        {
            List<BlockReference> btrs = new List<BlockReference>();
            foreach (Oid oid in btr)
            {
                if (!oid.IsDerivedFrom<BlockReference>()) continue;
                BlockReference nestedBr = oid.Go<BlockReference>(
                    btr.Database.TransactionManager.TopTransaction);
                if (nestedBr.RealName() == blockName)
                    btrs.Add(nestedBr);
            }

            return btrs.ToArray();
        }
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
                br.ResetAttributesLocation(attDefs, tr);
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
                        br.ResetAttributesLocation(attDefs, tr);
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
        private static void ResetAttributesLocation(
            this BlockReference br, List<AttributeDefinition> attDefs, Transaction tr)
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
        public static void ResetAttributesValues(this BlockTableRecord target)
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
                br.ResetAttributesToDefaultValues(attDefs, tr);
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
                        br.ResetAttributesToDefaultValues(attDefs, tr);
                    }
                }
            }
        }
        private static void ResetAttributesToDefaultValues(
            this BlockReference br, List<AttributeDefinition> attDefs, Transaction tr)
        {
            string tag = "";
            var query = attDefs.Where(x => x.Tag == tag);
            foreach (ObjectId id in br.AttributeCollection)
            {
                if (!id.IsErased)
                {
                    AttributeReference attRef = (AttributeReference)tr.GetObject(id, OpenMode.ForWrite);
                    tag = attRef.Tag;

                    var attDef = query.FirstOrDefault();
                    if (attDef == default) continue;

                    attRef.SetAttributeFromBlock(attDef, br.BlockTransform);
                    //if (attDef.HasFields) attRef.TextString = attDef.getTextWithFieldCodes();
                    //attRef.UpdateMTextAttribute();

                    //prdDbg(tag);
                    //prdDbg(attDef.getTextWithFieldCodes());
                    //prdDbg(attDef.TextString);
                    //prdDbg(attRef.TextString);

                    //if (attRef.IsMTextAttribute) attValues.Add(attRef.Tag, attRef.MTextAttribute.HasFields ? attRef.MTextAttribute.getMTextWithFieldCodes() : attRef.MTextAttribute.Contents);
                    //else attValues.Add(attRef.Tag, attRef.HasFields ? attRef.getTextWithFieldCodes() : attRef.TextString);
                    //attRef.Erase();
                }
            }
            //foreach (AttributeDefinition attDef in attDefs)
            //{
            //    AttributeReference attRef = new AttributeReference();
            //    attRef.SetAttributeFromBlock(attDef, br.BlockTransform);
            //    if (attDef.Constant)
            //    {
            //        string textString;
            //        if (attRef.IsMTextAttribute) textString = attRef.MTextAttribute.HasFields ? attRef.MTextAttribute.getMTextWithFieldCodes() : attRef.MTextAttribute.Contents;
            //        else textString = attRef.HasFields ? attRef.getTextWithFieldCodes() : attRef.TextString;
            //        attRef.TextString = textString;
            //        //attRef.TextString = attDef.IsMTextAttributeDefinition ?
            //        //    attDef.MTextAttributeDefinition.Contents :
            //        //    attDef.TextString;
            //    }
            //    else if (attValues.ContainsKey(attRef.Tag))
            //    {
            //        attRef.TextString = attValues[attRef.Tag];
            //    }
            //    br.AttributeCollection.AppendAttribute(attRef);
            //    tr.AddNewlyCreatedDBObject(attRef, true);
            //}
        }

        private static string ConstructStringByRegex(BlockReference br, string stringToProcess)
        {
            //Construct pattern which matches the parameter definition
            Regex variablePattern = new Regex(@"{\$(?<Parameter>[a-zæøåA-ZÆØÅ0-9_:-]*)}");

            //Test if a pattern matches in the input string
            if (variablePattern.IsMatch(stringToProcess))
            {
                //Get the first match
                Match match = variablePattern.Match(stringToProcess);
                //Get the first capture
                string capture = match.Captures[0].Value;
                //Get the parameter name from the regex match
                string parameterName = match.Groups["Parameter"].Value;
                //Read the parameter value from BR
                string parameterValue = br.ReadDynamicPropertyValue(parameterName);
                //Replace the captured group in original string with the parameter value
                stringToProcess = stringToProcess.Replace(capture, parameterValue);
                //Recursively call current function
                //It runs on the string until no more captures remain
                //Then it returns
                stringToProcess = ConstructStringByRegex(br, stringToProcess);
            }

            return stringToProcess;
        }
        public static string ReadDynamicPropertyValue(this BlockReference br, string propertyName)
        {
            DynamicBlockReferencePropertyCollection props = br.DynamicBlockReferencePropertyCollection;
            foreach (DynamicBlockReferenceProperty property in props)
            {
                //prdDbg($"Name: {property.PropertyName}, Units: {property.UnitsType}, Value: {property.Value}");
                if (property.PropertyName == propertyName)
                {
                    switch (property.UnitsType)
                    {
                        case DynamicBlockReferencePropertyUnitsType.NoUnits:
                            return property.Value.ToString();
                        case DynamicBlockReferencePropertyUnitsType.Angular:
                            double angular = Convert.ToDouble(property.Value);
                            return angular.ToDegrees().ToString("0.##");
                        case DynamicBlockReferencePropertyUnitsType.Distance:
                            double distance = Convert.ToDouble(property.Value);
                            return distance.ToString("0.##");
                        case DynamicBlockReferencePropertyUnitsType.Area:
                            double area = Convert.ToDouble(property.Value);
                            return area.ToString("0.00");
                        default:
                            return "";
                    }
                }
            }
            return "";
        }
        public static bool IsPointInsideXY(this Extents3d extents, Point2d pnt)
        => pnt.X >= extents.MinPoint.X && pnt.X <= extents.MaxPoint.X
            && pnt.Y >= extents.MinPoint.Y && pnt.Y <= extents.MaxPoint.Y;
        public static bool IsPointInsideXY(this Extents3d extents, Point3d pnt)
        => pnt.X >= extents.MinPoint.X && pnt.X <= extents.MaxPoint.X
            && pnt.Y >= extents.MinPoint.Y && pnt.Y <= extents.MaxPoint.Y;
        public static bool IsExtentsInsideXY(this Extents3d original, Extents3d other)
        {
            // Check if the other Extents3d is inside the original Extents3d
            bool insideX = original.MinPoint.X <= other.MinPoint.X && original.MaxPoint.X >= other.MaxPoint.X;
            bool insideY = original.MinPoint.Y <= other.MinPoint.Y && original.MaxPoint.Y >= other.MaxPoint.Y;

            return insideX && insideY;
        }
        public static bool Intersects(this Extents3d original, Extents3d other)
        {
            if ((original.MaxPoint.X != other.MinPoint.X && original.MinPoint.X != other.MaxPoint.X) &&
                (original.MaxPoint.X <= other.MinPoint.X || original.MinPoint.X >= other.MaxPoint.X))
                return false;

            if ((original.MaxPoint.Y != other.MinPoint.Y && original.MinPoint.Y != other.MaxPoint.Y) &&
                (original.MaxPoint.Y <= other.MinPoint.Y || original.MinPoint.Y >= other.MaxPoint.Y))
                return false;

            if ((original.MaxPoint.Z != other.MinPoint.Z && original.MinPoint.Z != other.MaxPoint.Z) &&
                (original.MaxPoint.Z <= other.MinPoint.Z || original.MinPoint.Z >= other.MaxPoint.Z))
                return false;

            // If none of the above conditions are met, then the boxes intersect in 2D space
            return true;
        }
        public static bool Intersects2D(this Extents3d original, Extents3d other)
        {
            // Check if one box is to the left or right of the other
            if (original.MaxPoint.X <= other.MinPoint.X || original.MinPoint.X >= other.MaxPoint.X)
                return false;

            // Check if one box is above or below the other
            if (original.MaxPoint.Y <= other.MinPoint.Y || original.MinPoint.Y >= other.MaxPoint.Y)
                return false;

            // If none of the above conditions are met, then the boxes intersect in 2D space
            return true;
        }
        public static Oid DrawExtents(this Extents3d extents, Database db)
        {
            if (extents == null) return Oid.Null;
            Polyline pline = new Polyline(4);
            List<Point2d> point2Ds = new List<Point2d>
            {
                new Point2d(extents.MinPoint.X, extents.MinPoint.Y),
                new Point2d(extents.MinPoint.X, extents.MaxPoint.Y),
                new Point2d(extents.MaxPoint.X, extents.MaxPoint.Y),
                new Point2d(extents.MaxPoint.X, extents.MinPoint.Y)
            };
            foreach (Point2d p2d in point2Ds)
            {
                pline.AddVertexAt(pline.NumberOfVertices, p2d, 0, 0, 0);
            }
            pline.Closed = true;
            using (Transaction tx = db.TransactionManager.StartTransaction())
            {
                Oid id = pline.AddEntityToDbModelSpace(db);
                tx.Commit();
                return id;
            }
        }
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
        public static Extents3d GetBufferedXYGeometricExtents(
            this Entity entity, double buffer)
        {
            if (buffer == 0) return entity.GeometricExtents;
            var bbox = entity.GeometricExtents;

            //Apply buffer to the bbox and create new extents
            var newMin = new Point3d(
                bbox.MinPoint.X - buffer, bbox.MinPoint.Y - buffer, 0);
            var newMax = new Point3d(
                bbox.MaxPoint.X + buffer, bbox.MaxPoint.Y + buffer, 0);
            return new Extents3d(newMin, newMax);
        }
        public static double ConstantWidthSafe(this Polyline pline)
        {
            double plineWidth;
            try
            {
                plineWidth = pline.ConstantWidth;
            }
            catch (System.Exception)
            {
                plineWidth = 0.0;
            }
            return plineWidth;
        }
        /// <summary>
        /// Uses backward lookup, index is the forward segment compared with index - 1 backward segment.
        /// </summary>
        public static (Vector3d dir1, Vector3d dir2) DirectionsAt(this Polyline pline, int index)
        {
            int numberOfVertices = pline.NumberOfVertices;
            if (index == 0 || index == numberOfVertices - 1) return default;
            if (numberOfVertices < 3) return default;

            SegmentType st1 = pline.GetSegmentType(index - 1);
            SegmentType st2 = pline.GetSegmentType(index);

            Vector3d dir1;
            Vector3d dir2;

            if (st1 == SegmentType.Line) dir1 = pline.GetLineSegmentAt(index - 1).Direction;
            else if (st1 == SegmentType.Arc)
            {
                CircularArc3d ca3d = pline.GetArcSegmentAt(index - 1);
                dir1 = ca3d.GetTangent(ca3d.EndPoint).Direction;
            }
            else dir1 = default;

            if (st2 == SegmentType.Line) dir2 = pline.GetLineSegmentAt(index).Direction;
            else if (st2 == SegmentType.Arc)
            {
                CircularArc3d ca3d = pline.GetArcSegmentAt(index);
                dir2 = ca3d.GetTangent(ca3d.StartPoint).Direction;
            }
            else dir2 = default;

            //Detect if vectors are opposite directions
            if (dir1.DotProduct(dir2) < 0) dir2 = -dir2;

            return (dir1, dir2);
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
        public static Point3d To3d(this Point2d p2d, double Z = 0.0) => new Point3d(p2d.X, p2d.Y, Z);
        /// <summary>
        /// 2D key for use in dictionaries for faster points lookup.
        /// </summary>
        /// <param name="p3d">The point to index.</param>
        /// <param name="precision">Precision to which truncate the double. Default 1000.0 gives millimeter precision.</param>
        public static (long, long) Get2DKey(this Point3d p3d, double precision = 1000.0) =>
            ((long)(p3d.X * precision), (long)(p3d.Y * precision));
        public static Point2d To2d(this Point3d p3d) => new Point2d(p3d.X, p3d.Y);
        /// <summary>
        /// Order of returned coordinates explained here:
        /// https://macwright.com/lonlat/
        /// GeoJson is lon, lat.
        /// </summary>
        /// <param name="latlon">If false, reverses the returned array to lon, lat.</param>
        public static double[] ToWGS84FromUtm32N(this Point3d p, bool latlon = true)
        {
            double easting = p.X; double northing = p.Y; string zone = "32N";

            int ZoneNumber = int.Parse(zone.Substring(0, zone.Length - 1));
            //char ZoneLetter = zone[zone.Length - 1];

            double a = 6378137; // WGS-84 ellipsiod parameters
            double eccSquared = 0.00669438;
            double eccPrimeSquared;
            double e1 = (1 - Math.Sqrt(1 - eccSquared)) / (1 + Math.Sqrt(1 - eccSquared));

            double N1, T1, C1, R1, D, M;
            double LongOrigin;
            double mu, phi1Rad;

            double x = easting - 500000.0; // remove 500,000 meter offset for longitude
            double y = northing;

            LongOrigin = (ZoneNumber - 1) * 6 - 180 + 3;  //+3 puts origin in middle of zone

            eccPrimeSquared = (eccSquared) / (1 - eccSquared);

            M = y / 0.9996;
            mu = M / (a * (1 - eccSquared / 4 - 3 * eccSquared * eccSquared / 64 - 5 * eccSquared * eccSquared * eccSquared / 256));

            phi1Rad = mu + (3 * e1 / 2 - 27 * Math.Pow(e1, 3) / 32) * Math.Sin(2 * mu) +
                     (21 * e1 * e1 / 16 - 55 * Math.Pow(e1, 4) / 32) * Math.Sin(4 * mu) +
                     (151 * Math.Pow(e1, 3) / 96) * Math.Sin(6 * mu);

            N1 = a / Math.Sqrt(1 - eccSquared * Math.Sin(phi1Rad) * Math.Sin(phi1Rad));
            T1 = Math.Tan(phi1Rad) * Math.Tan(phi1Rad);
            C1 = eccPrimeSquared * Math.Cos(phi1Rad) * Math.Cos(phi1Rad);
            R1 = a * (1 - eccSquared) / Math.Pow(1 - eccSquared * Math.Sin(phi1Rad) * Math.Sin(phi1Rad), 1.5);
            D = x / (N1 * 0.9996);

            double lat = phi1Rad - (N1 * Math.Tan(phi1Rad) / R1) * (D * D / 2 - (5 + 3 * T1 + 10 * C1 - 4 * C1 * C1 - 9 * eccPrimeSquared) * Math.Pow(D, 4) / 24 +
                                                           (61 + 90 * T1 + 298 * C1 + 45 * T1 * T1 - 252 * eccPrimeSquared - 3 * C1 * C1) * Math.Pow(D, 6) / 720);
            lat = lat * 180.0 / Math.PI;

            double lon = (D - (1 + 2 * T1 + C1) * Math.Pow(D, 3) / 6 + (5 - 2 * C1 + 28 * T1 - 3 * C1 * C1 + 8 * eccPrimeSquared + 24 * T1 * T1) * Math.Pow(D, 5) / 120) / Math.Cos(phi1Rad);
            lon = LongOrigin + (lon * 180.0 / Math.PI);

            if (latlon) return new double[] { lat, lon };
            else return new double[] { lon, lat };
        }
        /// <summary>
        /// Order of returned coordinates explained here:
        /// https://macwright.com/lonlat/
        /// GeoJson is lon, lat.
        /// </summary>
        /// <param name="latlon">If false, reverses the returned array to lon, lat.</param
        public static double[] ToWGS84FromUtm32N(this Point2d p, bool latlon = true) =>
            ToWGS84FromUtm32N(p.To3d(), latlon);
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
        public static double GetLengthOfSegmentAt(this Polyline pl, int index)
        {
            int nVerts = pl.NumberOfVertices;
            if (index >= nVerts || index < 0) return 0.0;

            SegmentType sType = pl.GetSegmentType(index);

            switch (sType)
            {
                case SegmentType.Line:
                    var line = pl.GetLineSegment2dAt(index);
                    return line.Length;
                case SegmentType.Arc:
                    var arc = pl.GetArcSegment2dAt(index);
                    return
                        arc.GetLength(
                            arc.GetParameterOf(arc.StartPoint),
                            arc.GetParameterOf(arc.EndPoint));
                case SegmentType.Coincident:
                case SegmentType.Point:
                case SegmentType.Empty:
                default:
                    return 0.0;
            }
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
        public static HashSet<Point3d> GetAllEndPoints(this Entity ent)
        {
            switch (ent)
            {
                case Polyline pl:
                    return new HashSet<Point3d> { pl.StartPoint, pl.EndPoint };
                case BlockReference br:
                    return br.GetAllEndPoints();
                default:
                    throw new System.Exception($"Entity is not a Polyline or BlockReference! {ent.GetType()}");
            }
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
        #region SPECIAL ORDERING METHOD DIMIMPORTDIMS
        private static Dictionary<string, int> orderDefinition = new Dictionary<string, int>()
        {
            //ADD NEW STRINGS HERE!!!
            {"DN", 1 },
            {"PRTFLEXL", 0},
        };
        public static IOrderedEnumerable<T> OrderBySpecial<T>(this IEnumerable<T> source, Func<T, string> selector)
        {
            int maxNumberLength = source
                .SelectMany(i => Regex.Matches(selector(i), @"\d+").Cast<Match>().Select(m => (int?)m.Value.Length))
        .Max() ?? 0;

            return source.OrderBy(i =>
            {
                var key = selector(i);
                // Identify the prefix and numeric parts of the key
                var match = System.Text.RegularExpressions.Regex.Match(key, @"^([A-Za-z]+)(\d*)");
                var prefix = match.Groups[1].Value;
                var numericPart = match.Groups[2].Value;

                // Determine the sort order based on the prefix
                int prefixOrder = orderDefinition.ContainsKey(prefix) ? orderDefinition[prefix] : orderDefinition.Values.Max() + 1;

                // Pad the numeric part for proper alphanumeric sorting
                var paddedNumericPart = numericPart.PadLeft(maxNumberLength, '0');

                return $"{prefixOrder:D3}-{prefix}-{paddedNumericPart}";
            });
        }
        #endregion
        public static double StationAtPoint(this Alignment al, Point3d p)
        {
            double station = 0.0;
            double offset = 0.0;
            Point3d cP = default;

            try
            {
                Polyline pline = al.GetPolyline().Go<Polyline>(
                    al.Database.TransactionManager.TopTransaction);
                //cP = al.GetClosestPointTo(p, false);
                cP = pline.GetClosestPointTo(p, false);
                al.StationOffset(cP.X, cP.Y, ref station, ref offset);
                pline.CheckOrOpenForWrite();
                pline.Erase();
            }
            catch (System.Exception ex)
            {
                prdDbg($"Alignment {al.Name} threw an exception when sampling station at point:\n" +
                    $"Entity position: {p}\n" +
                    $"Sampled position: {cP}");
                prdDbg(ex);
                throw;
            }

            return station;
        }
        public static double StationAtPoint(this Alignment al, BlockReference br)
            => StationAtPoint(al, br.Position);
        public static bool IsConnectedTo(this Alignment itself, Alignment other, double tolerance)
        {
            // Get the start and end points of the alignments
            Point3d thisStart = itself.StartPoint;
            Point3d thisEnd = itself.EndPoint;
            Point3d otherStart = other.StartPoint;
            Point3d otherEnd = other.EndPoint;

            // Check if any of the endpoints of this alignment are on the other alignment
            if (IsOn(other, thisStart, tolerance) || IsOn(other, thisEnd, tolerance))
                return true;

            // Check if any of the endpoints of the other alignment are on this alignment
            if (IsOn(itself, otherStart, tolerance) || IsOn(itself, otherEnd, tolerance))
                return true;

            // If none of the checks passed, the alignments are not connected
            return false;

            bool IsOn(Alignment al, Point3d point, double tol)
            {
                //double station = 0;
                //double offset = 0;

                //try
                //{
                //    alignment.StationOffset(point.X, point.Y, tolerance, ref station, ref offset);
                //}
                //catch (Exception) { return false; }

                Polyline pline = al.GetPolyline().Go<Polyline>(
                    al.Database.TransactionManager.TopTransaction);

                Point3d p = pline.GetClosestPointTo(point, false);
                pline.UpgradeOpen();
                pline.Erase(true);
                //prdDbg($"{offset}, {Math.Abs(offset)} < {tolerance}, {Math.Abs(offset) <= tolerance}, {station}");

                //Debug.CreateDebugLine(p, ColorByName("yellow"));
                //Debug.CreateDebugLine(point, ColorByName("red"));

                // If the offset is within the tolerance, the point is on the alignment
                if (Math.Abs(p.DistanceTo(point)) <= tol) return true;

                // Otherwise, the point is not on the alignment
                return false;
            }
        }
        public static Polyline GetPolyline(this Alignment al)
        {
            Oid id = al.GetPolyline();
            return id.Go<Polyline>(al.Database.TransactionManager.TopTransaction);
        }
        public static bool IsOn(this PolylineVertex3d vert, Polyline3d pl3d, double tol)
        {
            var dist = vert.Position.DistanceTo(pl3d.GetClosestPointTo(vert.Position, false));
            return dist <= tol;
        }
        public static void UpdateElevationZ(this PolylineVertex3d vert, double newElevation)
        {
            if (!vert.Position.Z.Equalz(newElevation, Tolerance.Global.EqualPoint))
            {
                vert.CheckOrOpenForWrite();
                vert.Position = new Point3d(vert.Position.X, vert.Position.Y, newElevation);
            }
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
        public static Vector3d To3D(this Vector2d vec) => new Vector3d(vec.X, vec.Y, 0.0);

        
        public static IEnumerable<T> Entities<T>(this ObjectIdCollection col, Transaction tx) where T : DBObject
        {
            foreach (Oid oid in col) yield return oid.Go<T>(tx);
        }
        public static HashSet<Oid> ToHashSet(this ObjectIdCollection col)
        {
            HashSet<Oid> ids = new HashSet<Oid>();
            foreach (Oid item in col)
            {
                ids.Add(item);
            }
            return ids;
        }
        public static List<string> ToList(this StringCollection sc)
        {
            List<string> list = new List<string>();
            foreach (string s in sc) list.Add(s);
            return list;
        }
        public static HashSet<string> ToHashSet(this StringCollection sc)
        {
            HashSet<string> list = new HashSet<string>();
            foreach (string s in sc) list.Add(s);
            return list;
        }
        public static bool IsOverlapping(this Extents2d ext1, Extents2d ext2)
        {
            //https://stackoverflow.com/questions/20925818/algorithm-to-check-if-two-boxes-overlap

            return ProjectionOverlaps(ext1.MinPoint.X, ext1.MaxPoint.X, ext2.MinPoint.X, ext2.MaxPoint.X) &&
                ProjectionOverlaps(ext1.MinPoint.Y, ext1.MaxPoint.Y, ext2.MinPoint.Y, ext2.MaxPoint.Y);

            bool ProjectionOverlaps(double cmin1, double cmax1, double cmin2, double cmax2)
                => cmax1 >= cmin2 && cmax2 >= cmin1;
        }
        public static HashSet<BlockReference> GetDetailingBlocks(
            this ProfileView pv, Database db, double buffer = 0)
        {
            Transaction tx = db.TransactionManager.TopTransaction;
            HashSet<BlockReference> brs =
                db.HashSetOfType<BlockReference>(tx);

            HashSet<BlockReference> detailBlocks =
                new HashSet<BlockReference>();

            Extents3d bbox = pv.GetBufferedXYGeometricExtents(buffer);
            foreach (var item in brs)
            {
                if (!bbox.IsPointInsideXY(item.Position)) continue;
                detailBlocks.Add(item);
            }
            return detailBlocks;
        }
        public static List<Point2d> SortAndEnsureCounterclockwiseOrder(this List<Point2d> points)
        {
            if (points.Count < 3)
            {
                throw new ArgumentException("A polygon must have at least 3 points.");
            }

            // Find the point with the lowest Y-coordinate (leftmost in case of a tie)
            Point2d referencePoint = points.Aggregate((p1, p2) =>
            {
                double tolerance = 1e-12;
                bool yEqual = Math.Abs(p1.Y - p2.Y) < tolerance;
                bool xLess = p1.X < p2.X;

                return (p1.Y < p2.Y || (yEqual && xLess)) ? p1 : p2;
            });

            // Sort the points by their polar angle with respect to the reference point
            points.Sort((p1, p2) => Math.Atan2(
                p1.Y - referencePoint.Y, p1.X - referencePoint.X).CompareTo(
                Math.Atan2(p2.Y - referencePoint.Y, p2.X - referencePoint.X)));

            List<Point2d> sortedPoints =
                points.OrderBy(p => Math.Atan2(p.Y - referencePoint.Y, p.X - referencePoint.X)).ToList();
            //Add the first point at the end of the list to comply with the RFC 7946
            sortedPoints.Add(sortedPoints[0]);
            return sortedPoints;
        }
        public static List<Point2d> SortAndEnsureCounterclockwiseOrder(this HashSet<Point2d> points) =>
            SortAndEnsureCounterclockwiseOrder(points.ToList());
        public static List<Point3d> SortAndEnsureCounterclockwiseOrder(this List<Point3d> points)
        {
            if (points.Count < 3)
            {
                throw new ArgumentException("A polygon must have at least 3 points.");
            }

            // Find the point with the lowest Y-coordinate (leftmost in case of a tie)
            Point3d referencePoint = points.Aggregate((p1, p2) =>
            {
                double tolerance = 1e-12;
                bool yEqual = Math.Abs(p1.Y - p2.Y) < tolerance;
                bool xLess = p1.X < p2.X;

                return (p1.Y < p2.Y || (yEqual && xLess)) ? p1 : p2;
            });

            // Sort the points by their polar angle with respect to the reference point
            points.Sort((p1, p2) => Math.Atan2(
                p1.Y - referencePoint.Y, p1.X - referencePoint.X).CompareTo(
                Math.Atan2(p2.Y - referencePoint.Y, p2.X - referencePoint.X)));

            List<Point3d> sortedPoints =
                points.OrderBy(p => Math.Atan2(p.Y - referencePoint.Y, p.X - referencePoint.X)).ToList();
            //Add the first point at the end of the list to comply with the RFC 7946
            sortedPoints.Add(sortedPoints[0]);
            return sortedPoints;
        }
        public static HashSet<BulgeVertex> ToHashSet(this BulgeVertexCollection col)
        {
            HashSet<BulgeVertex> set = new HashSet<BulgeVertex>();
            foreach (BulgeVertex item in col) set.Add(item);
            return set;
        }
        public static List<BulgeVertex> ToDistinctList(this BulgeVertexCollection col)
        {
            HashSet<BulgeVertex> set = new HashSet<BulgeVertex>(
                new BulgeVertexEqualityComparer());
            foreach (BulgeVertex item in col) set.Add(item);
            return set.ToList();
        }
        public static List<BulgeVertex> ToList(this BulgeVertexCollection col)
        {
            List<BulgeVertex> set = new List<BulgeVertex>();
            foreach (BulgeVertex item in col) set.Add(item);
            return set.ToList();
        }
        public static List<Point2d> GetSamplePoints(this List<BulgeVertex> bulgeVertices, double radianStep = 0.25)
        {
            HashSet<Point2d> samplePoints =
                new HashSet<Point2d>(
                    new Point2dEqualityComparer());

            for (int i = 0; i < bulgeVertices.Count - 1; i++)
            {
                BulgeVertex start = bulgeVertices[i];
                BulgeVertex end = bulgeVertices[i + 1];

                // Add start vertex to the list
                samplePoints.Add(start.Vertex);

                if (Math.Abs(start.Bulge) > 1e-6) // Arc segment
                {
                    CircularArc2d ca2d = new CircularArc2d(start.Vertex, end.Vertex, start.Bulge, false);

                    double sPar = ca2d.GetParameterOf(ca2d.StartPoint);
                    double ePar = ca2d.GetParameterOf(ca2d.EndPoint);
                    double length = ca2d.GetLength(sPar, ePar);
                    double radians = length / ca2d.Radius;
                    int nrOfSamples = (int)(radians / radianStep);
                    if (nrOfSamples < 3)
                    {
                        samplePoints.Add(ca2d.StartPoint);
                        samplePoints.Add(ca2d.GetSamplePoints(3)[1]);
                        samplePoints.Add(ca2d.EndPoint);
                    }
                    else
                    {
                        Point2d[] samples = ca2d.GetSamplePoints(nrOfSamples);
                        foreach (Point2d p2d in samples) samplePoints.Add(p2d);
                    }
                }
            }

            return samplePoints.SortAndEnsureCounterclockwiseOrder();
        }
        public static List<Point2d> GetSamplePoints(this Polyline polyline)
        {
            List<Point2d> points = new List<Point2d>();
            int numOfVert = polyline.NumberOfVertices - 1;
            if (polyline.Closed) numOfVert++;
            for (int i = 0; i < numOfVert; i++)
            {
                switch (polyline.GetSegmentType(i))
                {
                    case SegmentType.Line:
                        LineSegment2d ls = polyline.GetLineSegment2dAt(i);
                        if (i == 0)
                        {//First iteration
                            points.Add(ls.StartPoint);
                        }
                        points.Add(ls.EndPoint);
                        break;
                    case SegmentType.Arc:
                        CircularArc2d arc = polyline.GetArcSegment2dAt(i);
                        double sPar = arc.GetParameterOf(arc.StartPoint);
                        double ePar = arc.GetParameterOf(arc.EndPoint);
                        double length = arc.GetLength(sPar, ePar);
                        double radians = length / arc.Radius;
                        int nrOfSamples = (int)(radians / 0.1);
                        if (nrOfSamples < 3)
                        {
                            if (i == 0) points.Add(arc.StartPoint);
                            points.Add(arc.EndPoint);
                        }
                        else
                        {
                            Point2d[] samples = arc.GetSamplePoints(nrOfSamples);
                            if (i != 0) samples = samples.Skip(1).ToArray();
                            foreach (Point2d p2d in samples) points.Add(p2d);
                        }
                        break;
                    case SegmentType.Coincident:
                    case SegmentType.Point:
                    case SegmentType.Empty:
                    default:
                        throw new System.Exception(
                            $"Polyline {polyline.Handle} is not clean!\n" +
                            $"Run CLEANPLINES!");
                }
            }
            return points;
        }
        /// <summary>
        /// Remember that the grouped objects need to have Equals and GetHashCode implemented
        /// </summary>
        /// <returns>List of lists, hvere each list contains connected objects</returns>
        public static List<List<T>> GroupConnected<T>(this IEnumerable<T> itemsToGroup, Func<T, T, bool> predicateIsConnected)
        {
            var visited = new HashSet<T>();
            var groups = new List<List<T>>();

            foreach (var item in itemsToGroup)
            {
                if (!visited.Contains(item))
                {
                    var group = new List<T>();
                    Stack<T> stack = new Stack<T>();
                    stack.Push(item);

                    while (stack.Count > 0)
                    {
                        var current = stack.Pop();

                        if (!visited.Contains(current))
                        {
                            visited.Add(current);
                            group.Add(current);

                            foreach (var neighbor in itemsToGroup)
                            {
                                if (!visited.Contains(neighbor) && predicateIsConnected(current, neighbor))
                                {
                                    stack.Push(neighbor);
                                }
                            }
                        }
                    }

                    groups.Add(group);
                }
            }

            return groups;
        }
    }
    public static class ExtensionMethods
    {
        public static T Go<T>(this Oid oid, Transaction tx,
            Autodesk.AutoCAD.DatabaseServices.OpenMode openMode =
            Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead) where T : Autodesk.AutoCAD.DatabaseServices.DBObject
        {
            var obj = tx.GetObject(oid, openMode, false);
            return obj as T;
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
        public static IEnumerable<Oid> AcWhere<T>(
            this IEnumerable<Oid> source, Func<T, bool> predicate) where T : Entity
        {
            if (source == null) throw new System.Exception("source is null");
            if (predicate == null) throw new System.Exception("predicate is null");

            foreach (Oid oid in source)
            {
                T item = (T)oid.Open(OpenMode.ForRead);
                if (predicate(item)) yield return oid;
                item.Close();
                item.Dispose();
            }
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
        public static bool CheckOrCreateLayer(this Database db, string layerName, Color color, bool isPlottable = true)
        {
            return CheckOrCreateLayer(db, layerName, color.ColorIndex, isPlottable);
        }
        public static bool CheckOrCreateLayer(this Database db, string layerName, short colorIdx = -1, bool isPlottable = true)
        {
            bool newTx = false;
            if (db.TransactionManager.TopTransaction == null) newTx = true;

            Transaction txLag;
            if (newTx) txLag = db.TransactionManager.StartTransaction();
            else txLag = db.TransactionManager.TopTransaction;
            try
            {
                LayerTable lt = txLag.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;
                if (!lt.Has(layerName))
                {
                    LayerTableRecord ltr = new LayerTableRecord();
                    ltr.Name = layerName;
                    ltr.IsPlottable = isPlottable;
                    if (colorIdx != -1)
                    {
                        ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, colorIdx);
                    }

                    //Make layertable writable
                    lt.CheckOrOpenForWrite();

                    //Add the new layer to layer table
                    Oid ltId = lt.Add(ltr);
                    txLag.AddNewlyCreatedDBObject(ltr, true);
                    if (newTx)
                    {
                        txLag.Commit();
                        txLag.Dispose();
                    }
                    return true;
                }
                else
                {
                    if (colorIdx == -1) return true;
                    LayerTableRecord ltr = lt[layerName].Go<LayerTableRecord>(txLag, OpenMode.ForWrite);
                    if (ltr.Color.ColorIndex != colorIdx)
                        ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, colorIdx);
                    if (newTx)
                    {
                        txLag.Commit();
                        txLag.Dispose();
                    }
                    return true;
                }
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                txLag.Abort();
                if (newTx) txLag.Dispose();
                throw;
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
        public static IEnumerable<ObjectId> ToIEnumerable(this BlockTableRecord btr)
        {
            foreach (Oid oid in btr) yield return oid;
        }
        public static HashSet<Oid> HashSetOfFjvPipeIds(this Database db, bool discardFrozen = true)
        {
            if (db.TransactionManager.TopTransaction != null)
                throw new System.Exception(
                    "HashSetOfFjvPipeIds must be used outside of Transaction!");

            HashSet<Oid> result = new HashSet<Oid>();

            var bt = db.BlockTableId.Open(OpenMode.ForRead) as BlockTable;
            var ms = bt[BlockTableRecord.ModelSpace].Open(OpenMode.ForRead) as BlockTableRecord;
            var lt = db.LayerTableId.Open(OpenMode.ForRead) as LayerTable;

            foreach (Oid oid in ms)
            {
                if (oid.IsDerivedFrom<Polyline>())
                {
                    Polyline pline = oid.Open(OpenMode.ForRead) as Polyline;
                    var ltr = lt[pline.Layer].Open(OpenMode.ForRead) as LayerTableRecord;

                    if (!(ltr.IsFrozen && discardFrozen) &&
                        GetPipeSystem(pline) != PipeSystemEnum.Ukendt)
                        result.Add(oid);

                    ltr.Close();
                    ltr.Dispose();
                    pline.Close();
                    pline.Dispose();
                }
            }

            lt.Close();
            lt.Dispose();
            ms.Close();
            ms.Dispose();
            bt.Close();
            bt.Dispose();

            return result;
        }
        public static HashSet<Oid> HashSetIdsOfType<T>(this Database db) where T : Autodesk.AutoCAD.DatabaseServices.Entity
        {
            HashSet<Oid> objs = new HashSet<Oid>();
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var blockTable = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var modelSpace = (BlockTableRecord)tr.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                RXClass theClass = RXObject.GetClass(typeof(T));
                foreach (Oid oid in modelSpace)
                    if (oid.ObjectClass.IsDerivedFrom(theClass))
                        objs.Add(oid);
                tr.Commit();
            }
            return objs;
        }
        public static List<T> ListOfType<T>(this Database database, Transaction tr, bool discardFrozen = false) where T : Autodesk.AutoCAD.DatabaseServices.Entity
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

                    objs.Add(entity);
                }
            }
            return objs;
        }
        public static HashSet<T> HashSetOfType<T>(this Database db, Transaction tr, bool discardFrozen = false)
            where T : Autodesk.AutoCAD.DatabaseServices.Entity
        {
            return new HashSet<T>(db.ListOfType<T>(tr, discardFrozen));
        }
        public static HashSet<Entity> GetFjvEntities(this Database db, Transaction tr,
            bool discardWelds = true, bool discardStikBlocks = true, bool discardFrozen = false)
        {
            System.Data.DataTable dt = CsvData.FK;

            HashSet<Entity> entities = new HashSet<Entity>();

            var rawPlines = db.ListOfType<Polyline>(tr, discardFrozen);
            var plineQuery = rawPlines.Where(pline => GetPipeSystem(pline) != PipeSystemEnum.Ukendt);

            var rawBrefs = db.ListOfType<BlockReference>(tr, discardFrozen);
            var brQuery = rawBrefs.Where(x => UtilsDataTables.ReadStringParameterFromDataTable(
                            x.RealName(), dt, "Navn", 0) != default);

            HashSet<string> weldingBlocks = new HashSet<string>()
            {
                "SVEJSEPUNKT",
                "SVEJSEPUNKT-NOTXT",
            };

            HashSet<string> stikBlocks = new HashSet<string>()
            {
                "STIKAFGRENING",
                "STIKTEE"
            };

            if (discardWelds) brQuery = brQuery.Where(x => !weldingBlocks.Contains(x.RealName()));
            if (discardStikBlocks) brQuery = brQuery.Where(x => !stikBlocks.Contains(x.RealName()));

            //prdDbg($"FJV Entities > Polyline(s): {plineQuery.Count()}, BlockReference(s): {brQuery.Count()}");

            entities.UnionWith(brQuery);
            entities.UnionWith(plineQuery);
            return entities;
        }
        public static HashSet<BlockReference> GetFjvBlocks(this Database db, Transaction tr, System.Data.DataTable fjvKomponenter,
            bool discardWelds = true, bool discardStikBlocks = true, bool discardFrozen = false)
        {
            HashSet<BlockReference> entities = new HashSet<BlockReference>();

            var rawBrefs = db.ListOfType<BlockReference>(tr, discardFrozen);
            var brQuery = rawBrefs.Where(x => UtilsDataTables.ReadStringParameterFromDataTable(
                            x.RealName(), fjvKomponenter, "Navn", 0) != default);

            HashSet<string> weldingBlocks = new HashSet<string>()
            {
                "SVEJSEPUNKT",
                "SVEJSEPUNKT-NOTXT",
            };

            HashSet<string> stikBlocks = new HashSet<string>()
            {
                "STIKAFGRENING",
                "STIKTEE"
            };

            if (discardWelds) brQuery = brQuery.Where(x => !weldingBlocks.Contains(x.RealName()));
            if (discardStikBlocks) brQuery = brQuery.Where(x => !stikBlocks.Contains(x.RealName()));

            entities.UnionWith(brQuery);
            return entities;
        }
        public static HashSet<Polyline> GetFjvPipes(this Database db, Transaction tr, bool discardFrozen = false)
        {
            HashSet<Polyline> entities = new HashSet<Polyline>();

            var rawPlines = db.ListOfType<Polyline>(tr, discardFrozen);
            entities = rawPlines
                .Where(pline => PipeScheduleV2.PipeScheduleV2.GetPipeSystem(pline) != PipeSystemEnum.Ukendt)
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

                if (BlkRecId != Oid.Null)
                {
                    btr = tx.GetObject(BlkRecId, OpenMode.ForRead) as BlockTableRecord;
                    //Utils.prdDbg("Btr opened!");

                    ObjectIdCollection blockRefIds = btr.IsDynamicBlock ? btr.GetAnonymousBlockIds() : btr.GetBlockReferenceIds(true, true);
                    prdDbg("Number of brefids: " + blockRefIds.Count);

                    foreach (ObjectId blockRefId in blockRefIds)
                    {
                        if (btr.IsDynamicBlock)
                        {
                            ObjectIdCollection oids2 = blockRefId
                                .Go<BlockTableRecord>(tx)
                                .GetBlockReferenceIds(true, true);
                            foreach (Oid oid in oids2)
                                set.Add(oid.Go<BlockReference>(tx));
                        }
                        else { set.Add(blockRefId.Go<BlockReference>(tx)); }
                    }
                    Utils.prdDbg($"Number of refs: {blockRefIds.Count}.");
                }

            }
            return set;
        }
        public static double ToDegrees(this double radians) => (180 / Math.PI) * radians;
        public static double ToRadians(this double degrees) => (Math.PI / 180) * degrees;
        public static void Zoom(this Editor ed, Extents3d ext)
        {
            if (ed == null)
                throw new ArgumentNullException("ed");
            using (ViewTableRecord view = ed.GetCurrentView())
            {
                Matrix3d worldToEye = Matrix3d.WorldToPlane(view.ViewDirection) *
                    Matrix3d.Displacement(Point3d.Origin - view.Target) *
                    Matrix3d.Rotation(view.ViewTwist, view.ViewDirection, view.Target);
                ext.TransformBy(worldToEye);
                view.Width = ext.MaxPoint.X - ext.MinPoint.X;
                view.Height = ext.MaxPoint.Y - ext.MinPoint.Y;
                view.CenterPoint = new Point2d(
                    (ext.MaxPoint.X + ext.MinPoint.X) / 2.0,
                    (ext.MaxPoint.Y + ext.MinPoint.Y) / 2.0);
                ed.SetCurrentView(view);
            }
        }
        public static void ZoomExtents(this Editor ed)
        {
            Database db = ed.Document.Database;
            db.UpdateExt(false);
            Extents3d ext = (short)Application.GetSystemVariable("cvport") == 1 ?
                new Extents3d(db.Pextmin, db.Pextmax) :
                new Extents3d(db.Extmin, db.Extmax);
            ed.Zoom(ext);
        }
        public static DoubleCollection ToDoubleCollection(this IEnumerable<double> list) => new DoubleCollection(list.ToArray());

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
        private readonly int _scale;

        public Point3dHorizontalComparer(int scale = 1000)
        {
            _scale = scale;
        }

        public bool Equals(Point3d p1, Point3d p2) =>
            (int)(p1.X * _scale) == (int)(p2.X * _scale) && (int)(p1.Y * _scale) == (int)(p2.Y * _scale);

        public int GetHashCode(Point3d a)
        {
            int xHash = ((int)(a.X * _scale)).GetHashCode();
            int yHash = ((int)(a.Y * _scale)).GetHashCode();
            return xHash ^ yHash;
        }
    }
    public class Point2dEqualityComparer : IEqualityComparer<Point2d>
    {
        private readonly double _epsilon;

        public Point2dEqualityComparer(double epsilon = 1e-3)
        {
            _epsilon = epsilon;
        }

        public bool Equals(Point2d p1, Point2d p2)
        {
            //prdDbg(
            //    $"p1.X: {(int)(p1.X * _scale)} == p2.X: {(int)(p2.X * _scale)}\n" +
            //    $"p1.Y: {(int)(p1.Y * _scale)} == p2.Y: {(int)(p2.Y * _scale)}\n");
            return p1.X.Equalz(p2.X, _epsilon) && p1.Y.Equalz(p2.Y, _epsilon);
        }


        public int GetHashCode(Point2d point)
        {
            int xHash = ((int)(point.X / _epsilon)).GetHashCode();
            int yHash = ((int)(point.Y / _epsilon)).GetHashCode();
            return xHash ^ yHash;
        }
    }
    public class BulgeVertexEqualityComparer : IEqualityComparer<BulgeVertex>
    {
        private readonly int _scale;
        private Point2dEqualityComparer _equality;

        public BulgeVertexEqualityComparer(int scale = 1000)
        {
            _scale = scale;
            _equality = new Point2dEqualityComparer(scale);
        }

        public bool Equals(BulgeVertex v1, BulgeVertex v2) =>
            _equality.Equals(v1.Vertex, v2.Vertex);

        public int GetHashCode(BulgeVertex bv)
        {
            int xHash = ((int)(bv.Vertex.X * _scale)).GetHashCode();
            int yHash = ((int)(bv.Vertex.Y * _scale)).GetHashCode();
            return xHash ^ yHash;
        }
    }
    public class JsonConverterDouble : JsonConverter<double>
    {
        private readonly int _decimalPlaces;

        public JsonConverterDouble(int decimalPlaces)
        {
            _decimalPlaces = decimalPlaces;
        }

        public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return reader.GetDouble();
        }

        public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options)
        {
            string format = $"0.{new string('#', _decimalPlaces)}";
            writer.WriteRawValue(value.ToString(format, CultureInfo.InvariantCulture));
        }
    }
    public static class CsvData
    {
        private static readonly Dictionary<string, (DataTable data, DateTime lastModified)> cache =
            new Dictionary<string, (DataTable data, DateTime lastModified)>();
        private static readonly Dictionary<string, string> csvs = new Dictionary<string, string>();
        static CsvData()
        {
            if (!File.Exists(@"X:\AutoCAD DRI - 01 Civil 3D\_csv_register.csv"))
                throw new FileNotFoundException("CSV register not found!");

            var lines = File.ReadAllLines(
                @"X:\AutoCAD DRI - 01 Civil 3D\_csv_register.csv",
                Encoding.UTF8)
                .Skip(1);

            foreach (var line in lines)
            {
                var split = line.Split(';');
                csvs.Add(split[0], split[1]);
            }

            foreach (var item in csvs) LoadCsvFile(item.Key, item.Value);
        }
        private static void LoadCsvFile(string name, string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"File {path} does not exist!");

            DateTime lastWriteTime = File.GetLastWriteTimeUtc(path);
            DataTable dt = CsvReader.ReadCsvToDataTable(path, name);
            cache.Add(name, (dt, lastWriteTime));
        }
        private static DataTable GetDataTable(string name)
        {
            if (csvs.ContainsKey(name))
            {
                var path = csvs[name];
                if (cache.ContainsKey(name))
                {
                    var cached = cache[name];
                    var lastWriteTime = File.GetLastWriteTimeUtc(path);

#if DEBUG
                    //prdDbg($"Cached last modified: {cached.lastModified}\n" +
                    //                           $"File last modified: {lastWriteTime}");
#endif

                    if (cached.lastModified < lastWriteTime)
                    {
                        prdDbg($"Csv file {name} has been updated! Loading new version.");
                        cache.Remove(name);
                        LoadCsvFile(name, path);
                        return cache[name].data;
                    }
                    else return cached.data;
                }
                else
                {
                    LoadCsvFile(name, path);
                    return cache[name].data;
                }
            }
            else throw new System.Exception(
                $"Csv name {name} not defined!\nUpdate registration.");
        }
        /// <summary>
        /// Fjernvarme komponenter
        /// </summary>
        public static DataTable FK { get => GetDataTable("fjvKomponenter"); }
        /// <summary>
        /// Krydsninger
        /// </summary>
        public static DataTable Kryds { get => GetDataTable("krydsninger"); }
        /// <summary>
        /// Dybde
        /// </summary>
        public static DataTable Dybde { get => GetDataTable("dybde"); }
        /// <summary>
        /// Distances
        /// </summary>
        public static DataTable Dist { get => GetDataTable("distances"); }
        /// <summary>
        /// Installation og brændsel
        /// </summary>
        public static DataTable InstOgBrændsel { get => GetDataTable("instogbr"); }
        /// <summary>
        /// Anvendelseskoder for bygninger fra BBR registeret
        /// </summary>
        public static DataTable AnvKoder { get => GetDataTable("AnvKoder"); }
        /// <summary>
        /// Anvendelseskoder for enheder fra BBR registeret
        /// </summary>
        public static DataTable EnhKoder { get => GetDataTable("EnhKoder"); }
    }
    public static class AutocadColors
    {
        private static Dictionary<short, ColorData> _colorDictionary;

        static AutocadColors()
        {
            _colorDictionary = new Dictionary<short, ColorData>();
            LoadColorData();
        }

        private static void LoadColorData()
        {
            if (!File.Exists(@"X:\AutoCAD DRI - 01 Civil 3D\AutoCAD Colors\AutoCAD Color Index RGB Equivalents.csv"))
                throw new System.Exception($"AutoCAD Colors does not exist at:\n" +
                    $"@X:\\AutoCAD DRI - 01 Civil 3D\\AutoCAD Colors\\AutoCAD Color Index RGB Equivalents.csv");
            var lines = File.ReadAllLines(
                @"X:\AutoCAD DRI - 01 Civil 3D\AutoCAD Colors\AutoCAD Color Index RGB Equivalents.csv");
            foreach (var line in lines.Skip(1))
            {
                var parts = line.Split(';');
                var colorIndex = short.Parse(parts[0]);
                var hexRed = parts[1];
                var hexGreen = parts[2];
                var hexBlue = parts[3];
                var dRed = short.Parse(parts[4]);
                var dGreen = short.Parse(parts[5]);
                var dBlue = short.Parse(parts[6]);

                _colorDictionary[colorIndex] = new ColorData
                {
                    HexRed = hexRed,
                    HexGreen = hexGreen,
                    HexBlue = hexBlue,
                    DRed = dRed,
                    DGreen = dGreen,
                    DBlue = dBlue
                };
            }
        }

        public static string GetHexColor(short colorIndex)
        {
            if (_colorDictionary.TryGetValue(colorIndex, out var colorData))
            {
                return $"#{colorData.HexRed}{colorData.HexGreen}{colorData.HexBlue}";
            }
            return null;
        }

        public static (short red, short green, short blue) GetRGBColor(short colorIndex)
        {
            if (_colorDictionary.TryGetValue(colorIndex, out var colorData))
            {
                return (colorData.DRed, colorData.DGreen, colorData.DBlue);
            }
            return (0, 0, 0);
        }

        private class ColorData
        {
            public string HexRed { get; set; }
            public string HexGreen { get; set; }
            public string HexBlue { get; set; }
            public short DRed { get; set; }
            public short DGreen { get; set; }
            public short DBlue { get; set; }
        }
    }
}
