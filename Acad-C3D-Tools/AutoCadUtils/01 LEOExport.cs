using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
                List<Polyline> plines = db.ListOfType<Polyline>(tx)
                                          .Where(x => x.Layer == "RØR INSTRUMENT SIGNAL")
                                          .ToList();

                foreach (Polyline pline in plines)
                {
                    Dreambuild.AutoCAD.Modify.Explode(pline.ObjectId);
                }

                List<Polyline> rumPolys = db.ListOfType<Polyline>(tx)
                                            .Where(x => x.Layer == "RUM DELER")
                                            .ToList();

                List<(string, string)> list = new List<(string, string)>();

                foreach (Polyline pline in rumPolys)
                {
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
                            }
                        }
                    }

                    BlockReference br = blocks.Where(x => x.Name == "rumnummer").FirstOrDefault();
                    if (br == null) continue;
                    string value = br.GetBlockAttribute("ROOMNO");
                    editor.WriteMessage($"\nRoom nr.: {value}");
                }
            }
        }
    }

    public static class ExtensionMethods
    {
        public static T Go<T>(this ObjectId Oid, Transaction tx, OpenMode openMode = OpenMode.ForRead) where T : Autodesk.AutoCAD.DatabaseServices.Entity
        {
            return (T)tx.GetObject(Oid, openMode, false);
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
            foreach (ObjectId objectId in modelSpace)
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

        public static List<T> ListOfType<T>(this Database database, Transaction tr) where T : Autodesk.AutoCAD.DatabaseServices.Entity
        {
            //using (var tr = database.TransactionManager.StartTransaction())
            //{

            //Init the list of the objects
            List<T> objs = new List<T>();

            // Get the block table for the current database
            var blockTable = (BlockTable)tr.GetObject(database.BlockTableId, OpenMode.ForRead);

            // Get the model space block table record
            var modelSpace = (BlockTableRecord)tr.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead);

            RXClass theClass = RXObject.GetClass(typeof(T));

            // Loop through the entities in model space
            foreach (ObjectId objectId in modelSpace)
            {
                // Look for entities of the correct type
                if (objectId.ObjectClass.IsDerivedFrom(theClass))
                {
                    var entity = (T)tr.GetObject(objectId, OpenMode.ForRead);
                    objs.Add(entity);
                }
            }
            return objs;
            //tr.Commit();
            //}
        }

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

