using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
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
using Autodesk.Gis.Map.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Svg;
using BlockReference = Autodesk.AutoCAD.DatabaseServices.BlockReference;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using DataType = Autodesk.Gis.Map.Constants.DataType;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using ObjectIdCollection = Autodesk.AutoCAD.DatabaseServices.ObjectIdCollection;
using oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;
using static IntersectUtilities.Utils;

namespace IntersectUtilities
{
    /// <summary>
    /// Class for intersection tools.
    /// </summary>
    public class ExportBlocksToSvg : IExtensionApplication
    {
        #region IExtensionApplication members
        public void Initialize()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            doc.Editor.WriteMessage("\n-> Export all blocks as SVG: EXPORTBLOCKSTOSVG");
        }

        public void Terminate()
        {
        }
        #endregion

        [CommandMethod("EXPORTBLOCKSTOSVG")]
        //Does not update dynamic blocks
        public static void exportblockstosvg()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            try
            {
                using (Transaction tx = localDb.TransactionManager.StartTransaction())
                //using (Database symbolerDB = new Database(false, true))
                {
                    try
                    {
                        System.Data.DataTable fjvKomponenter = CsvReader.ReadCsvToDataTable(
                                            @"X:\AutoCAD DRI - 01 Civil 3D\FJV Komponenter.csv", "FjvKomponenter");

                        //symbolerDB.ReadDwgFile(@"X:\0371-1158 - Gentofte Fase 4 - Dokumenter\01 Intern\" +
                        //                       @"02 Tegninger\01 Autocad\Autocad\01 Views\0.0 Fælles\Symboler.dwg",
                        //                       System.IO.FileShare.Read, true, "");

                        ObjectIdCollection ids = new ObjectIdCollection();

                        BlockTable bt = tx.GetObject(localDb.BlockTableId, OpenMode.ForRead) as BlockTable;

                        BlockTableRecord mSpc = (BlockTableRecord)tx.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                        foreach (oid Oid in bt)
                        {
                            BlockTableRecord btr = tx.GetObject(Oid, OpenMode.ForWrite) as BlockTableRecord;

                            if (ReadStringParameterFromDataTable(btr.Name, fjvKomponenter, "Navn", 0) != null)// &&
                                //btr.Name == "DN32 90gr twin")
                            {
                                Extents3d bbox = new Extents3d();
                                bbox.AddBlockExtents(btr);
                                prdDbg(bbox.MaxPoint.ToString());
                                prdDbg(bbox.MinPoint.ToString());

                                //int upscale = 1000;

                                float width = Convert.ToSingle(Math.Abs(bbox.MaxPoint.X - bbox.MinPoint.X)); //* upscale);
                                float height = Convert.ToSingle(Math.Abs(bbox.MaxPoint.Y - bbox.MinPoint.Y)); //* upscale);

                                Svg.SvgDocument svg = new Svg.SvgDocument()
                                {
                                    Width = width,
                                    Height = height,
                                    ViewBox = new Svg.SvgViewBox(Convert.ToSingle(bbox.MinPoint.X),
                                                                 Convert.ToSingle(bbox.MinPoint.Y),
                                                                 width, height)
                                };

                                var group = new Svg.SvgGroup();
                                svg.Children.Add(group);
                                //Matrix3d transform = (btr.GetBlockReferenceIds(true, true)[0]
                                //                        .Go<BlockReference>(tx) as BlockReference).BlockTransform;
                                //prdDbg(transform.ToString());
                                //WCS ORIGO transform matrix
                                double[] dMatrix = new double[16] { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 };
                                Matrix3d transform = new Matrix3d(dMatrix);

                                DrawOrDiscardEntity(btr, tx, mSpc, transform, group);//, upscale);

                                svg.Write(@"X:\AutoCAD DRI - 01 Civil 3D\Svg\" + btr.Name + ".svg");
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        tx.Abort();
                        ed.WriteMessage(ex.Message);
                        throw;
                    }

                    tx.Commit();
                };
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage(ex.Message);
            }
        }

        public static void DrawOrDiscardEntity(BlockTableRecord btr, Transaction tx,
                                               BlockTableRecord mSpc, Matrix3d transform, Svg.SvgGroup group)//, int upscale)
        {
            foreach (oid oid in btr)
            {
                switch (oid.ObjectClass.Name)
                {
                    case "AcDbLine":
                        prdDbg(oid.ObjectClass.Name);
                        Line line = oid.Go<Line>(tx);
                        using (Line newLine = new Line(line.StartPoint, line.EndPoint))
                        {
                            newLine.TransformBy(transform);
                            group.Children.Add(new Svg.SvgLine
                            {
                                StartX = Convert.ToSingle(newLine.StartPoint.X),
                                StartY = Convert.ToSingle(newLine.StartPoint.Y),
                                EndX = Convert.ToSingle(newLine.EndPoint.X),
                                EndY = Convert.ToSingle(newLine.EndPoint.Y),
                                StrokeWidth = Convert.ToSingle(0.1),
                                Stroke = new Svg.SvgColourServer(System.Drawing.Color.Black)

                            });
                            //mSpc.AppendEntity(newLine);
                            //tx.AddNewlyCreatedDBObject(newLine, true);
                        }
                        break;
                    case "AcDbCircle":
                        prdDbg(oid.ObjectClass.Name);
                        Circle circle = oid.Go<Circle>(tx);
                        using (Circle newCircle = new Circle())
                        {
                            newCircle.SetDatabaseDefaults();
                            newCircle.Center = circle.Center;
                            newCircle.Radius = circle.Radius;
                            newCircle.TransformBy(transform);
                            group.Children.Add(new Svg.SvgCircle
                            {
                                CenterX = Convert.ToSingle(newCircle.Center.X),
                                CenterY = Convert.ToSingle(newCircle.Center.Y),
                                Radius = Convert.ToSingle(newCircle.Radius),
                                Fill = new Svg.SvgColourServer(System.Drawing.Color.Black),
                                Stroke = new Svg.SvgColourServer(System.Drawing.Color.Black),
                                StrokeWidth = Convert.ToSingle(0.055)
                            });
                            //mSpc.AppendEntity(newCircle);
                            //tx.AddNewlyCreatedDBObject(newCircle, true);
                        }
                        break;
                    case "AcDbBlockReference":
                        DrawOrDiscardEntity(tx.GetObject(oid, OpenMode.ForRead) as BlockReference, tx, mSpc, group);//, upscale);
                        break;
                    default:
                        prdDbg("Not implemented: " + oid.ObjectClass.Name);
                        break;
                }
            }
        }
        public static void DrawOrDiscardEntity(BlockReference br, Transaction tx, BlockTableRecord mSpc, Svg.SvgGroup group)//, int upscale)
        {
            BlockTableRecord btr = tx.GetObject(br.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
            DrawOrDiscardEntity(btr, tx, mSpc, br.BlockTransform, group);//, upscale);
        }

    }
}