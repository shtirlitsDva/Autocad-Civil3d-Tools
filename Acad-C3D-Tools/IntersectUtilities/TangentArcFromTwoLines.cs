using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

using IntersectUtilities.ArcConstruction;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace IntersectUtilities;

public partial class Intersect
{
    private static double _lastTwoLineTangentArcLength = 2.5;

    /// <command>TANGENTARCFROMTWOLINES</command>
    /// <summary>
    /// Tegner en bue med angivet buelængde tangent til to koplanare LINE-objekter.
    /// Vælg linjerne på de ønskede sider af deres skæring. Radius beregnes fra
    /// buelængden og vinklen. Linjerne ændres ikke; deres forlængelser kan anvendes.
    /// </summary>
    /// <category>Utilities</category>
    [CommandMethod("TANGENTARCFROMTWOLINES")]
    public void TangentArcFromTwoLines()
    {
        Document doc = Application.DocumentManager.MdiActiveDocument;
        Database db = doc.Database;
        Editor ed = doc.Editor;

        // One command-boundary guard covers AutoCAD input and database API failures.
        try
        {
            var lengthOptions = new PromptDistanceOptions("\nEnter arc length")
            {
                AllowNegative = false,
                AllowZero = false,
                AllowNone = false,
                DefaultValue = _lastTwoLineTangentArcLength,
                UseDefaultValue = true
            };
            var lengthResult = ed.GetDistance(lengthOptions);
            if (lengthResult.Status != PromptStatus.OK)
                return;
            if (!double.IsFinite(lengthResult.Value) || lengthResult.Value <= 0)
            {
                ed.WriteMessage("\nArc length must be a finite, positive number.");
                return;
            }
            _lastTwoLineTangentArcLength = lengthResult.Value;

            var firstOptions = new PromptEntityOptions("\nSelect first line on the desired side of the corner");
            firstOptions.SetRejectMessage("\nSelect a LINE object.");
            firstOptions.AddAllowedClass(typeof(Line), exactMatch: true);
            var firstResult = ed.GetEntity(firstOptions);
            if (firstResult.Status != PromptStatus.OK)
                return;
            var firstPick = firstResult.PickedPoint.TransformBy(ed.CurrentUserCoordinateSystem);
            using var firstView = ed.GetCurrentView();

            var secondOptions = new PromptEntityOptions("\nSelect second line on the desired side of the corner");
            secondOptions.SetRejectMessage("\nSelect a LINE object.");
            secondOptions.AddAllowedClass(typeof(Line), exactMatch: true);
            var secondResult = ed.GetEntity(secondOptions);
            if (secondResult.Status != PromptStatus.OK)
                return;
            if (firstResult.ObjectId == secondResult.ObjectId)
            {
                ed.WriteMessage("\nSelect two different lines.");
                return;
            }
            var secondPick = secondResult.PickedPoint.TransformBy(ed.CurrentUserCoordinateSystem);
            using var secondView = ed.GetCurrentView();

            using var tr = db.TransactionManager.StartTransaction();
            var firstLine = (Line)tr.GetObject(firstResult.ObjectId, OpenMode.ForRead);
            var secondLine = (Line)tr.GetObject(secondResult.ObjectId, OpenMode.ForRead);

            // Entity picks lie on the UCS plane. Project along each pick's view direction
            // to resolve the side correctly for elevated lines and oblique views.
            var firstOnLine = firstLine.GetClosestPointTo(firstPick, firstView.ViewDirection, false);
            var secondOnLine = secondLine.GetClosestPointTo(secondPick, secondView.ViewDirection, false);
            var result = TangentArcGeometry.Solve(
                ToArcVector(firstLine.StartPoint), ToArcVector(firstLine.EndPoint), ToArcVector(firstOnLine),
                ToArcVector(secondLine.StartPoint), ToArcVector(secondLine.EndPoint), ToArcVector(secondOnLine),
                lengthResult.Value);

            result.Match(
                geometry =>
                {
                    // Use the calculated direction, avoiding loss of orthogonality from
                    // subtracting large survey coordinates to recover a small radius.
                    var xAxis = geometry.StartDirection;
                    var yAxis = geometry.Normal.Cross(xAxis);
                    var toWorld = Matrix3d.AlignCoordinateSystem(
                        Point3d.Origin, Vector3d.XAxis, Vector3d.YAxis, Vector3d.ZAxis,
                        ToArcPoint(geometry.Center), ToArcDirection(xAxis),
                        ToArcDirection(yAxis), ToArcDirection(geometry.Normal));
                    using var arc = new Arc(Point3d.Origin, geometry.Radius, 0, geometry.Sweep);
                    arc.SetDatabaseDefaults(db);
                    arc.TransformBy(toWorld);

                    var space = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                    space.AppendEntity(arc);
                    tr.AddNewlyCreatedDBObject(arc, true);
                    tr.Commit();

                    ed.WriteMessage($"\nTangent arc created: length = {lengthResult.Value:G8}, radius = {geometry.Radius:G8}.");
                    if (geometry.UsesExtensions)
                        ed.WriteMessage("\nTangency lies on a line extension. The original lines were left unchanged.");
                    return true;
                },
                message =>
                {
                    ed.WriteMessage($"\n{message}");
                    return false;
                });
        }
        catch (System.Exception ex)
        {
            ed.WriteMessage($"\nUnable to create tangent arc: {ex.Message}");
        }
    }

    private static ArcVector ToArcVector(Point3d point) => new(point.X, point.Y, point.Z);
    private static Point3d ToArcPoint(ArcVector point) => new(point.X, point.Y, point.Z);
    private static Vector3d ToArcDirection(ArcVector vector) => new(vector.X, vector.Y, vector.Z);
}
