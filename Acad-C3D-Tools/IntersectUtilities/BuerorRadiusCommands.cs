using System.Globalization;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Application = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace IntersectUtilities;

public partial class Intersect
{
    /// <command>BUERORRADIUS</command>
    /// <summary>
    /// Tilpasser Buerør-blokkens dynamiske R-værdi til en valgt bue eller et polyline-buesegment.
    /// </summary>
    /// <category>Utilities</category>
    [CommandMethod("BUERORRADIUS", CommandFlags.Modal)]
    public void BuerorRadius()
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc is null) return;
        var ed = doc.Editor;
        var db = doc.Database;

        // One command-boundary guard handles unexpected AutoCAD API failures.
        try
        {
            var blockOptions = new PromptEntityOptions("\nVælg Buerør-blok: ")
            {
                AllowNone = true,
                AllowObjectOnLockedLayer = false
            };
            blockOptions.SetRejectMessage("\nVælg en blokreference.");
            blockOptions.AddAllowedClass(typeof(BlockReference), true);

            ObjectId blockId;
            while (true)
            {
                var selection = ed.GetEntity(blockOptions);
                if (selection.Status != PromptStatus.OK) return;
                blockId = selection.ObjectId;
                using var tx = db.TransactionManager.StartTransaction();
                var block = (BlockReference)tx.GetObject(blockId, OpenMode.ForRead);
                var accepted = BuerorRadiusMatcher.FindWritableProperty(block, "R").Match(
                    property => true,
                    message => { ed.WriteMessage("\n" + message); return false; });
                if (accepted) break;
            }

            var sourceOptions = new PromptEntityOptions("\nVælg bue eller buet polyline-segment: ")
            {
                AllowNone = true,
                AllowObjectOnLockedLayer = true
            };
            sourceOptions.SetRejectMessage("\nVælg en ARC eller en 2D-polyline.");
            sourceOptions.AddAllowedClass(typeof(Arc), true);
            sourceOptions.AddAllowedClass(typeof(Polyline), true);
            sourceOptions.AddAllowedClass(typeof(Polyline2d), true);

            double radius;
            while (true)
            {
                var selection = ed.GetEntity(sourceOptions);
                if (selection.Status != PromptStatus.OK) return;
                using var tx = db.TransactionManager.StartTransaction();
                var curve = (Curve)tx.GetObject(selection.ObjectId, OpenMode.ForRead);
                // GetEntity returns UCS coordinates; curve methods require WCS.
                var pickWcs = selection.PickedPoint.TransformBy(ed.CurrentUserCoordinateSystem);
                using var view = ed.GetCurrentView();
                var result = BuerorRadiusMatcher.ReadRadius(curve, pickWcs, view.ViewDirection, tx).Match(
                    value => (Accepted: true, Radius: value),
                    message => { ed.WriteMessage("\n" + message); return (Accepted: false, Radius: 0.0); });
                if (!result.Accepted) continue;
                radius = result.Radius;
                break;
            }

            using (var tx = db.TransactionManager.StartTransaction())
            {
                var block = (BlockReference)tx.GetObject(blockId, OpenMode.ForWrite);
                var updated = BuerorRadiusMatcher.SetAndVerify(block, "R", radius).Match(
                    value => { block.RecordGraphicsModified(true); tx.Commit(); return true; },
                    message => { ed.WriteMessage("\n" + message); return false; });
                if (!updated) return;
            }
            ed.Regen();
            ed.WriteMessage("\nBuerør R = " + radius.ToString("G17", CultureInfo.CurrentCulture));
        }
        catch (System.Exception ex)
        {
            ed.WriteMessage("\nBUERORRADIUS: " + ex.Message);
        }
    }
}

internal static class BuerorRadiusMatcher
{
    internal static BuerorRadiusResult<DynamicBlockReferenceProperty> FindWritableProperty(BlockReference block, string name)
    {
        if (!block.IsDynamicBlock)
            return BuerorRadiusResult<DynamicBlockReferenceProperty>.Failure("This block has no dynamic properties. Select a Bueror block.");
        foreach (DynamicBlockReferenceProperty property in block.DynamicBlockReferencePropertyCollection)
        {
            if (!string.Equals(property.PropertyName.Trim(), name, StringComparison.OrdinalIgnoreCase)) continue;
            if (property.ReadOnly)
                return BuerorRadiusResult<DynamicBlockReferenceProperty>.Failure($"The block's {name} property is read-only.");
            if (property.Value is not double)
                return BuerorRadiusResult<DynamicBlockReferenceProperty>.Failure($"The block's {name} property is not a numeric distance.");
            return BuerorRadiusResult<DynamicBlockReferenceProperty>.Success(property);
        }
        return BuerorRadiusResult<DynamicBlockReferenceProperty>.Failure($"This block has no dynamic {name} property. Select a Bueror block.");
    }

    // The caller owns the transaction and commits only Success.
    internal static BuerorRadiusResult<double> SetAndVerify(BlockReference block, string name, double radius) =>
        ValidateRadius(radius).Bind(value => FindWritableProperty(block, name).Bind(property =>
        {
            property.Value = value;
            return FindWritableProperty(block, name).Bind(updated =>
                updated.Value is double actual && actual.Equals(value)
                    ? BuerorRadiusResult<double>.Success(value)
                    : BuerorRadiusResult<double>.Failure(
                        $"The block definition rejected the exact radius for {name}. Check its allowed values or limits; the change was cancelled."));
        }));

    internal static BuerorRadiusResult<double> ReadRadius(Curve curve, Point3d pickWcs, Vector3d viewDirection, Transaction tx)
    {
        switch (curve)
        {
            case Arc arc:
                return ValidateRadius(arc.Radius);
            case Polyline polyline:
            {
                var segments = polyline.Closed ? polyline.NumberOfVertices : polyline.NumberOfVertices - 1;
                return PickSegment(polyline, pickWcs, viewDirection, segments, polyline.Closed).Bind(index =>
                {
                    if (polyline.GetSegmentType(index) != SegmentType.Arc)
                        return BuerorRadiusResult<double>.Failure("That segment is straight. Click a curved segment.");
                    using var segment = polyline.GetArcSegmentAt(index);
                    return ValidateRadius(segment.Radius);
                });
            }
            case Polyline2d polyline:
            {
                if (polyline.PolyType != Poly2dType.SimplePoly)
                    return BuerorRadiusResult<double>.Failure("Curve-fit and spline-fit polylines have no single arc radius. Select an arc or a normal 2D polyline.");
                var vertices = new List<Vertex2d>();
                foreach (ObjectId id in polyline)
                    vertices.Add((Vertex2d)tx.GetObject(id, OpenMode.ForRead));
                var segments = polyline.Closed ? vertices.Count : vertices.Count - 1;
                return PickSegment(polyline, pickWcs, viewDirection, segments, polyline.Closed).Bind(index =>
                {
                    var start = vertices[index];
                    var end = vertices[(index + 1) % vertices.Count];
                    var bulge = Math.Abs(start.Bulge);
                    if (bulge == 0)
                        return BuerorRadiusResult<double>.Failure("That segment is straight. Click a curved segment.");
                    // OCS preserves chord distance. This avoids squaring a large bulge.
                    return ValidateRadius(start.Position.DistanceTo(end.Position) * (0.25 / bulge + 0.25 * bulge));
                });
            }
            default:
                return BuerorRadiusResult<double>.Failure("Select an ARC or a 2D polyline.");
        }
    }

    private static BuerorRadiusResult<int> PickSegment(Curve curve, Point3d pickWcs, Vector3d viewDirection, int count, bool closed)
    {
        if (count <= 0) return BuerorRadiusResult<int>.Failure("This polyline has no segments.");
        var nearest = curve.GetClosestPointTo(pickWcs, viewDirection, false);
        var parameter = curve.GetParameterAtPoint(nearest);
        const double vertexTolerance = 1e-9;
        if (!closed && parameter <= vertexTolerance) return BuerorRadiusResult<int>.Success(0);
        if (!closed && parameter >= count - vertexTolerance) return BuerorRadiusResult<int>.Success(count - 1);
        if (Math.Abs(parameter - Math.Round(parameter)) <= vertexTolerance)
            return BuerorRadiusResult<int>.Failure("You clicked a shared vertex. Click inside the desired curved segment.");
        return BuerorRadiusResult<int>.Success(Math.Clamp((int)Math.Floor(parameter), 0, count - 1));
    }

    private static BuerorRadiusResult<double> ValidateRadius(double radius) =>
        double.IsFinite(radius) && radius > 0
            ? BuerorRadiusResult<double>.Success(radius)
            : BuerorRadiusResult<double>.Failure("The selected curve has no valid positive radius.");
}

// A closed two-case result for this .NET 8 project. Match requires both handlers,
// and each case holds only its own payload: no absent/null value and no validation exceptions.
internal abstract record BuerorRadiusResult<T>
{
    private BuerorRadiusResult() { }
    internal abstract TResult Match<TResult>(Func<T, TResult> success, Func<string, TResult> failure);
    internal BuerorRadiusResult<TNext> Bind<TNext>(Func<T, BuerorRadiusResult<TNext>> next) =>
        Match(next, BuerorRadiusResult<TNext>.Failure);
    internal static BuerorRadiusResult<T> Success(T value) => new Succeeded(value);
    internal static BuerorRadiusResult<T> Failure(string message) => new Failed(message);

    private sealed record Succeeded(T Value) : BuerorRadiusResult<T>
    {
        internal override TResult Match<TResult>(Func<T, TResult> success, Func<string, TResult> failure) => success(Value);
    }

    private sealed record Failed(string Message) : BuerorRadiusResult<T>
    {
        internal override TResult Match<TResult>(Func<T, TResult> success, Func<string, TResult> failure) => failure(Message);
    }
}
