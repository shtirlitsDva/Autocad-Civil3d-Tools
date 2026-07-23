using System.Globalization;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using IntersectUtilities.MPE.PipePlan;
using Polyline = Autodesk.AutoCAD.DatabaseServices.Polyline;

namespace IntersectUtilities.MPE.PipePlanDE;

/// <summary>
/// Transient preview of an in-progress German pipe run: the routing centreline plus the
/// two supply/return bands offset parallel to it. In the default filleted mode it mirrors
/// PPDraw's preview — straight centreline segments in green, arc (fillet) segments in a
/// darker green, an "R=" label at each bend and a marker ring at each tangent point. In
/// straight mode (the "Straight" toggle) the centreline is drawn sharp/grey with no fillet
/// visuals, matching the sharp mitered geometry that will be baked. Refreshed after each
/// committed point during PDDRAW; mirrors the transient lifecycle of PPDraw's manager.
/// </summary>
internal sealed class PipePlanDEPreviewManager : IDisposable
{
    // Straight centreline segments = green; arc (fillet) segments and their annotations =
    // darker green (matching PPDraw); infeasible run = red.
    private static readonly Color StraightColor = Color.FromRgb(0, 170, 70);
    private static readonly Color ArcColor = Color.FromRgb(0, 100, 45);
    private static readonly Color InfeasibleColor = Color.FromRgb(210, 45, 45);

    // ~55% transparent so the overlapping supply/return bands stay easy to tell apart.
    private const byte PreviewAlpha = 115;

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
        IReadOnlyList<double> rMinRadii,
        PipePlanDEParameters parameters,
        bool flip,
        bool straight,
        PipePlanDESnapMode snapMode = PipePlanDESnapMode.None,
        double indicatorSize = 0.0)
    {
        Clear();
        if (centerline.Count < 2)
        {
            return;
        }

        double elevation = centerline[0].Z;
        double half = parameters.PipeSpacing / 2.0;

        if (PipePlanDEGeometryBuilder.TryBuild(
                centerline, rMinRadii, parameters, flip, straight,
                out List<PolylineVertexData> centre,
                out List<PolylineVertexData> frem,
                out List<PolylineVertexData> retur,
                out PipePlanAnalysis? analysis,
                out _))
        {
            if (straight)
            {
                // Sharp mitered geometry: grey centreline, no fillet visuals.
                AddTransient(CreatePolyline(centre, 0.0, ByAci(8), elevation, transparent: false));
            }
            else
            {
                // Filleted: green straight base + dark-green arc overlays + R labels + markers.
                AddTransient(CreatePolyline(centre, 0.0, StraightColor, elevation, transparent: false));
                AddArcOverlays(centre, elevation);
                if (analysis is not null)
                {
                    AddRadiusLabels(analysis, half);
                    AddFilletEndpointMarkers(analysis);
                }
            }

            // FREM (supply) red, RETUR blue; flip already applied inside the builder.
            AddTransient(CreatePolyline(frem, parameters.D, ByAci(1), elevation, transparent: true));
            AddTransient(CreatePolyline(retur, parameters.D, ByAci(5), elevation, transparent: true));
        }
        else
        {
            // A corner too tight for the DN's elastic radius (or no radius): show the raw
            // sharp centreline in red so the offending corner is visible live, matching
            // PPDraw's infeasible-preview colour.
            AddTransient(CreateSharpPolyline(centerline, InfeasibleColor));
        }

        if (snapMode != PipePlanDESnapMode.None && indicatorSize > 0.0 && centerline.Count >= 3)
        {
            AddSnapIndicator(centerline, snapMode, indicatorSize);
        }

        RefreshTransients();
    }

    // AddTransient only paints reliably while the cursor is moving — the PDDRAW draw preview
    // gets away with it because you are always moving while placing points. A hover-and-stop
    // gesture (PDEDIT delete, where you settle on a vertex to read the result) needs an
    // explicit UpdateTransient to repaint at rest; Editor.UpdateScreen() inside a PointMonitor
    // is deferred until AutoCAD is next idle, so it does not help here.
    private void RefreshTransients()
    {
        foreach (Entity entity in _entities)
        {
            try
            {
                TransientManager.CurrentTransientManager.UpdateTransient(entity, _viewports);
            }
            catch
            {
                // Best effort — ignore refresh failures during pan/zoom races.
            }
        }
    }

    // Overlays each arc segment of the centreline with a dark-green copy so straights stay
    // green and bends read dark green (centreline only — the bands keep their red/blue).
    private void AddArcOverlays(IReadOnlyList<PolylineVertexData> vertices, double elevation)
    {
        for (int i = 0; i < vertices.Count - 1; i++)
        {
            double bulge = vertices[i].Bulge;
            if (!PipePlanArcGeometry.IsArcBulge(bulge))
            {
                continue;
            }

            Polyline arc = new();
            arc.AddVertexAt(0, vertices[i].Point, bulge, 0.0, 0.0);
            arc.AddVertexAt(1, vertices[i + 1].Point, 0.0, 0.0, 0.0);
            arc.Elevation = elevation;
            arc.Color = ArcColor;
            arc.LineWeight = LineWeight.LineWeight050;
            AddTransient(arc);
        }
    }

    // "R=<min> (akse <centre>)": the minimum elastic bending radius (inner pipe) plus the
    // actual filleted centreline radius. The solver annotates the centreline radius, so the
    // bending limit is that minus the half-spacing offset.
    private void AddRadiusLabels(PipePlanAnalysis analysis, double half)
    {
        double textHeight = GetTextHeight();
        foreach (PipePlanRadiusAnnotation annotation in analysis.RadiusAnnotations)
        {
            AddTransient(CreateRadiusLabel(annotation, half, textHeight));
        }
    }

    private static MText CreateRadiusLabel(PipePlanRadiusAnnotation annotation, double half, double textHeight)
    {
        double rMin = annotation.Radius - half;
        MText label = new();
        label.SetDatabaseDefaults();
        label.Color = ArcColor;
        label.Contents = $"R={rMin.ToString("0.###", CultureInfo.CurrentCulture)}";
        label.TextHeight = textHeight;
        label.Attachment = AttachmentPoint.MiddleCenter;
        label.Location = GetLabelLocation(annotation, textHeight);
        return label;
    }

    private void AddFilletEndpointMarkers(PipePlanAnalysis analysis)
    {
        double markerRadius = GetMarkerRadius();
        foreach (PipePlanFilletEndpointMarker marker in analysis.FilletEndpointMarkers)
        {
            AddTransient(CreateMarker(marker.TangentIn, markerRadius));
            AddTransient(CreateMarker(marker.TangentOut, markerRadius));
        }
    }

    private static Circle CreateMarker(Point3d point, double markerRadius)
    {
        Circle marker = new(point, Vector3d.ZAxis, markerRadius) { Color = ArcColor };
        marker.LineWeight = LineWeight.LineWeight050;
        return marker;
    }

    private static Point3d GetLabelLocation(PipePlanRadiusAnnotation annotation, double textHeight)
    {
        Vector3d direction = annotation.ArcMidPoint - annotation.Center;
        if (direction.Length <= 1e-6)
        {
            return annotation.ArcMidPoint;
        }

        Vector3d offset = direction.GetNormal() * Math.Max(textHeight * 0.8, annotation.Radius * 0.05);
        return annotation.ArcMidPoint + offset;
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

    private static Color ByAci(short index) => Color.FromColorIndex(ColorMethod.ByAci, index);

    private static Polyline CreateIndicatorPolyline(IReadOnlyList<Point3d> points, short colorIndex)
    {
        Polyline polyline = new();
        for (int i = 0; i < points.Count; i++)
        {
            polyline.AddVertexAt(i, new Point2d(points[i].X, points[i].Y), 0.0, 0.0, 0.0);
        }

        polyline.Elevation = points[0].Z;
        polyline.Color = ByAci(colorIndex);
        polyline.LineWeight = LineWeight.LineWeight040;
        return polyline;
    }

    private static Polyline CreatePolyline(IReadOnlyList<PolylineVertexData> vertices, double width, Color color, double elevation, bool transparent)
    {
        Polyline polyline = new();
        for (int i = 0; i < vertices.Count; i++)
        {
            polyline.AddVertexAt(i, vertices[i].Point, vertices[i].Bulge, width, width);
        }

        polyline.Elevation = elevation;
        polyline.Color = color;
        if (transparent)
        {
            polyline.Transparency = new Transparency(PreviewAlpha);
        }

        polyline.LineWeight = LineWeight.LineWeight050;
        return polyline;
    }

    // Straight (bulge-free) polyline used only for the infeasible-corner warning preview.
    private static Polyline CreateSharpPolyline(IReadOnlyList<Point3d> points, Color color)
    {
        Polyline polyline = new();
        for (int i = 0; i < points.Count; i++)
        {
            polyline.AddVertexAt(i, new Point2d(points[i].X, points[i].Y), 0.0, 0.0, 0.0);
        }

        polyline.Elevation = points[0].Z;
        polyline.Color = color;
        polyline.LineWeight = LineWeight.LineWeight050;
        return polyline;
    }

    private static double GetTextHeight()
    {
        try
        {
            using ViewTableRecord view = Application.DocumentManager.MdiActiveDocument.Editor.GetCurrentView();
            return Math.Clamp(view.Height / 40.0, 0.3, 8.0);
        }
        catch
        {
            return 1.0;
        }
    }

    private static double GetMarkerRadius()
    {
        try
        {
            using ViewTableRecord view = Application.DocumentManager.MdiActiveDocument.Editor.GetCurrentView();
            return Math.Clamp(view.Height / 140.0, 0.08, 2.5);
        }
        catch
        {
            return 0.15;
        }
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
