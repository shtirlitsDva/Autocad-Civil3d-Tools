using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using xel = Microsoft.Office.Interop.Excel;

using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

using Dreambuild.AutoCAD;

namespace AutoCadUtils
{
    public class ExportLeoComponentsInRooms : IExtensionApplication
    {
        public void Initialize()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            doc.Editor.WriteMessage("\n-> Export LEO components to EXCEL: EXPORTLEO");
        }

        public void Terminate()
        {
        }

        [CommandMethod("exportleo")]
        public void exportleo()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database db = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;


            using (Transaction tx = db.TransactionManager.StartTransaction())
            {
                var plines = QuickSelection.SelectAll("LWPOLYLINE").QWhere(x => x.Layer == "RØR INSTRUMENT SIGNAL").ToList();

                editor.WriteMessage($"\nPlines collected {plines.Count}");

                plines.QOpenForWrite<Polyline>(listToExplode =>
                {
                    foreach (var poly in listToExplode) Modify.Explode(poly.ObjectId);
                });

                var rumPolyIds = QuickSelection.SelectAll("LWPOLYLINE").QWhere(x => x.Layer == "RUM DELER").ToList();

                editor.WriteMessage($"\nCollected {rumPolyIds.Count} rum polylines.");

                rumPolyIds.QOpenForWrite<Polyline>(listToClean =>
                {
                    foreach (var poly in listToClean) Algorithms.PolyClean_RemoveDuplicatedVertex(poly);
                });

                List<(string, string)> tagRoomlist = new List<(string Tag, string RoomNr)>();

                int i = 0;

                List<BlockReference> AllBlocks = new List<BlockReference>();

                var rumPolys = rumPolyIds.QOpenForRead<Polyline>();

                foreach (Polyline pline in rumPolys)
                {
                    i++;
                    editor.WriteMessage($"\nProcessing polyline number {i}.");

                    List<BlockReference> blocks = new List<BlockReference>();

                    PromptSelectionResult selection =
                        editor.SelectByPolyline(pline, ExtensionMethods.PolygonSelectionMode.Window, new TypedValue(0, "INSERT"));

                    if (selection.Status == PromptStatus.OK)
                    {
                        SelectionSet set = selection.Value;
                        foreach (SelectedObject selObj in set)
                        {
                            if (selObj != null)
                            {
                                BlockReference block = tx.GetObject(selObj.ObjectId, OpenMode.ForRead) as BlockReference;
                                blocks.Add(block);
                                AllBlocks.Add(block);
                            }
                        }
                    }
                    else
                    {
                        editor.WriteMessage($"\nPolyline number {i} failed the selection!");
                    }

                    BlockReference roomNumberBlock = blocks.Where(x => x.Name == "rumnummer").FirstOrDefault();
                    if (roomNumberBlock == null) continue;
                    string roomNumber = roomNumberBlock.GetBlockAttribute("ROOMNO");
                    editor.WriteMessage($"\nRoom nr.: {roomNumber}");

                    foreach (BlockReference br in blocks)
                    {
                        string tagValue = string.Empty;
                        var attrs = br.GetBlockAttributes();
                        if (attrs.ContainsKey("TEXT1"))
                        {
                            tagValue = attrs["TEXT1"];
                        }
                        else if (attrs.ContainsKey("TAG"))
                        {
                            tagValue = attrs["TAG"];
                        }
                        else continue;

                        if (!string.IsNullOrEmpty(roomNumber))
                        {
                            (string, string) pair = (tagValue, roomNumber);
                            tagRoomlist.Add(pair); 
                        }
                    }
                }

                //Sort the pairs list
                tagRoomlist = tagRoomlist.OrderByDescending(x => x.Item1).ToList();

                //Export to excel
                xel.Application excel = new xel.Application();
                if (null == excel) throw new System.Exception("Failed to start EXCEL!");
                excel.Visible = true;
                xel.Workbook workbook = excel.Workbooks.Add(Missing.Value);
                xel.Worksheet worksheet;
                worksheet = excel.ActiveSheet as xel.Worksheet;
                worksheet.Name = "LeoExport";
                worksheet.Columns.ColumnWidth = 15;

                int row = 1;
                int col = 1;

                foreach (var pair in tagRoomlist)
                {
                    worksheet.Cells[row, col] = pair.Item1;
                    worksheet.Cells[row, col+1] = pair.Item2;
                    row++;
                }
            }
        }
    }

    public static class ExtensionMethods
    {
        public enum PolygonSelectionMode { Crossing, Window }

        public static PromptSelectionResult SelectByPolyline(this Editor ed, Polyline pline, PolygonSelectionMode mode, params TypedValue[] filter)
        {
            if (ed == null) throw new ArgumentNullException("ed");
            if (pline == null) throw new ArgumentNullException("pline");
            Matrix3d wcs = ed.CurrentUserCoordinateSystem.Inverse();
            Point3dCollection polygon = new Point3dCollection();
            for (int i = 0; i < pline.NumberOfVertices; i++)
            {
                polygon.Add(pline.GetPoint3dAt(i).TransformBy(wcs));
            }
            PromptSelectionResult result;
            using (ViewTableRecord curView = ed.GetCurrentView())
            {
                ed.Zoom(pline.GeometricExtents);
                if (mode == PolygonSelectionMode.Crossing)
                    result = ed.SelectCrossingPolygon(polygon, new SelectionFilter(filter));
                else
                    result = ed.SelectWindowPolygon(polygon, new SelectionFilter(filter));
                ed.SetCurrentView(curView);
            }
            return result;
        }

        public static void Zoom(this Editor ed, Extents3d extents)
        {
            if (ed == null) throw new ArgumentNullException("ed");
            using (ViewTableRecord view = ed.GetCurrentView())
            {
                Matrix3d worldToEye =
                    (Matrix3d.Rotation(-view.ViewTwist, view.ViewDirection, view.Target) *
                    Matrix3d.Displacement(view.Target - Point3d.Origin) *
                    Matrix3d.PlaneToWorld(view.ViewDirection))
                    .Inverse();
                extents.TransformBy(worldToEye);
                view.Width = extents.MaxPoint.X - extents.MinPoint.X;
                view.Height = extents.MaxPoint.Y - extents.MinPoint.Y;
                view.CenterPoint = new Point2d(
                    (extents.MaxPoint.X + extents.MinPoint.X) / 2.0,
                    (extents.MaxPoint.Y + extents.MinPoint.Y) / 2.0);
                ed.SetCurrentView(view);
            }
        }
    }
}

