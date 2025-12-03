using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;

using Dreambuild.AutoCAD;

using IntersectUtilities;
using IntersectUtilities.UtilsCommon;

using Microsoft.Win32;

using NetTopologySuite.Features;
using NetTopologySuite.IO.Esri;

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;

using static IntersectUtilities.UtilsCommon.Utils;
using static IntersectUtilities.UtilsCommon.UtilsDataTables;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Log = ExportShapeFiles.ExportShapeFiles.SimpleLogger;

namespace ExportShapeFiles
{
    public class ExportShapeFiles : IExtensionApplication
    {
        #region IExtensionApplication members
        public void Initialize()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            doc.Editor.WriteMessage("\nExport Fjernvarme to shapefiles (original): EXPORTSHAPEFILES");
            doc.Editor.WriteMessage("\nExport Fjernvarme to shapefiles for many drawings (original): EXPORTSHAPEFILESBATCH");
            doc.Editor.WriteMessage("\nExport Fjernvarme to shapefiles (flotte polygoner): EXPORTFJVTOSHAPE\n");
            doc.Editor.WriteMessage("\nExport BBR to shapefiles (punkter): EXPORTBBRTOSHAPE\n");

#if DEBUG
            AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(EventHandlers.Debug_AssemblyResolve);
#endif
        }

        public void Terminate()
        {
#if DEBUG
            AppDomain.CurrentDomain.AssemblyResolve -= new ResolveEventHandler(EventHandlers.Debug_AssemblyResolve);
#endif
        }
        #endregion

        /// <command>EXPORTSHAPEFILESOLD</command>
        /// <summary>
        /// This is used for exporting to shape files in old format used by Gentofte/Gladsaxe project.
        /// Exports district heating data to two legacy shapefiles in a /SHP folder beside the DWG:
        /// - Polyline shapefile (base + "_pipes"): FJV pipes gathered via GetFjvPipes(...), converted
        ///   with PolylineFjvToShapeLineStringConverterOLD.
        /// - Point shapefile (base + "_comps"): FJV component blocks from GetFjvBlocks(..., CsvData.FK, false),
        ///   converted with BlockRefFjvToShapePointConverterOLD.
        /// Prompts for a base name, logs to export.log, and writes a .prj using ETRS_1989_UTM_Zone_32N.
        /// Use when reproducing the original export workflow that downstream tools still expect.
        /// </summary>
        /// <category>GIS</category>
        [CommandMethod("EXPORTSHAPEFILESOLD")]
        public void exportshapefilesOLD()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Document doc = docCol.MdiActiveDocument;

            string dbFilename = localDb.OriginalFileName;
            string path = Path.GetDirectoryName(dbFilename);
            string shapeExportPath = path + "\\SHP\\";
            if (Directory.Exists(shapeExportPath) == false) Directory.CreateDirectory(shapeExportPath);

            Log.LogFileName = shapeExportPath + "export.log";

            string baseDir = shapeExportPath;

            PromptStringOptions options = new PromptStringOptions("\nAngiv navnet på shapefilen: ");
            options.DefaultValue = $"{Path.GetFileNameWithoutExtension(dbFilename)}";
            options.UseDefaultValue = true;
            PromptResult result = doc.Editor.GetString(options);

            if (result.Status != PromptStatus.OK) return;

            string input = result.StringResult;

            if (input.IsNotNoE())
            {
                string shapeName = input;
                exportshapefilesmethodOLD(baseDir, shapeName, "_pipes", "_comps");
            }
        }
        public void exportshapefilesmethodOLD(
            string exportDir,
            string shapeBaseName,
            string polylineSuffix,
            string blockSuffix,
            Database database = null)
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = database ?? docCol.MdiActiveDocument.Database;
            Document doc = docCol.MdiActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    Log.log($"Exporting to {exportDir}.");

                    #region Exporting plines
                    HashSet<Polyline> pls = localDb.GetFjvPipes(tx, true);

                    Log.log($"{pls.Count} polyline(s) found for export.");

                    if (pls.Count > 0)
                    {
                        var features = pls.Select(
                            PolylineFjvToShapeLineStringConverterOLD.Convert);

                        var shapeFileName = shapeBaseName + polylineSuffix;
                        var fileName = Path.Combine(exportDir, shapeFileName);
                        var projName = Path.Combine(exportDir, $"{shapeFileName}.prj");

                        Shapefile.WriteAllFeatures(features, fileName);

                        //Create the projection file
                        using (var sw = new StreamWriter(projName))
                        {
                            //sw.Write(ProjNet.CoordinateSystems.ProjectedCoordinateSystem.WGS84_UTM(32, true));
                            sw.Write(@"PROJCS[""ETRS_1989_UTM_Zone_32N"",GEOGCS[""GCS_ETRS_1989"",DATUM[""D_ETRS_1989"",SPHEROID[""GRS_1980"",6378137.0,298.257222101]],PRIMEM[""Greenwich"",0.0],UNIT[""Degree"",0.0174532925199433]],PROJECTION[""Transverse_Mercator""],PARAMETER[""False_Easting"",500000.0],PARAMETER[""False_Northing"",0.0],PARAMETER[""Central_Meridian"",9.0],PARAMETER[""Scale_Factor"",0.9996],PARAMETER[""Latitude_Of_Origin"",0.0],UNIT[""Meter"",1.0]]");
                        }
                    }
                    #endregion

                    #region Exporting BRs
                    var dt = CsvData.FK;

                    HashSet<BlockReference> brs = localDb.GetFjvBlocks(tx, dt, false);

                    Log.log($"{brs.Count} br(s) found for export.");

                    if (brs.Count > 0)
                    {
                        var features = brs.Select(
                            BlockRefFjvToShapePointConverterOLD.Convert);

                        var shapeFileName = shapeBaseName + blockSuffix;
                        var fileName = Path.Combine(exportDir, shapeFileName);
                        var projName = Path.Combine(exportDir, $"{shapeFileName}.prj");

                        Shapefile.WriteAllFeatures(features, fileName);

                        //Create the projection file
                        using (var sw = new StreamWriter(projName))
                        {
                            //sw.Write(ProjNet.CoordinateSystems.ProjectedCoordinateSystem.WGS84_UTM(32, true));
                            sw.Write(@"PROJCS[""ETRS_1989_UTM_Zone_32N"",GEOGCS[""GCS_ETRS_1989"",DATUM[""D_ETRS_1989"",SPHEROID[""GRS_1980"",6378137.0,298.257222101]],PRIMEM[""Greenwich"",0.0],UNIT[""Degree"",0.0174532925199433]],PROJECTION[""Transverse_Mercator""],PARAMETER[""False_Easting"",500000.0],PARAMETER[""False_Northing"",0.0],PARAMETER[""Central_Meridian"",9.0],PARAMETER[""Scale_Factor"",0.9996],PARAMETER[""Latitude_Of_Origin"",0.0],UNIT[""Meter"",1.0]]");
                        }
                    }
                    #endregion
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    Log.log($"EXCEPTION!!!: {ex.ToString()}. Aborting export of current file!");
                    throw new System.Exception(ex.ToString());
                    //return;
                }
                tx.Abort();
            }
        }

        /// <command>EXPORTSHAPEFILES</command>
        /// <summary>
        /// Exports district heating data to two shapefiles in /SHP beside the DWG (modern converters):
        /// - Polyline shapefile (base + "_pipes"): pipes from GetFjvPipes(...), converted with
        ///   PolylineFjvToShapeLineStringConverter.
        /// - Point shapefile (base + "_comps"): components from GetFjvBlocks(..., CsvData.FK), converted
        ///   with BlockRefFjvToShapePointConverter.
        /// Prompts for a base file name (sanitizes periods), logs to export.log, and writes .prj with
        /// ETRS_1989_UTM_Zone_32N. Use as the primary one-drawing export for GIS delivery.
        /// </summary>
        /// <category>GIS</category>
        [CommandMethod("EXPORTSHAPEFILES")]
        public void exportshapefiles()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Document doc = docCol.MdiActiveDocument;

            string dbFilename = localDb.OriginalFileName;
            string path = Path.GetDirectoryName(dbFilename);
            string shapeExportPath = path + "\\SHP\\";
            if (Directory.Exists(shapeExportPath) == false) Directory.CreateDirectory(shapeExportPath);

            Log.LogFileName = shapeExportPath + "export.log";

            string baseDir = shapeExportPath;

            PromptStringOptions options = new PromptStringOptions("\nAngiv navnet på shapefilen: ");
            options.DefaultValue = $"{Path.GetFileNameWithoutExtension(dbFilename)}";
            options.UseDefaultValue = true;
            PromptResult result = doc.Editor.GetString(options);

            if (result.Status != PromptStatus.OK) return;

            string input = result.StringResult;
            input = input.Trim().Replace('.', '_');

            if (input.IsNotNoE())
            {
                string shapeName = input;
                exportshapefilesmethod(baseDir, shapeName, "_pipes", "_comps");
            }
        }
        public void exportshapefilesmethod(
            string exportDir,
            string shapeBaseName,
            string polylineSuffix,
            string blockSuffix,
            Database database = null)
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = database ?? docCol.MdiActiveDocument.Database;
            Document doc = docCol.MdiActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    Log.log($"Exporting to {exportDir}.");

                    #region Exporting plines
                    HashSet<Polyline> pls = localDb.GetFjvPipes(tx, true);

                    Log.log($"{pls.Count} polyline(s) found for export.");

                    if (pls.Count > 0)
                    {
                        var features = pls.Select(PolylineFjvToShapeLineStringConverter.Convert);

                        var shapeFileName = shapeBaseName + polylineSuffix;
                        var fileName = Path.Combine(exportDir, shapeFileName);
                        var projName = Path.Combine(exportDir, $"{shapeFileName}.prj");

                        Shapefile.WriteAllFeatures(features, fileName);

                        //Create the projection file
                        using (var sw = new StreamWriter(projName))
                        {
                            //sw.Write(ProjNet.CoordinateSystems.ProjectedCoordinateSystem.WGS84_UTM(32, true));
                            sw.Write(@"PROJCS[""ETRS_1989_UTM_Zone_32N"",GEOGCS[""GCS_ETRS_1989"",DATUM[""D_ETRS_1989"",SPHEROID[""GRS_1980"",6378137.0,298.257222101]],PRIMEM[""Greenwich"",0.0],UNIT[""Degree"",0.0174532925199433]],PROJECTION[""Transverse_Mercator""],PARAMETER[""False_Easting"",500000.0],PARAMETER[""False_Northing"",0.0],PARAMETER[""Central_Meridian"",9.0],PARAMETER[""Scale_Factor"",0.9996],PARAMETER[""Latitude_Of_Origin"",0.0],UNIT[""Meter"",1.0]]");
                        }
                    }
                    #endregion

                    #region Exporting BRs
                    var dt = CsvData.FK;

                    HashSet<BlockReference> brs = localDb.GetFjvBlocks(tx, dt);

                    Log.log($"{brs.Count} br(s) found for export.");

                    if (brs.Count > 0)
                    {
                        var features = brs.Select(BlockRefFjvToShapePointConverter.Convert);

                        var shapeFileName = shapeBaseName + blockSuffix;
                        var fileName = Path.Combine(exportDir, shapeFileName);
                        var projName = Path.Combine(exportDir, $"{shapeFileName}.prj");

                        Shapefile.WriteAllFeatures(features, fileName);

                        //Create the projection file
                        using (var sw = new StreamWriter(projName))
                        {
                            //sw.Write(ProjNet.CoordinateSystems.ProjectedCoordinateSystem.WGS84_UTM(32, true));
                            sw.Write(@"PROJCS[""ETRS_1989_UTM_Zone_32N"",GEOGCS[""GCS_ETRS_1989"",DATUM[""D_ETRS_1989"",SPHEROID[""GRS_1980"",6378137.0,298.257222101]],PRIMEM[""Greenwich"",0.0],UNIT[""Degree"",0.0174532925199433]],PROJECTION[""Transverse_Mercator""],PARAMETER[""False_Easting"",500000.0],PARAMETER[""False_Northing"",0.0],PARAMETER[""Central_Meridian"",9.0],PARAMETER[""Scale_Factor"",0.9996],PARAMETER[""Latitude_Of_Origin"",0.0],UNIT[""Meter"",1.0]]");
                        }
                    }
                    #endregion
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    Log.log($"EXCEPTION!!!: {ex.ToString()}. Aborting export of current file!");
                    throw new System.Exception(ex.ToString());
                    //return;
                }
                tx.Abort();
            }
        }

        /// <command>EXPORTFJVTOSHAPE</command>
        /// <summary>
        /// Exports all FJV entities from the current drawing to a single shapefile named "Fjernvarme"
        /// in /SHP. All entites are converted to polygons for best visual experience,
        /// but not great for analytical work.
        /// Entities are gathered via GetFjvEntities(tx, includePipes: true, includeBlocks: true,
        /// includeOther: true) and converted per-entity using FjvToShapeConverterFactory. Produces a
        /// consolidated GIS dataset and writes a .prj using ETRS_1989_UTM_Zone_32N.
        /// </summary>
        /// <category>GIS</category>
        [CommandMethod("EXPORTFJVTOSHAPE")]
        public void exportfjvtoshape()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Document doc = docCol.MdiActiveDocument;

            string dbFilename = localDb.OriginalFileName;
            string path = Path.GetDirectoryName(dbFilename);
            string shapeExportPath = path + "\\SHP\\";
            if (Directory.Exists(shapeExportPath) == false) Directory.CreateDirectory(shapeExportPath);

            Log.LogFileName = shapeExportPath + "export.log";

            string baseDir = shapeExportPath;

            string shapeName = "Fjernvarme";
            exportshapefilesmethod2(baseDir, shapeName);
        }

        public void exportshapefilesmethod2(
            string exportDir,
            string shapeBaseName,
            Database database = null)
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = database ?? docCol.MdiActiveDocument.Database;
            Document doc = docCol.MdiActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    Log.log($"Exporting to {exportDir}.");

                    #region Exporting
                    var dt = ExportShapeFilesEasyGis.Utils.GetFjvBlocksDt();
                    HashSet<Entity> ents = localDb.GetFjvEntities(tx, true, true, true);

                    Log.log($"{ents.Count} entit(y/ies) found for export.");

                    List<Feature> features = new List<Feature>();
                    foreach (var ent in ents)
                    {
                        var converter = FjvToShapeConverterFactory.CreateConverter(ent);
                        if (converter == null) continue;
                        features.Add(converter.Convert(ent));
                    }

                    string fileName = Path.Combine(exportDir, shapeBaseName);
                    string projName = Path.Combine(exportDir, $"{shapeBaseName}.prj");

                    Shapefile.WriteAllFeatures(features, fileName);

                    using (var sw = new StreamWriter(projName))
                    {
                        //sw.Write(ProjNet.CoordinateSystems.ProjectedCoordinateSystem.WGS84_UTM(32, true));
                        sw.Write(@"PROJCS[""ETRS_1989_UTM_Zone_32N"",GEOGCS[""GCS_ETRS_1989"",DATUM[""D_ETRS_1989"",SPHEROID[""GRS_1980"",6378137.0,298.257222101]],PRIMEM[""Greenwich"",0.0],UNIT[""Degree"",0.0174532925199433]],PROJECTION[""Transverse_Mercator""],PARAMETER[""False_Easting"",500000.0],PARAMETER[""False_Northing"",0.0],PARAMETER[""Central_Meridian"",9.0],PARAMETER[""Scale_Factor"",0.9996],PARAMETER[""Latitude_Of_Origin"",0.0],UNIT[""Meter"",1.0]]");
                    }
                    #endregion
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    Log.log($"EXCEPTION!!!: {ex}. Aborting export of current file!");
                    throw new System.Exception(ex.ToString());
                    //return;
                }
                tx.Abort();
            }
        }

        #region Old method to export TBL ares to QGIS for quantity analysis -- not needed any longer
        //[CommandMethod("EXPORTAREAS")]
        //public void exportareas()
        //{
        //    DocumentCollection docCol = Application.DocumentManager;
        //    Database localDb = docCol.MdiActiveDocument.Database;
        //    Document doc = docCol.MdiActiveDocument;

        //    using (Transaction tx = localDb.TransactionManager.StartTransaction())
        //    {
        //        string logFileName = @"X:\AutoCAD DRI - QGIS\EGIS\Export\export.log";
        //        Log.LogFileName = logFileName;

        //        PropertySetManager psm = new PropertySetManager(localDb, PSetDefs.DefinedSets.DriOmråder);
        //        PSetDefs.DriOmråder driOmråder = new PSetDefs.DriOmråder();

        //        try
        //        {
        //            string fileName = localDb.OriginalFileName;

        //            string dbFilename = localDb.OriginalFileName;
        //            string path = Path.GetDirectoryName(dbFilename);
        //            string shapeExportPath = path + "\\SHP\\";
        //            if (Directory.Exists(shapeExportPath) == false) Directory.CreateDirectory(shapeExportPath);

        //            string shapeName = "Områder";

        //            Log.log($"Exporting to {shapeExportPath}.");

        //            #region Exporting (p)lines
        //            HashSet<Polyline> pls = localDb.HashSetOfType<Polyline>(tx);

        //            pls = pls.Where(pl => pl.Layer == "0-OMRÅDER-OK").ToHashSet();

        //            Log.log($"{pls.Count} object(s) found for export.");

        //            DbfFieldDesc[] dbfFields = new DbfFieldDesc[4];

        //            dbfFields[0].FieldName = "Vejnavn";
        //            dbfFields[0].FieldType = DbfFieldType.General;
        //            dbfFields[0].FieldLength = 100;

        //            dbfFields[1].FieldName = "Vejklasse";
        //            dbfFields[1].FieldType = DbfFieldType.Number;
        //            dbfFields[1].FieldLength = 10;

        //            dbfFields[2].FieldName = "Belaegning";
        //            dbfFields[2].FieldType = DbfFieldType.General;
        //            dbfFields[2].FieldLength = 100;

        //            dbfFields[3].FieldName = "Nummer";
        //            dbfFields[3].FieldType = DbfFieldType.General;
        //            dbfFields[3].FieldLength = 100;



        //            using (ShapeFileWriter writer = ShapeFileWriter.CreateWriter(
        //                shapeExportPath, shapeName,
        //                ShapeType.PolyLine, dbfFields,
        //                EGIS.Projections.CoordinateReferenceSystemFactory.Default.GetCRSById(25832)
        //                .GetWKT(EGIS.Projections.PJ_WKT_TYPE.PJ_WKT1_GDAL, false)))
        //            {
        //                foreach (Polyline pline in pls)
        //                {
        //                    List<Point2d> points = new List<Point2d>();
        //                    int numOfVert = pline.NumberOfVertices - 1;
        //                    if (pline.Closed) numOfVert++;
        //                    for (int i = 0; i < numOfVert; i++)
        //                    {
        //                        switch (pline.GetSegmentType(i))
        //                        {
        //                            case SegmentType.Line:
        //                                LineSegment2d ls = pline.GetLineSegment2dAt(i);
        //                                if (i == 0)
        //                                {//First iteration
        //                                    points.Add(ls.StartPoint);
        //                                }
        //                                points.Add(ls.EndPoint);
        //                                break;
        //                            case SegmentType.Arc:
        //                                CircularArc2d arc = pline.GetArcSegment2dAt(i);
        //                                double sPar = arc.GetParameterOf(arc.StartPoint);
        //                                double ePar = arc.GetParameterOf(arc.EndPoint);
        //                                double length = arc.GetLength(sPar, ePar);
        //                                double radians = length / arc.Radius;
        //                                int nrOfSamples = (int)(radians / 0.04);
        //                                if (nrOfSamples < 3)
        //                                {
        //                                    if (i == 0) points.Add(arc.StartPoint);
        //                                    points.Add(arc.EndPoint);
        //                                }
        //                                else
        //                                {
        //                                    Point2d[] samples = arc.GetSamplePoints(nrOfSamples);
        //                                    if (i != 0) samples = samples.Skip(1).ToArray();
        //                                    foreach (Point2d p2d in samples) points.Add(p2d);
        //                                }
        //                                break;
        //                            case SegmentType.Coincident:
        //                            case SegmentType.Point:
        //                            case SegmentType.Empty:
        //                            default:
        //                                continue;
        //                        }
        //                    }

        //                    PointD[] shapePoints = points.Select(p => new PointD(p.X, p.Y)).ToArray();

        //                    string[] attributes = new string[4];

        //                    attributes[0] = psm.ReadPropertyString(pline, driOmråder.Vejnavn);
        //                    attributes[1] = psm.ReadPropertyString(pline, driOmråder.Vejklasse);
        //                    attributes[2] = psm.ReadPropertyString(pline, driOmråder.Belægning);
        //                    attributes[3] = psm.ReadPropertyString(pline, driOmråder.Nummer);

        //                    writer.AddRecord(shapePoints, shapePoints.Length, attributes);
        //                }
        //            }

        //            #endregion
        //        }
        //        catch (System.Exception ex)
        //        {
        //            tx.Abort();
        //            Log.log($"EXCEPTION!!!: {ex.ToString()}. Aborting export of current file!");
        //            //editor.WriteMessage("\n" + ex.Message);
        //            return;
        //        }
        //        tx.Abort();
        //    }
        //} 
        #endregion

        /// <command>EXPORTSHAPEFILESBATCH</command>
        /// <summary>
        /// Batch-exports shapefiles for many drawings listed in the CSV at
        /// "X:\\AutoCAD DRI - 01 Civil 3D\\Stier.csv". Prompts for an export folder, lets you pick a
        /// project (PrjId), filters rows for that project, validates DWG paths, opens each file as an
        /// external database, derives the base name from the "Etape" column, and calls
        /// exportshapefilesmethod(exportDir, etape, "", "-komponenter", extDb) to produce one pipe and
        /// one component shapefile per drawing. Logs to batchExport.log and continues on missing files.
        /// </summary>
        /// <category>GIS</category>
        [CommandMethod("EXPORTSHAPEFILESBATCH")]
        public void exportshapefilesbatch()
        {
            Log.LogFileName = @"X:\AutoCAD DRI - 01 Civil 3D\Export\batchExport.log";

            Log.log($"-~*~- Starting new BATCH export -~*~-");

            System.Data.DataTable dt = CsvReader.ReadCsvToDataTable(@"X:\AutoCAD DRI - 01 Civil 3D\Stier.csv", "Stier");

            string exportDir;
            OpenFolderDialog fsd = new OpenFolderDialog()
            {
                Title = "Choose folder where to export shapefiles:",
                InitialDirectory = @"c:\"
            };
            if (fsd.ShowDialog() == true)
            {
                exportDir = fsd.FolderName;
                if (string.IsNullOrEmpty(exportDir)) return;
            }
            else return;

            if (dt == null)
            {
                Log.log($"Datatable creation failed (null)! Aborting...");
                return;
            }
            else if (dt.Rows.Count == 0)
            {
                Log.log($"Datatable creation failed (0 rows)! Aborting...");
                return;
            }
            else
            {
                Log.log($"Datatable created with {dt.Rows.Count} record(s).");
            }

            var projectList = dt.AsEnumerable()
                .Select(x => x["PrjId"].ToString()).Distinct().OrderByAlphaNumeric(x => x);

            string projectName =
                Interaction.GetKeywords("Vælg projekt til eksport: ", projectList.ToArray());
            if (projectName.IsNoE()) return;

            List<string> list = dt.AsEnumerable()
                .Where(x => x["PrjId"].ToString() == projectName)
                .Select(x => x["Fremtid"].ToString()).ToList();

            Log.log($"Phases found: {list.Count}.");

            List<string> faulty = new List<string>();

            foreach (string s in list)
                if (!File.Exists(s)) faulty.Add(s);

            if (faulty.Count > 0)
            {
                //Remove faulty entries from the list to execute
                list = list.Except(faulty).ToList();

                foreach (string s in faulty)
                {
                    Log.log($"Failed to find file {s}! Removing from export list...");
                }
                Log.log($"{list.Count} file(s) left in export list.");
            }
            else
            {
                Log.log($"All files present.");
            }

            int count = 0;
            foreach (string fileName in list)
            {
                Log.log($"Processing " + fileName);
                count++;

                string phaseNumber = ReadStringParameterFromDataTable(fileName, dt, "Etape", 5);

                using (Database extDb = new Database(false, true))
                {
                    extDb.ReadDwgFile(fileName, System.IO.FileShare.ReadWrite, false, "");
                    using (Transaction extTx = extDb.TransactionManager.StartTransaction())
                    {
                        try
                        {
                            #region Old phase detection routine
                            //string phaseNumber = "";

                            //Regex regex = new Regex(@"(?<number>\d.\d)(?<extension>\.[^.]*$)");

                            //if (regex.IsMatch(fileName))
                            //{
                            //    Match match = regex.Match(fileName);
                            //    phaseNumber = match.Groups["number"].Value;
                            //    phaseNumber = phaseNumber.Replace(".", "");
                            //    Log.log($"{DateTime.Now}: Phase number detected: <{phaseNumber}>.");
                            //}
                            //else
                            //{
                            //    Log.log($"Detection of phase from filename failed! Using project name and incremental.");
                            //    phaseNumber = $"GENTOFTE1158_{count}";
                            //} 
                            #endregion

                            //prdDbg($"{phaseNumber} -> {fileName}");

                            exportshapefilesmethod(exportDir, phaseNumber, "", "-komponenter", extDb);
                        }
                        catch (System.Exception ex)
                        {
                            Log.log($"EXCEPTION!!!: {ex.ToString()}.");
                            prdDbg(ex);
                            extTx.Abort();
                            extDb.Dispose();
                            throw;
                        }

                        extTx.Abort();
                    }
                    //extDb.SaveAs(extDb.Filename, true, DwgVersion.Newest, extDb.SecurityParameters);
                }
                System.Windows.Forms.Application.DoEvents();
            }
            Log.log("Export completed!");
            //Console.ReadKey();
        }

        #region Old method to import many shape files at once
        //[CommandMethod("MASSIMPORTSHAPEFILESINZIPS")]
        //public void massimportshapefilesinzips()
        //{
        //    DocumentCollection docCol = Application.DocumentManager;
        //    Database localDb = docCol.MdiActiveDocument.Database;

        //    #region Definition of specific "layers"
        //    Dictionary<string, string> layers = new Dictionary<string, string>()
        //    {
        //        { "Drikkevand", "Drikkevand" },
        //        { "Spildevand", "Spildevand" },
        //        { "AFL_ikke_ibrug", "AFL_ikke_ibrug" },
        //        { "AFL_knude", "AFL_knude" },
        //        { "AFL_ledning_andet", "AFL_ledning_andet" },
        //        { "AFL_ledning_draen", "AFL_ledning_draen" },
        //        { "AFL_ledning_faelles", "AFL_ledning_faelles" },
        //        { "AFL_ledning_regn", "AFL_ledning_regn" },
        //        { "AFL_ledning_spild", "AFL_ledning_spild" },
        //        { "VAND_brandhane", "VAND_brandhane" },
        //        { "VAND_komponent", "VAND_komponent" },
        //        { "VAND_ledning", "VAND_ledning" },
        //        { "VAND_ledning_ikke_i_brug", "VAND_ledning_ikke_i_brug" },
        //        { "VAND_punkt", "VAND_punkt" },
        //    };
        //    #endregion

        //    using (Transaction tx = localDb.TransactionManager.StartTransaction())
        //    {
        //        try
        //        {
        //            #region Get file and folder of gml
        //            string pathToTopFolder = string.Empty;
        //            FolderSelectDialog fsd = new FolderSelectDialog()
        //            {
        //                Title = "Choose folder where shape files are stored: ",
        //                InitialDirectory = @"C:\"
        //            };
        //            if (fsd.ShowDialog(IntPtr.Zero))
        //            {
        //                pathToTopFolder = fsd.FileName + "\\";
        //            }
        //            else return;

        //            var files = Directory.EnumerateFiles(pathToTopFolder, "*.zip", SearchOption.AllDirectories);

        //            #endregion

        //            using (Database xDb = new Database(true, true))
        //            using (Transaction xTx = xDb.TransactionManager.StartTransaction())
        //            {
        //                try
        //                {
        //                    int counter = 0;
        //                    foreach (var f in files)
        //                    {
        //                        string dir = Path.GetDirectoryName(f);
        //                        string fileName = Path.GetFileNameWithoutExtension(f);
        //                        string unzipDir = dir + "\\" + fileName;

        //                        if (Directory.Exists(unzipDir)) Directory.Delete(unzipDir, true);

        //                        ZipFile.ExtractToDirectory(f, unzipDir, System.Text.Encoding.UTF8);

        //                        var shapes = Directory.EnumerateFiles(unzipDir, "*.shp", SearchOption.AllDirectories);

        //                        foreach (var sf in shapes)
        //                        {
        //                            using (ShapeFile shape = new ShapeFile(sf))
        //                            {
        //                                ShapeType shapeType = shape.ShapeType;
        //                                switch (shapeType)
        //                                {
        //                                    case ShapeType.PolyLine:
        //                                    case ShapeType.PolyLineZ:
        //                                        {
        //                                            prdDbg($"{shapeType} - Name: {shape.Name} - Tag: {shape.Tag}");
        //                                            string sfn = Path.GetFileName(sf);

        //                                            #region Ownerspecific layer handling #1
        //                                            if (layers.Any(x => sfn.StartsWith(x.Key)))
        //                                                xDb.CheckOrCreateLayer(
        //                                                    layers.Where(x => sfn.StartsWith(x.Key))
        //                                                    .FirstOrDefault().Value);
        //                                            #endregion

        //                                            ShapeFileEnumerator sfe = shape.GetShapeFileEnumerator();
        //                                            while (sfe.MoveNext())
        //                                            {
        //                                                counter++;
        //                                                Polyline pl = new Polyline();
        //                                                ReadOnlyCollection<PointD[]> current = sfe.Current;
        //                                                foreach (PointD[] parray in current)
        //                                                {
        //                                                    foreach (PointD p in parray)
        //                                                    {
        //                                                        pl.AddVertexAt(
        //                                                            pl.NumberOfVertices,
        //                                                            new Point2d(p.X, p.Y),
        //                                                            0, 0, 0);
        //                                                    }
        //                                                }
        //                                                pl.AddEntityToDbModelSpace(xDb);

        //                                                #region Ownerspecific layer handling #2
        //                                                if (layers.Any(x => sfn.StartsWith(x.Key)))
        //                                                    pl.Layer = layers
        //                                                        .Where(x => sfn.StartsWith(x.Key))
        //                                                        .FirstOrDefault().Value;
        //                                                #endregion
        //                                            }
        //                                        }
        //                                        break;
        //                                    case ShapeType.NullShape:
        //                                    case ShapeType.Point:
        //                                    case ShapeType.Polygon:
        //                                    case ShapeType.MultiPoint:
        //                                    case ShapeType.PointZ:
        //                                    case ShapeType.PolygonZ:
        //                                    case ShapeType.MultiPointZ:
        //                                    case ShapeType.PointM:
        //                                    case ShapeType.PolyLineM:
        //                                        prdDbg($"Encountered unhandled ShapeType: {shapeType}");
        //                                        break;
        //                                    default:
        //                                        break;
        //                                }
        //                            }


        //                        }

        //                        Directory.Delete(unzipDir, true);
        //                    }
        //                    prdDbg($"Total shapes imported: {counter}");
        //                    System.Windows.Forms.Application.DoEvents();

        //                }
        //                catch (System.Exception ex)
        //                {
        //                    prdDbg(ex);
        //                    //xDb.CloseInput(true);
        //                    xTx.Abort();
        //                    throw;
        //                }
        //                xTx.Commit();
        //                xDb.SaveAs(pathToTopFolder + "SAMLET.dwg", true, DwgVersion.Newest, xDb.SecurityParameters);
        //            }
        //        }
        //        catch (System.Exception ex)
        //        {
        //            tx.Abort();
        //            prdDbg(ex);
        //            return;
        //        }
        //        tx.Commit();
        //    }
        //} 
        #endregion

        /// <command>EXPORTBBRTOSHAPE</command>
        /// <summary>
        /// Exports all BBR blocks from current drawing to shape file.
        /// </summary>
        /// <category>GIS</category>
        [CommandMethod("EXPORTBBRTOSHAPE")]
        public void exportbbrtoshape()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Document doc = docCol.MdiActiveDocument;

            string dbFilename = localDb.OriginalFileName;
            string path = Path.Combine(Path.GetDirectoryName(dbFilename), "SHP");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            string shpFileName = Path.Combine(path, "BBR.shp");
            string prjFileName = Path.Combine(path, "BBR.prj");

            Log.LogFileName = Path.Combine(path, "export.log");
            Log.EchoToEditor = true;

            using var tx = localDb.TransactionManager.StartTransaction();

            try
            {
                Log.log($"Exporting to {shpFileName}.");

                #region Exporting BRs
                HashSet<BlockReference> brs =
                    localDb.HashSetOfTypeWithPs<BlockReference>(
                    tx, PSetDefs.DefinedSets.BBR);

                Log.log($"{brs.Count} br(s) found for export.");

                if (brs.Count > 0)
                {
                    var features = brs.Select(
                        x => BlockRefWithPsToShapePointConverter.Convert(
                            x, [PSetDefs.DefinedSets.BBR]));

                    Shapefile.WriteAllFeatures(features, shpFileName);

                    //Create the projection file
                    using (var sw = new StreamWriter(prjFileName))
                    {
                        //sw.Write(ProjNet.CoordinateSystems.ProjectedCoordinateSystem.WGS84_UTM(32, true));
                        sw.Write(@"PROJCS[""ETRS_1989_UTM_Zone_32N"",GEOGCS[""GCS_ETRS_1989"",DATUM[""D_ETRS_1989"",SPHEROID[""GRS_1980"",6378137.0,298.257222101]],PRIMEM[""Greenwich"",0.0],UNIT[""Degree"",0.0174532925199433]],PROJECTION[""Transverse_Mercator""],PARAMETER[""False_Easting"",500000.0],PARAMETER[""False_Northing"",0.0],PARAMETER[""Central_Meridian"",9.0],PARAMETER[""Scale_Factor"",0.9996],PARAMETER[""Latitude_Of_Origin"",0.0],UNIT[""Meter"",1.0]]");
                    }
                }
                #endregion
            }
            catch (System.Exception ex)
            {
                tx.Abort();
                Log.log($"EXCEPTION!!!: {ex.ToString()}. Aborting export of current file!");
                throw new System.Exception(ex.ToString());
                //return;
            }
            tx.Abort();
        }

        public static class SimpleLogger
        {
            public static bool EchoToEditor { get; set; } = true;
            public static string LogFileName { get; set; } = "C:\\Temp\\ShapeExportLog.txt";
            public static void log(string msg)
            {
                File.AppendAllLines(LogFileName, new string[] { $"{DateTime.Now}: {msg}" });
                if (EchoToEditor) prdDbg(msg);
            }
        }

        /// <command>SELECTBYHANDLE, SBH</command>
        /// <summary>
        /// Utility (alias SBH): prompts for a hexadecimal object handle, resolves it to an ObjectId in
        /// the current drawing, and selects the entity (implied selection). Handy for quickly finding
        /// and verifying objects referenced in logs or attributes during export preparation.
        /// </summary>
        /// <category>GIS</category>
        [CommandMethod("SELECTBYHANDLE")]
        [CommandMethod("SBH")]
        public void selectbyhandle()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    PromptResult pr = editor.GetString("\nEnter handle of object to select: ");

                    if (pr.Status == PromptStatus.OK)
                    {
                        // Convert hexadecimal string to 64-bit integer
                        long ln = Convert.ToInt64(pr.StringResult, 16);
                        // Now create a Handle from the long integer
                        Handle hn = new Handle(ln);
                        // And attempt to get an ObjectId for the Handle
                        ObjectId id = localDb.GetObjectId(false, hn, 0);
                        // Finally let's open the object and erase it
                        editor.SetImpliedSelection(new[] { id });

                    }
                }
                catch (System.Exception ex)
                {
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }
    }
}