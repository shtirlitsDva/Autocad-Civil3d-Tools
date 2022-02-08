using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Aec.PropertyData.DatabaseServices;
using IntersectUtilities;
using IntersectUtilities.UtilsCommon;

using static IntersectUtilities.PipeSchedule;
using static IntersectUtilities.ComponentSchedule;
using static IntersectUtilities.UtilsCommon.Utils;
using static IntersectUtilities.UtilsCommon.UtilsDataTables;

using EGIS.ShapeFileLib;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

using Log = ExportShapeFiles.ExportShapeFiles.SimpleLogger;
using System.Data;
using Autodesk.AutoCAD.EditorInput;

namespace ExportShapeFiles
{
    public class ExportShapeFiles : IExtensionApplication
    {
        #region IExtensionApplication members
        public void Initialize()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            doc.Editor.WriteMessage("\nExport lines to shapefiles: EXPORTSHAPEFILES");
        }

        public void Terminate()
        {
        }
        #endregion

        [CommandMethod("EXPORTSHAPEFILES")]
        public void exportshapefiles()
        {
            Log.LogFileName = @"X:\AutoCAD DRI - 01 Civil 3D\Export\export.log";

            string baseDir = @"X:\037-1178 - Gladsaxe udbygning - Dokumenter\01 Intern\02 Tegninger\03 Intern\";
            string shapeName = "GLADSAXE1178_1.2";

            exportshapefilesmethod(baseDir, shapeName, "_pipes", "_comps"); 
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

                    #region Exporting (p)lines
                    HashSet<Polyline> pls = localDb.HashSetOfType<Polyline>(tx);

                    pls = pls.Where(pl => (pl.Layer.Contains("FJV-TWIN") ||
                                             pl.Layer.Contains("FJV-FREM") ||
                                             pl.Layer.Contains("FJV-RETUR")))
                        .Where(pl => GetPipeDN(pl) != 999)
                        .ToHashSet();

                    Log.log($"{pls.Count} polyline(s) found for export.");

                    #region Field def
                    DbfFieldDesc[] dbfFields = new DbfFieldDesc[3];

                    dbfFields[0].FieldName = "DN";
                    dbfFields[0].FieldType = DbfFieldType.Number;
                    dbfFields[0].FieldLength = 10;

                    dbfFields[1].FieldName = "System";
                    dbfFields[1].FieldType = DbfFieldType.General;
                    dbfFields[1].FieldLength = 100;

                    dbfFields[2].FieldName = "Serie";
                    dbfFields[2].FieldType = DbfFieldType.General;
                    dbfFields[2].FieldLength = 100;
                    #endregion

                    using (ShapeFileWriter writer = ShapeFileWriter.CreateWriter(
                        exportDir, shapeBaseName + polylineSuffix,
                        ShapeType.PolyLine, dbfFields,
                        EGIS.Projections.CoordinateReferenceSystemFactory.Default.GetCRSById(25832)
                        .GetWKT(EGIS.Projections.PJ_WKT_TYPE.PJ_WKT1_GDAL, false)))
                    {
                        foreach (Polyline pline in pls)
                        {
                            List<Point2d> points = new List<Point2d>();
                            int numOfVert = pline.NumberOfVertices - 1;
                            if (pline.Closed) numOfVert++;
                            for (int i = 0; i < numOfVert; i++)
                            {
                                switch (pline.GetSegmentType(i))
                                {
                                    case SegmentType.Line:
                                        LineSegment2d ls = pline.GetLineSegment2dAt(i);
                                        if (i == 0)
                                        {//First iteration
                                            points.Add(ls.StartPoint);
                                        }
                                        points.Add(ls.EndPoint);
                                        break;
                                    case SegmentType.Arc:
                                        CircularArc2d arc = pline.GetArcSegment2dAt(i);
                                        double sPar = arc.GetParameterOf(arc.StartPoint);
                                        double ePar = arc.GetParameterOf(arc.EndPoint);
                                        double length = arc.GetLength(sPar, ePar);
                                        double radians = length / arc.Radius;
                                        int nrOfSamples = (int)(radians / 0.04);
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
                                        continue;
                                }
                            }

                            PointD[] shapePoints = points.Select(p => new PointD(p.X, p.Y)).ToArray();

                            string[] attributes = new string[3];
                            attributes[0] = GetPipeDN(pline).ToString();
                            attributes[1] = GetPipeSystem(pline);
                            attributes[2] = GetPipeSeries(pline);

                            writer.AddRecord(shapePoints, shapePoints.Length, attributes);
                        }
                    }
                    #endregion

                    #region Exporting BRs
                    System.Data.DataTable komponenter = CsvReader.ReadCsvToDataTable(
                                        @"X:\AutoCAD DRI - 01 Civil 3D\FJV Dynamiske Komponenter.csv", "FjvKomponenter");

                    HashSet<BlockReference> allBrs = localDb.HashSetOfType<BlockReference>(tx);

                    HashSet<BlockReference> brs = allBrs.Where(x => UtilsDataTables.ReadStringParameterFromDataTable(
                            x.RealName(), komponenter, "Navn", 0) != default).ToHashSet();

                    Log.log($"{brs.Count} br(s) found for export.");

                    //Handle legacy blocks
                    System.Data.DataTable stdBlocks = CsvReader.ReadCsvToDataTable(@"X:\AutoCAD DRI - 01 Civil 3D\FJV Komponenter.csv", "FjvKomponenter");

                    HashSet<BlockReference> legacyBrs = allBrs.Where(x => UtilsDataTables.ReadStringParameterFromDataTable(
                            x.Name, stdBlocks, "Navn", 0) != default).ToHashSet();

                    if (brs.Count > 0) Log.log($"Legacy blocks detected: {legacyBrs.Count}.");

                    #region Field def
                    dbfFields = new DbfFieldDesc[8];

                    dbfFields[0].FieldName = "BlockName";
                    dbfFields[0].FieldType = DbfFieldType.General;
                    dbfFields[0].FieldLength = 100;

                    dbfFields[1].FieldName = "Type";
                    dbfFields[1].FieldType = DbfFieldType.General;
                    dbfFields[1].FieldLength = 100;

                    dbfFields[2].FieldName = "Rotation";
                    dbfFields[2].FieldType = DbfFieldType.General;
                    dbfFields[2].FieldLength = 100;

                    dbfFields[3].FieldName = "System";
                    dbfFields[3].FieldType = DbfFieldType.General;
                    dbfFields[3].FieldLength = 100;

                    dbfFields[4].FieldName = "DN1";
                    dbfFields[4].FieldType = DbfFieldType.General;
                    dbfFields[4].FieldLength = 100;

                    dbfFields[5].FieldName = "DN2";
                    dbfFields[5].FieldType = DbfFieldType.General;
                    dbfFields[5].FieldLength = 100;

                    dbfFields[6].FieldName = "Serie";
                    dbfFields[6].FieldType = DbfFieldType.General;
                    dbfFields[6].FieldLength = 100;

                    dbfFields[7].FieldName = "Vinkel";
                    dbfFields[7].FieldType = DbfFieldType.General;
                    dbfFields[7].FieldLength = 100;
                    #endregion

                    using (ShapeFileWriter writer = ShapeFileWriter.CreateWriter(
                        exportDir, shapeBaseName + blockSuffix,
                        ShapeType.Point, dbfFields,
                        EGIS.Projections.CoordinateReferenceSystemFactory.Default.GetCRSById(25832)
                        .GetWKT(EGIS.Projections.PJ_WKT_TYPE.PJ_WKT1_GDAL, false)))
                    {
                        foreach (BlockReference br in brs)
                        {
                            PointD[] shapePoints = new PointD[1];
                            shapePoints[0] = new PointD(br.Position.X, br.Position.Y);

                            string[] attributes = new string[8];
                            attributes[0] = br.RealName();
                            attributes[1] = ReadComponentType(br, komponenter);
                            attributes[2] = ReadBlockRotation(br, komponenter).ToString("0.00");
                            attributes[3] = ReadComponentSystem(br, komponenter);
                            attributes[4] = ReadComponentDN1(br, komponenter);
                            attributes[5] = ReadComponentDN2(br, komponenter);
                            attributes[6] = ReadComponentSeries(br, komponenter);
                            attributes[7] = ReadComponentVinkel(br, komponenter);

                            writer.AddRecord(shapePoints, shapePoints.Length, attributes);
                        }

                        foreach (BlockReference br in legacyBrs)
                        {
                            PointD[] shapePoints = new PointD[1];
                            shapePoints[0] = new PointD(br.Position.X, br.Position.Y);

                            string[] attributes = new string[8];
                            attributes[0] = br.Name;
                            attributes[1] = ReadStringParameterFromDataTable(br.Name, stdBlocks, "Type", 0);
                            attributes[2] = (br.Rotation * (180 / Math.PI)).ToString("0.###");
                            attributes[3] = ReadStringParameterFromDataTable(br.Name, stdBlocks, "System", 0);
                            attributes[4] = ReadStringParameterFromDataTable(br.Name, stdBlocks, "DN1", 0); ;
                            attributes[5] = ReadStringParameterFromDataTable(br.Name, stdBlocks, "DN2", 0); ;
                            attributes[6] = "S3";
                            attributes[7] = "0";

                            writer.AddRecord(shapePoints, shapePoints.Length, attributes);
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

        [CommandMethod("EXPORTAREAS")]
        public void exportareas()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Document doc = docCol.MdiActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                string logFileName = @"X:\AutoCAD DRI - QGIS\EGIS\Export\export.log";
                Log.LogFileName = logFileName;

                PropertySetManager psm = new PropertySetManager(localDb, PSetDefs.DefinedSets.DriOmråder);
                PSetDefs.DriOmråder driOmråder = new PSetDefs.DriOmråder();

                try
                {
                    string fileName = localDb.OriginalFileName;

                    string baseDir = @"X:\037-1178 - Gladsaxe udbygning - Dokumenter\01 Intern\04 Projektering\05 Optælling til TBL\";
                    string shapeName = "Områder";

                    Log.log($"Exporting to {baseDir}.");

                    #region Exporting (p)lines
                    HashSet<Polyline> pls = localDb.HashSetOfType<Polyline>(tx);

                    pls = pls.Where(pl => pl.Layer == "0-OMRÅDER-OK").ToHashSet();

                    Log.log($"{pls.Count} object(s) found for export.");

                    DbfFieldDesc[] dbfFields = new DbfFieldDesc[4];

                    dbfFields[0].FieldName = "Vejnavn";
                    dbfFields[0].FieldType = DbfFieldType.General;
                    dbfFields[0].FieldLength = 100;

                    dbfFields[1].FieldName = "Vejklasse";
                    dbfFields[1].FieldType = DbfFieldType.Number;
                    dbfFields[1].FieldLength = 10;

                    dbfFields[2].FieldName = "Belaegning";
                    dbfFields[2].FieldType = DbfFieldType.General;
                    dbfFields[2].FieldLength = 100;

                    dbfFields[3].FieldName = "Nummer";
                    dbfFields[3].FieldType = DbfFieldType.General;
                    dbfFields[3].FieldLength = 100;



                    using (ShapeFileWriter writer = ShapeFileWriter.CreateWriter(
                        baseDir, shapeName,
                        ShapeType.PolyLine, dbfFields,
                        EGIS.Projections.CoordinateReferenceSystemFactory.Default.GetCRSById(25832)
                        .GetWKT(EGIS.Projections.PJ_WKT_TYPE.PJ_WKT1_GDAL, false)))
                    {
                        foreach (Polyline pline in pls)
                        {
                            List<Point2d> points = new List<Point2d>();
                            int numOfVert = pline.NumberOfVertices - 1;
                            if (pline.Closed) numOfVert++;
                            for (int i = 0; i < numOfVert; i++)
                            {
                                switch (pline.GetSegmentType(i))
                                {
                                    case SegmentType.Line:
                                        LineSegment2d ls = pline.GetLineSegment2dAt(i);
                                        if (i == 0)
                                        {//First iteration
                                            points.Add(ls.StartPoint);
                                        }
                                        points.Add(ls.EndPoint);
                                        break;
                                    case SegmentType.Arc:
                                        CircularArc2d arc = pline.GetArcSegment2dAt(i);
                                        double sPar = arc.GetParameterOf(arc.StartPoint);
                                        double ePar = arc.GetParameterOf(arc.EndPoint);
                                        double length = arc.GetLength(sPar, ePar);
                                        double radians = length / arc.Radius;
                                        int nrOfSamples = (int)(radians / 0.04);
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
                                        continue;
                                }
                            }

                            PointD[] shapePoints = points.Select(p => new PointD(p.X, p.Y)).ToArray();

                            string[] attributes = new string[4];

                            psm.GetOrAttachPropertySet(pline);

                            attributes[0] = psm.ReadPropertyString(driOmråder.Vejnavn);
                            attributes[1] = psm.ReadPropertyString(driOmråder.Vejklasse);
                            attributes[2] = psm.ReadPropertyString(driOmråder.Belægning);
                            attributes[3] = psm.ReadPropertyString(driOmråder.Nummer);

                            writer.AddRecord(shapePoints, shapePoints.Length, attributes);
                        }
                    }

                    #endregion
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    Log.log($"EXCEPTION!!!: {ex.ToString()}. Aborting export of current file!");
                    //editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Abort();
            }
        }
        
        [CommandMethod("EXPORTSHAPEFILESBATCH")]
        public void exportshapefilesbatch()
        {
            Log.LogFileName = @"X:\AutoCAD DRI - 01 Civil 3D\Export\batchExport.log";

            Log.log($"-~*~- Starting new BATCH export -~*~-");

            System.Data.DataTable dt = CsvReader.ReadCsvToDataTable(@"X:\AutoCAD DRI - 01 Civil 3D\Stier.csv", "Stier");

            string exportDir = @"X:\0371-1158 - Gentofte Fase 4 - Dokumenter\02 Ekstern\01 Gældende tegninger\01 GIS input\02 Trace shape\Staging\";

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

            List<string> list = dt.AsEnumerable()
                .Where(x => x["PrjId"].ToString() == "GENTOFTE1158")
                .Select(x => x["Fremtid"].ToString()).ToList();

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
                Log.log($"Processing " + Path.GetFileName(fileName));
                count++;

                using (Database extDb = new Database(false, true))
                {
                    extDb.ReadDwgFile(fileName, System.IO.FileShare.ReadWrite, false, "");
                    using (Transaction extTx = extDb.TransactionManager.StartTransaction())
                    {
                        try
                        {
                            string phaseNumber = "";

                            Regex regex = new Regex(@"(?<number>\d.\d)(?<extension>\.[^.]*$)");

                            if (regex.IsMatch(fileName))
                            {
                                Match match = regex.Match(fileName);
                                phaseNumber = match.Groups["number"].Value;
                                phaseNumber = phaseNumber.Replace(".", "");
                                Log.log($"{DateTime.Now}: Phase number detected: <{phaseNumber}>.");
                            }
                            else
                            {
                                Log.log($"Detection of phase from filename failed! Using project name and incremental.");
                                phaseNumber = $"GENTOFTE1158_{count}";
                            }

                            exportshapefilesmethod(exportDir, phaseNumber, "", "-komponenter", extDb);
                        }
                        catch (System.Exception ex)
                        {
                            Log.log($"EXCEPTION!!!: {ex.ToString()}.");
                            prdDbg(ex.ToString());
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

        [CommandMethod("selectbyhandle")]
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

