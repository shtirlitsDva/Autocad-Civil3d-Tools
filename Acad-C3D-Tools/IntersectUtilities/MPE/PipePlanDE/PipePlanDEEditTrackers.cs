using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using IntersectUtilities.MPE.PipePlan;

namespace IntersectUtilities.MPE.PipePlanDE;

/// <summary>
/// Live PDEDIT preview while dragging a Vertex/Segment handle: each cursor tick re-solves the
/// candidate run and shows the three-polyline preview (green feasible / red infeasible).
/// <see cref="LastPoint"/> is read on Enter to commit at the last previewed position.
/// </summary>
internal sealed class PipePlanDEEditMoveTracker : IDisposable
{
    private readonly Document _document;
    private readonly PipePlanDEEditSession _session;
    private readonly PipePlanEditHandle _handle;

    public Point3d? LastPoint { get; private set; }

    public PipePlanDEEditMoveTracker(Document document, PipePlanDEEditSession session, PipePlanEditHandle handle)
    {
        _document = document;
        _session = session;
        _handle = handle;
        _document.Editor.PointMonitor += OnPointMonitor;
    }

    public void Dispose() => _document.Editor.PointMonitor -= OnPointMonitor;

    private void OnPointMonitor(object? sender, PointMonitorEventArgs e)
    {
        Point3d cursor = e.Context.ComputedPoint;
        LastPoint = cursor;
        _session.ShowCandidatePreview(_session.BuildCandidate(_handle, cursor));
    }
}

/// <summary>Live preview while extending a run from an endpoint (Continue): each cursor
/// tick rebuilds the whole run through the moving candidate and shows the 3-polyline preview.
/// The candidate is built by a caller-supplied callback that owns the growing point list.</summary>
internal sealed class PipePlanDEContinueTracker : IDisposable
{
    private readonly Document _document;
    private readonly PipePlanDEEditSession _session;
    private readonly Func<Point3d, PipePlanDEEditCandidate> _build;

    public PipePlanDEContinueTracker(Document document, PipePlanDEEditSession session, Func<Point3d, PipePlanDEEditCandidate> build)
    {
        _document = document;
        _session = session;
        _build = build;
        _document.Editor.PointMonitor += OnPointMonitor;
    }

    public void Dispose() => _document.Editor.PointMonitor -= OnPointMonitor;

    private void OnPointMonitor(object? sender, PointMonitorEventArgs e)
    {
        _session.ShowCandidatePreview(_build(e.Context.ComputedPoint));
    }
}

/// <summary>Live preview while placing a new corner on a segment.</summary>
internal sealed class PipePlanDEInsertTracker : IDisposable
{
    private readonly Document _document;
    private readonly PipePlanDEEditSession _session;
    private readonly double _radius;

    public PipePlanDEInsertTracker(Document document, PipePlanDEEditSession session, double radius)
    {
        _document = document;
        _session = session;
        _radius = radius;
        _document.Editor.PointMonitor += OnPointMonitor;
    }

    public void Dispose() => _document.Editor.PointMonitor -= OnPointMonitor;

    private void OnPointMonitor(object? sender, PointMonitorEventArgs e)
    {
        _session.ShowCandidatePreview(_session.BuildNearestInsertCandidate(e.Context.ComputedPoint, _radius, out _));
    }
}

/// <summary>Live preview while choosing a corner to delete: previews the run without the hovered
/// vertex and marks it with a red X.</summary>
internal sealed class PipePlanDEDeleteTracker : IDisposable
{
    private static readonly Color MarkerColor = Color.FromRgb(255, 80, 80);

    private readonly Document _document;
    private readonly PipePlanDEEditSession _session;
    private readonly IntegerCollection _viewports = [];
    private readonly List<Entity> _markers = [];

    public PipePlanDEDeleteTracker(Document document, PipePlanDEEditSession session)
    {
        _document = document;
        _session = session;
        _document.Editor.PointMonitor += OnPointMonitor;
    }

    public void Dispose()
    {
        _document.Editor.PointMonitor -= OnPointMonitor;
        ClearMarkers();
    }

    private void OnPointMonitor(object? sender, PointMonitorEventArgs e)
    {
        ClearMarkers();
        Point3d cursor = e.Context.ComputedPoint;
        if (!_session.TryGetNearestVertexIndex(cursor, _session.GetPickTolerance(), out int index))
        {
            _session.ClearPreview();
            return;
        }

        if (!_session.TryBuildRemoveVertexCandidate(index, out PipePlanDEEditCandidate? candidate, out _) || candidate is null)
        {
            _session.ClearPreview();
            return;
        }

        _session.ShowCandidatePreview(candidate);
        ShowDeleteMarker(_session.ControlPoints[index]);
    }

    private void ShowDeleteMarker(Point3d point)
    {
        double size = _session.GetPickTolerance();
        AddMarker(new Line(new Point3d(point.X - size, point.Y - size, point.Z), new Point3d(point.X + size, point.Y + size, point.Z)));
        AddMarker(new Line(new Point3d(point.X - size, point.Y + size, point.Z), new Point3d(point.X + size, point.Y - size, point.Z)));
    }

    private void AddMarker(Entity entity)
    {
        entity.Color = MarkerColor;
        _markers.Add(entity);
        TransientManager.CurrentTransientManager.AddTransient(entity, TransientDrawingMode.DirectShortTerm, 130, _viewports);
    }

    private void ClearMarkers()
    {
        foreach (Entity marker in _markers)
        {
            try
            {
                TransientManager.CurrentTransientManager.EraseTransient(marker, _viewports);
            }
            catch
            {
                // Best effort cleanup.
            }

            marker.Dispose();
        }

        _markers.Clear();
    }
}
