using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using Polyline = Autodesk.AutoCAD.DatabaseServices.Polyline;

namespace IntersectUtilities.MPE.PipePlanDE;

/// <summary>
/// Transient preview of an in-progress German pipe run: the routing centreline
/// plus the two mitered supply/return bands. Refreshed after each committed point
/// during PDDRAW. Mirrors the transient lifecycle of <c>PipePlanPreviewManager</c>.
/// </summary>
internal sealed class PipePlanDEPreviewManager : IDisposable
{
    private readonly IntegerCollection _viewports = [];
    private readonly List<Entity> _entities = [];

    public void Dispose() => Clear();

    public void Clear()
    {
        foreach (Entity entity in _entities)
        {
            try
            {
                TransientManager.CurrentTransientManager.EraseTransient(entity, _viewports);
            }
            catch
            {
                // Best effort cleanup for transient preview entities.
            }

            entity.Dispose();
        }

        _entities.Clear();
    }

    public void Show(
        IReadOnlyList<Point3d> centerline,
        PipePlanDEParameters parameters,
        bool flip,
        PipePlanDESnapMode snapMode = PipePlanDESnapMode.None,
        double indicatorSize = 0.0)
    {
        Clear();
        if (centerline.Count < 2)
        {
            return;
        }

        AddTransient(CreatePolyline(centerline, 0.0, 8));

        if (PipePlanDEOffsetBuilder.TryBuild(centerline, parameters.PipeSpacing, out List<Point3d> left, out List<Point3d> right, out _))
        {
            // FREM (supply) is red, RETUR is blue. Flip swaps which physical side
            // each one is, so the preview colours swap with it.
            (List<Point3d> frem, List<Point3d> retur) = flip ? (right, left) : (left, right);
            AddTransient(CreatePolyline(frem, parameters.D, 1));
            AddTransient(CreatePolyline(retur, parameters.D, 5));
        }

        if (snapMode != PipePlanDESnapMode.None && indicatorSize > 0.0 && centerline.Count >= 3)
        {
            AddSnapIndicator(centerline, snapMode, indicatorSize);
        }
    }

    // The last three preview points are: previous committed, anchor (last committed),
    // and the moving candidate. The indicator sits at the anchor — the joint being snapped.
    private void AddSnapIndicator(IReadOnlyList<Point3d> centerline, PipePlanDESnapMode snapMode, double size)
    {
        Point3d previous = centerline[^3];
        Point3d anchor = centerline[^2];
        Point3d candidate = centerline[^1];

        if (snapMode == PipePlanDESnapMode.Perpendicular)
        {
            Vector3d legToPrev = SafeUnit(previous - anchor);
            Vector3d legToCandidate = SafeUnit(candidate - anchor);
            if (legToPrev.Length < 0.5 || legToCandidate.Length < 0.5)
            {
                return;
            }

            // Little square corner: P1 -> P2 -> P3 forms the right-angle tick (cyan).
            Point3d p1 = anchor + (legToPrev * size);
            Point3d p2 = anchor + (legToPrev * size) + (legToCandidate * size);
            Point3d p3 = anchor + (legToCandidate * size);
            AddTransient(CreateIndicatorPolyline([p1, p2, p3], 4));
            return;
        }

        // Straight: an extended alignment guide line through the joint (green).
        Vector3d direction = SafeUnit(candidate - anchor);
        if (direction.Length < 0.5)
        {
            return;
        }

        Point3d start = anchor - (direction * size * 6.0);
        Point3d end = candidate + (direction * size * 3.0);
        AddTransient(CreateIndicatorPolyline([start, end], 3));
    }

    private static Vector3d SafeUnit(Vector3d vector)
    {
        double length = vector.Length;
        return length < 1e-9 ? new Vector3d(0.0, 0.0, 0.0) : vector / length;
    }

    private static Polyline CreateIndicatorPolyline(IReadOnlyList<Point3d> points, short colorIndex)
    {
        Polyline polyline = new();
        for (int i = 0; i < points.Count; i++)
        {
            polyline.AddVertexAt(i, new Point2d(points[i].X, points[i].Y), 0.0, 0.0, 0.0);
        }

        polyline.Elevation = points[0].Z;
        polyline.Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex);
        polyline.LineWeight = LineWeight.LineWeight040;
        return polyline;
    }

    // ~55% transparent so the overlapping supply/return bands and the centreline
    // stay easy to tell apart in the preview. Alpha 0 = clear, 255 = opaque.
    private const byte PreviewAlpha = 115;

    private static Polyline CreatePolyline(IReadOnlyList<Point3d> points, double width, short colorIndex)
    {
        Polyline polyline = new();
        for (int i = 0; i < points.Count; i++)
        {
            polyline.AddVertexAt(i, new Point2d(points[i].X, points[i].Y), 0.0, width, width);
        }

        polyline.Elevation = points[0].Z;
        polyline.Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex);
        polyline.Transparency = new Transparency(PreviewAlpha);
        polyline.LineWeight = LineWeight.LineWeight050;
        return polyline;
    }

    private void AddTransient(Entity entity)
    {
        _entities.Add(entity);
        TransientManager.CurrentTransientManager.AddTransient(
            entity,
            TransientDrawingMode.DirectShortTerm,
            128,
            _viewports);
    }
}
