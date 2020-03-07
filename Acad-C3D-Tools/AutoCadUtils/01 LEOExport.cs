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
            doc.Editor.WriteMessage("\n-> Export LEO components to EXCEL: EXPORTLEO" +
                                    "\n-> Set tags by sequential selection: SETTAGSSEQ" +
                                    "\n-> Detect and select duplicate tags: DETECTDUPES");
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

                List<(string Tag, string RoomNr)> tagRoomlist = new List<(string Tag, string RoomNr)>();

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
                            tagRoomlist.Add((tagValue, roomNumber));
                        }
                    }
                }

                //Sort the pairs list
                tagRoomlist = tagRoomlist.OrderBy(x => x.Tag).ToList();

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
                    worksheet.Rows[row].Cells[col] = pair.Tag;
                    worksheet.Rows[row].Cells[col + 1] = pair.RoomNr;
                    //worksheet.Cells[row, col] = pair.Item1;
                    //worksheet.Cells[row, col+1] = pair.Item2;
                    row++;
                }
            }
        }

        [CommandMethod("settagsseq")]
        public void settagsseq()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database db = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;

            //Total length of tag numbering part
            //Leo is 3 current
            const int length = 3;

            string compType = Interaction.GetString("Set component type for tag:");
            int number = Interaction.GetInteger("Set start number for tags:");
            editor.WriteMessage("\nContinue with selecting blocks sequentially.");

            bool Continue = true;

            do
            {
                var id = Interaction.GetEntity("", typeof(BlockReference));
                if (id == ObjectId.Null) Continue = false;
                else
                {
                    //Prepare tag
                    string tag = compType + number.ToString("D" + length);

                    using (Transaction tx = db.TransactionManager.StartTransaction())
                    {
                        id.QOpenForWrite<BlockReference>(br =>
                        {
                            var attrs = br.GetBlockAttributeIds();
                            if (attrs.ContainsKey("TEXT1"))
                            {
                                attrs["TEXT1"].QOpenForWrite<AttributeReference>(ar =>
                                {
                                    ar.TextString = tag;
                                });
                                number++;
                            }
                            else if (attrs.ContainsKey("TAG"))
                            {
                                attrs["TAG"].QOpenForWrite<AttributeReference>(ar =>
                                {
                                    ar.TextString = tag;
                                });
                                number++;
                            }
                        });

                        tx.Commit();
                    }
                }

            } while (Continue);
        }

        [CommandMethod("detectdupes")]
        public void detectdupes()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database db = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;

            var ids = QuickSelection.SelectAll("INSERT").QWhere(x => x.Layer == "SYMBOL");
            editor.WriteMessage($"\nSymbol blocks found in drawing: {ids.Count()}.");

            List<(string Tag, ObjectId id)> tagIdPairs = new List<(string Tag, ObjectId id)>();

            var blocks = ids.QOpenForRead<BlockReference>();

            //int count = 1;
            foreach (BlockReference br in blocks)
            {
                var attrs = br.GetBlockAttributes();

                if (attrs.ContainsKey("TEXT1"))
                {
                    tagIdPairs.Add((attrs["TEXT1"], br.ObjectId));
                    //editor.WriteMessage($"\n{count}: {attrs["TEXT1"]}");
                    //count++;
                }
                else if (attrs.ContainsKey("TAG"))
                {
                    tagIdPairs.Add((attrs["TAG"], br.ObjectId));
                    //editor.WriteMessage($"\n{count}: {attrs["TAG"]}");
                    //count++;
                }
                else
                {
                    //editor.WriteMessage($"\n{count}: NON-TAGGED");
                    //count++;
                }
                    
            }
            
            var groupByTag = tagIdPairs.GroupBy(x => x.Tag);

            var groupsWithDuplicates = groupByTag.Where(x => x.Count() > 1);

            if (groupsWithDuplicates.Count() < 1)
            {
                editor.WriteMessage("\nNo duplicates found!");
            }
            else
            {
                var groupWithDuplicates = groupsWithDuplicates.FirstOrDefault();
                editor.WriteMessage($"\nDuplicate tag: {groupWithDuplicates.Key}.");
                List<ObjectId> duplicateIds = new List<ObjectId>(groupWithDuplicates.Count());
                foreach (var dupe in groupWithDuplicates)
                {
                    duplicateIds.Add(dupe.id);
                    //editor.WriteMessage($"\n{dupe.id.ToString()}");
                }
                Autodesk.AutoCAD.Internal.Utils.SelectObjects(duplicateIds.ToArray());
                Interaction.ZoomObjects(duplicateIds);
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

