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

                        BlockTable bt = tx.GetObject(localDb.BlockTableId, OpenMode.ForRead) as BlockTable;

                        foreach (oid Oid in bt)
                        {
                            prdDbg(Oid.ObjectClass.Name);
                            BlockTableRecord btr = tx.GetObject(Oid, OpenMode.ForRead) as BlockTableRecord;

                            if (ReadStringParameterFromDataTable(btr.Name, fjvKomponenter, "Navn", 0) != null)
                            {
                                Extents3d bbox = new Extents3d();
                                bbox.AddBlockExtents(btr);
                                prdDbg(bbox.MaxPoint.ToString());
                                prdDbg(bbox.MinPoint.ToString());

                                float width = ts(Math.Abs(bbox.MaxPoint.X - bbox.MinPoint.X));
                                float height = ts(Math.Abs(bbox.MaxPoint.Y - bbox.MinPoint.Y));

                                Svg.SvgDocument svg = new Svg.SvgDocument()
                                {
                                    Width = width,
                                    Height = height,
                                    ViewBox = new Svg.SvgViewBox(ts(bbox.MinPoint.X),
                                                                 ts(bbox.MinPoint.Y),
                                                                 width, height)
                                };

                                var group = new Svg.SvgGroup();
                                svg.Children.Add(group);
                                //WCS ORIGO transform matrix
                                double[] dMatrix = new double[16] { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 };
                                Matrix3d transform = new Matrix3d(dMatrix);
                                DrawOrDiscardEntity(btr, tx, transform, group);//, upscale);

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
                                               Matrix3d transform, Svg.SvgGroup group)//, int upscale)
        {
            foreach (oid oid in btr)
            {
                switch (oid.ObjectClass.Name)
                {
                    case "AcDbLine":
                        //prdDbg(oid.ObjectClass.Name);
                        Line line = oid.Go<Line>(tx);
                        using (Line newLine = new Line(line.StartPoint, line.EndPoint))
                        {
                            newLine.TransformBy(transform);
                            group.Children.Add(new Svg.SvgLine
                            {
                                StartX = ts(newLine.StartPoint.X),
                                StartY = ts(newLine.StartPoint.Y),
                                EndX = ts(newLine.EndPoint.X),
                                EndY = ts(newLine.EndPoint.Y),
                                StrokeWidth = ts(0.1),
                                Stroke = new Svg.SvgColourServer(System.Drawing.Color.Black)

                            });
                            //mSpc.AppendEntity(newLine);
                            //tx.AddNewlyCreatedDBObject(newLine, true);
                        }
                        break;
                    case "AcDbPolyline":
                        //prdDbg(oid.ObjectClass.Name);
                        Polyline pline = oid.Go<Polyline>(tx);
                        using (Polyline newPline = new Polyline(pline.NumberOfVertices))
                        {
                            for (int i = 0; i < pline.NumberOfVertices; i++)
                            {
                                newPline.AddVertexAt(i, pline.GetPoint2dAt(i), 0, 0, 0);
                            }
                            newPline.TransformBy(transform);
                            SvgPointCollection pcol = new SvgPointCollection();
                            for (int i = 0; i < newPline.NumberOfVertices; i++)
                            {
                                Point2d p2d = newPline.GetPoint2dAt(i);
                                pcol.Add(ts(p2d.X));
                                pcol.Add(ts(p2d.Y));
                            }
                            SvgPolyline sPline = new SvgPolyline();
                            sPline.Points = pcol;
                            if (pline.NumberOfVertices == 2)
                            {
                                sPline.StrokeWidth = ts(0.1);
                                sPline.Stroke = new SvgColourServer(System.Drawing.Color.Black);
                            }
                            else
                            {
                                sPline.Fill = new SvgColourServer(System.Drawing.Color.Black);
                            }
                            group.Children.Add(sPline);
                        }
                        break;
                    case "AcDbCircle":
                        //prdDbg(oid.ObjectClass.Name);
                        Circle circle = oid.Go<Circle>(tx);
                        using (Circle newCircle = new Circle())
                        {
                            newCircle.SetDatabaseDefaults();
                            newCircle.Center = circle.Center;
                            newCircle.Radius = circle.Radius;
                            newCircle.TransformBy(transform);
                            group.Children.Add(new Svg.SvgCircle
                            {
                                CenterX = ts(newCircle.Center.X),
                                CenterY = ts(newCircle.Center.Y),
                                Radius = ts(newCircle.Radius),
                                Fill = new Svg.SvgColourServer(System.Drawing.Color.Black),
                            });
                            //mSpc.AppendEntity(newCircle);
                            //tx.AddNewlyCreatedDBObject(newCircle, true);
                        }
                        break;
                    case "AcDbMText":
                        prdDbg(oid.ObjectClass.Name);
                        MText mText = oid.Go<MText>(tx);
                        string text = mText.Contents;
                        using (DBText newText = new DBText())
                        {
                            newText.SetDatabaseDefaults();
                            newText.TextString = text;
                            newText.Position = mText.Location;
                            newText.Rotation = mText.Rotation;
                            //newText.TransformBy(transform);
                            SvgText sText = new SvgText(newText.TextString);
                            prdDbg(ts(newText.Position.X).ToString());
                            prdDbg(ts(newText.Position.Y).ToString());
                            sText.X.Add(ts(newText.Position.X));
                            sText.Y.Add(ts(newText.Position.Y));
                            sText.FontFamily = "Arial";
                            sText.FontSize = ts(0.50);
                            prdDbg(ts(newText.Rotation * (180 / Math.PI)).ToString());
                            sText.Rotate = ts(newText.Rotation * (180 / Math.PI)).ToString();
                            sText.Fill = new SvgColourServer(System.Drawing.Color.Black);
                            group.Children.Add(sText);
                        }
                        break;
                    case "AcDbBlockReference":
                        DrawOrDiscardEntity(tx.GetObject(oid, OpenMode.ForRead) as BlockReference, tx, group);//, upscale);
                        break;
                    default:
                        //prdDbg("Not implemented: " + oid.ObjectClass.Name);
                        break;
                }
            }
        }
        public static void DrawOrDiscardEntity(BlockReference br, Transaction tx, Svg.SvgGroup group)//, int upscale)
        {
            BlockTableRecord btr = tx.GetObject(br.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
            DrawOrDiscardEntity(btr, tx, br.BlockTransform, group);//, upscale);
        }

        [CommandMethod("LISTALLCLASSNAMES")]
        //Does not update dynamic blocks
        public static void listallclassnames()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            try
            {
                using (Transaction tx = localDb.TransactionManager.StartTransaction())
                {
                    try
                    {
                        System.Data.DataTable fjvKomponenter = CsvReader.ReadCsvToDataTable(
                                            @"X:\AutoCAD DRI - 01 Civil 3D\FJV Komponenter.csv", "FjvKomponenter");

                        BlockTable bt = tx.GetObject(localDb.BlockTableId, OpenMode.ForRead) as BlockTable;

                        HashSet<string> names = new HashSet<string>();

                        foreach (oid Oid in bt)
                        {
                            BlockTableRecord btr = tx.GetObject(Oid, OpenMode.ForWrite) as BlockTableRecord;

                            if (ReadStringParameterFromDataTable(btr.Name, fjvKomponenter, "Navn", 0) != null)
                            {
                                ListClassName(btr, tx, names);
                            }
                        }

                        foreach (string name in names.OrderBy(x => x))
                        {
                            prdDbg(name);
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

        public static void ListClassName(BlockTableRecord btr, Transaction tx, HashSet<string> names)
        {
            foreach (oid oid in btr)
            {
                switch (oid.ObjectClass.Name)
                {
                    case "AcDbBlockReference":
                        ListClassName(tx.GetObject(oid, OpenMode.ForRead) as BlockReference, tx, names);
                        break;
                    default:
                        names.Add(oid.ObjectClass.Name);
                        break;
                }
            }
        }
        public static void ListClassName(BlockReference br, Transaction tx, HashSet<string> names)
        {
            BlockTableRecord btr = tx.GetObject(br.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
            ListClassName(btr, tx, names);
        }

        /// <summary>
        /// Converts double to float. Svg uses floats for all inputs.
        /// </summary>
        /// <param name="d">Input double</param>
        /// <returns>Converted float</returns>
        public static float ts(double d) => Convert.ToSingle(d);
    }
}