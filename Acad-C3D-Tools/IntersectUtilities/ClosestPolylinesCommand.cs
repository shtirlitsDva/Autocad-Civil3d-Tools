using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

using IntersectUtilities.PolylineProximity;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace IntersectUtilities;

public partial class Intersect
{
    private const string ClosestPolylinesLayer = "0-REFERENCELINE";

    /// <command>CLOSESTPLINES</command>
    /// <summary>
    /// Finds the shortest 3D distance between two polyline paths, including line and
    /// arc interiors. Creates a connecting LINE on 0-REFERENCELINE and zooms to it.
    /// Touching/intersecting paths are marked with a POINT instead of a zero-length line.
    /// </summary>
    /// <category>Utilities</category>
    [CommandMethod("CLOSESTPLINES", CommandFlags.Modal | CommandFlags.UsePickSet)]
    public void ClosestPolylines()
    {
        Document doc = Application.DocumentManager.MdiActiveDocument;
        Database db = doc.Database;
        Editor ed = doc.Editor;

        try
        {
            var firstOptions = new PromptEntityOptions("\nSelect first polyline");
            firstOptions.SetRejectMessage("\nSelect a PLINE (lightweight polyline).");
            firstOptions.AddAllowedClass(typeof(Polyline), exactMatch: true);
            var firstResult = ed.GetEntity(firstOptions);
            if (firstResult.Status != PromptStatus.OK)
                return;

            var secondOptions = new PromptEntityOptions("\nSelect second polyline");
            secondOptions.SetRejectMessage("\nSelect a PLINE (lightweight polyline).");
            secondOptions.AddAllowedClass(typeof(Polyline), exactMatch: true);
            var secondResult = ed.GetEntity(secondOptions);
            if (secondResult.Status != PromptStatus.OK)
                return;
            if (firstResult.ObjectId == secondResult.ObjectId)
            {
                ed.WriteMessage("\nSelect two different polylines.");
                return;
            }

            using var tr = db.TransactionManager.StartTransaction();
            var first = (Polyline)tr.GetObject(firstResult.ObjectId, OpenMode.ForRead);
            var second = (Polyline)tr.GetObject(secondResult.ObjectId, OpenMode.ForRead);
            if (first.NumberOfVertices == 0 || second.NumberOfVertices == 0)
            {
                ed.WriteMessage("\nBoth polylines must contain at least one vertex.");
                return;
            }

            var closest = PolylineClosestPoints.Find(first, second);
            ObjectId layerId = GetClosestPolylinesLayer(db, tr);
            bool touching = closest.Distance <= Tolerance.Global.EqualPoint;
            using Entity reference = touching
                ? new DBPoint(closest.First)
                : new Line(closest.First, closest.Second);
            reference.SetDatabaseDefaults(db);
            reference.LayerId = layerId;
            var space = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
            ObjectId referenceId = space.AppendEntity(reference);
            tr.AddNewlyCreatedDBObject(reference, true);
            tr.Commit();

            ed.WriteMessage($"\nShortest path distance: {closest.Distance:G12} drawing units.");
            ed.WriteMessage($"\nFirst point: ({closest.First.X:G12}, {closest.First.Y:G12}, {closest.First.Z:G12}).");
            ed.WriteMessage($"\nSecond point: ({closest.Second.X:G12}, {closest.Second.Y:G12}, {closest.Second.Z:G12}).");
            if (touching)
                ed.WriteMessage("\nPaths touch within AutoCAD tolerance; a POINT marks the location.");

            ZoomToClosestPolylines(ed, closest);
            ed.SetImpliedSelection(new[] { referenceId });
        }
        catch (System.Exception ex)
        {
            ed.WriteMessage($"\nUnable to complete CLOSESTPLINES: {ex.Message}");
        }
    }

    private static ObjectId GetClosestPolylinesLayer(Database db, Transaction tr)
    {
        var layers = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
        if (layers.Has(ClosestPolylinesLayer))
        {
            ObjectId existingId = layers[ClosestPolylinesLayer];
            var existing = (LayerTableRecord)tr.GetObject(existingId, OpenMode.ForWrite);
            if (existing.IsOff)
                existing.IsOff = false;
            if (existing.IsFrozen)
                existing.IsFrozen = false;
            return existingId;
        }

        layers.UpgradeOpen();
        using var layer = new LayerTableRecord
        {
            Name = ClosestPolylinesLayer,
            Color = Color.FromColorIndex(ColorMethod.ByAci, 2)
        };
        ObjectId id = layers.Add(layer);
        tr.AddNewlyCreatedDBObject(layer, true);
        return id;
    }

    internal static void ZoomToClosestPolylines(Editor ed, ClosestPolylinePair closest)
    {
        using var view = ed.GetCurrentView();
        double aspect = view.Width / view.Height;
        view.Target = closest.First + (closest.Second - closest.First) * 0.5;
        var worldToEye = Matrix3d.WorldToPlane(view.ViewDirection)
            * Matrix3d.Displacement(Point3d.Origin - view.Target)
            * Matrix3d.Rotation(view.ViewTwist, view.ViewDirection, view.Target);
        Point3d first = closest.First.TransformBy(worldToEye);
        Point3d second = closest.Second.TransformBy(worldToEye);

        // Preserve the view direction, twist and aspect; give even a zero gap useful context.
        view.Height = System.Math.Max(10,
            1.5 * System.Math.Max(System.Math.Abs(first.Y - second.Y),
                System.Math.Abs(first.X - second.X) / aspect));
        view.Width = view.Height * aspect;
        view.CenterPoint = Point2d.Origin;
        ed.SetCurrentView(view);
    }
}
