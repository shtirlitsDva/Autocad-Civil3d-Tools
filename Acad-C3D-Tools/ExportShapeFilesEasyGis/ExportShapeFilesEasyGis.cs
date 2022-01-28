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

using EGIS.ShapeFileLib;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

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
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Document doc = docCol.MdiActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                string logFileName = @"X:\AutoCAD DRI - QGIS\EGIS\Export\export.log";
                
                try
                {
                    string fileName = localDb.OriginalFileName;

                    string baseDir = @"X:\022-1226 Egedal - Krogholmvej, Etape 1 - Dokumenter\01 Intern\02 Tegninger\03 Intern\2022.01.27 - DWF til optælling\";
                    string shapeName = "KROGHLM1226";

                    File.AppendAllLines(logFileName, new string[] { $"{DateTime.Now}: Exporting to {baseDir}." });

                    #region Exporting (p)lines
                    HashSet<Polyline> pls = localDb.HashSetOfType<Polyline>(tx);


                    pls = pls.Where(pl => (pl.Layer.Contains("FJV-TWIN") ||
                                             pl.Layer.Contains("FJV-FREM") ||
                                             pl.Layer.Contains("FJV-RETUR"))).ToHashSet();

                    File.AppendAllLines(logFileName, new string[] { $"{DateTime.Now}: {pls.Count} pline(s) found for export." });

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
                        baseDir, shapeName + "_pipes",
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

                    System.Data.DataTable komponenter = CsvReader.ReadCsvToDataTable(
                            @"X:\AutoCAD DRI - 01 Civil 3D\FJV Dynamiske Komponenter.csv", "FjvKomponenter");

                    HashSet<BlockReference> brs = localDb.ListOfType<BlockReference>(tx)
                        .Where(x => UtilsDataTables.ReadStringParameterFromDataTable(
                            x.RealName(), komponenter, "Navn", 0) != null).ToHashSet();

                    File.AppendAllLines(logFileName, new string[] { $"{DateTime.Now}: {brs.Count} br(s) found for export." });

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
                        baseDir, shapeName + "_comps",
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
                    }

                    #endregion
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    File.AppendAllLines(logFileName, new string[] { $"{DateTime.Now}: An exception was caught! Message: {ex.ToString()}. Aborting export of current file!" });
                    //editor.WriteMessage("\n" + ex.Message);
                    return;
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

                PropertySetManager psm = new PropertySetManager(localDb, PSetDefs.DefinedSets.DriOmråder);
                PSetDefs.DriOmråder driOmråder = new PSetDefs.DriOmråder();

                try
                {
                    string fileName = localDb.OriginalFileName;

                    string baseDir = @"X:\022-1226 Egedal - Krogholmvej, Etape 1 - Dokumenter\01 Intern\02 Tegninger\03 Intern\2022.01.27 - DWF til optælling\";
                    string shapeName = "Områder";

                    File.AppendAllLines(logFileName, new string[] { $"{DateTime.Now}: Exporting to {baseDir}." });

                    #region Exporting (p)lines
                    HashSet<Polyline> pls = localDb.HashSetOfType<Polyline>(tx);

                    pls = pls.Where(pl => pl.Layer == "0-OMRÅDER-OK").ToHashSet();

                    File.AppendAllLines(logFileName, new string[] { $"{DateTime.Now}: {pls.Count} object(s) found for export." });

                    DbfFieldDesc[] dbfFields = new DbfFieldDesc[3];

                    dbfFields[0].FieldName = "Vejnavn";
                    dbfFields[0].FieldType = DbfFieldType.General;
                    dbfFields[0].FieldLength = 100;

                    dbfFields[1].FieldName = "Vejklasse";
                    dbfFields[1].FieldType = DbfFieldType.Number;
                    dbfFields[1].FieldLength = 10;

                    dbfFields[2].FieldName = "Belaegning";
                    dbfFields[2].FieldType = DbfFieldType.General;
                    dbfFields[2].FieldLength = 100;

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

                            string[] attributes = new string[3];

                            psm.GetOrAttachPropertySet(pline);
                            
                            attributes[0] = psm.ReadPropertyString(driOmråder.Vejnavn);
                            attributes[1] = psm.ReadPropertyString(driOmråder.Vejklasse);
                            attributes[2] = psm.ReadPropertyString(driOmråder.Belægning);

                            writer.AddRecord(shapePoints, shapePoints.Length, attributes);
                        }
                    }

                    #endregion
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    File.AppendAllLines(logFileName, new string[] { $"{DateTime.Now}: An exception was caught! Message: {ex.ToString()}. Aborting export of current file!" });
                    //editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Abort();
            }
        }
    }
}
