using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using System.Windows.Forms;

namespace PipePlan.Plugin;

internal sealed class CandidatePointTracker : IDisposable
{
    private readonly Document _document;
    private readonly PipePlanState _state;

    public CandidatePointTracker(Document document, PipePlanState state)
    {
        _document = document;
        _state = state;
        _document.Editor.PointMonitor += OnPointMonitor;
    }

    public void Dispose()
    {
        _document.Editor.PointMonitor -= OnPointMonitor;
    }

    private void OnPointMonitor(object sender, PointMonitorEventArgs eventArgs)
    {
        if (_state.DraftPoints.Count == 0)
        {
            return;
        }

        Point3d candidate = eventArgs.Context.ComputedPoint;
        bool allowStraightSnap = (Control.ModifierKeys & Keys.Control) == Keys.Control;
        _state.PreviewCandidate(candidate, allowStraightSnap);
    }
}

internal sealed class PipePlanEditTracker : IDisposable
{
    private readonly Document _document;
    private readonly PipePlanState _state;
    private readonly PipePlanEditSession _session;
    private readonly PipePlanEditHandle _handle;

    public PipePlanEditTracker(Document document, PipePlanState state, PipePlanEditSession session, PipePlanEditHandle handle)
    {
        _document = document;
        _state = state;
        _session = session;
        _handle = handle;
        _document.Editor.PointMonitor += OnPointMonitor;
    }

    public void Dispose()
    {
        _document.Editor.PointMonitor -= OnPointMonitor;
    }

    private void OnPointMonitor(object sender, PointMonitorEventArgs eventArgs)
    {
        Point3d candidatePoint = eventArgs.Context.ComputedPoint;
        PipePlanEditCandidate candidate = _session.BuildCandidate(_handle, candidatePoint);
        _state.ShowPreview(candidate.Analysis, candidate.FittingProposal);

        if (candidate.Analysis.IsFeasible)
        {
            _state.SetStatus(
                candidate.FittingProposal is null
                    ? "Edit preview is feasible. Click to apply."
                    : $"{candidate.FittingProposal.Kind} fitting snap active. Click to apply.",
                PipePlanStatusKind.Ok);
        }
        else
        {
            _state.SetStatus(candidate.Analysis.Message, PipePlanStatusKind.Error);
        }
    }
}

internal sealed class PipePlanSplitTracker : IDisposable
{
    private readonly Document _document;
    private readonly Autodesk.AutoCAD.DatabaseServices.Polyline _polyline;
    private readonly IReadOnlyList<Point3d> _controlPoints;
    private readonly double _radius;
    private readonly IntegerCollection _viewportNumbers = [];
    private readonly List<Entity> _markers = [];

    public PipePlanSplitTracker(Document document, Autodesk.AutoCAD.DatabaseServices.Polyline polyline, IReadOnlyList<Point3d> controlPoints, double radius)
    {
        _document = document;
        _polyline = polyline;
        _controlPoints = controlPoints;
        _radius = radius;
        _document.Editor.PointMonitor += OnPointMonitor;
    }

    public void Dispose()
    {
        _document.Editor.PointMonitor -= OnPointMonitor;
        ClearMarkers();
    }

    private void OnPointMonitor(object? sender, PointMonitorEventArgs eventArgs)
    {
        ClearMarkers();

        if (!PipePlanSplitService.TryResolveSplit(
                _polyline,
                _controlPoints,
                _radius,
                eventArgs.Context.ComputedPoint,
                out _,
                out Point3d splitPoint,
                out _))
        {
            return;
        }

        double markerSize = GetMarkerSize(_document);
        AddMarker(CreateMarkerLine(
            new Point3d(splitPoint.X - markerSize, splitPoint.Y - markerSize, splitPoint.Z),
            new Point3d(splitPoint.X + markerSize, splitPoint.Y + markerSize, splitPoint.Z)));
        AddMarker(CreateMarkerLine(
            new Point3d(splitPoint.X - markerSize, splitPoint.Y + markerSize, splitPoint.Z),
            new Point3d(splitPoint.X + markerSize, splitPoint.Y - markerSize, splitPoint.Z)));
    }

    private void AddMarker(Entity entity)
    {
        _markers.Add(entity);
        TransientManager.CurrentTransientManager.AddTransient(
            entity,
            TransientDrawingMode.DirectShortTerm,
            129,
            _viewportNumbers);
    }

    private void ClearMarkers()
    {
        foreach (Entity marker in _markers)
        {
            try
            {
                TransientManager.CurrentTransientManager.EraseTransient(marker, _viewportNumbers);
            }
            catch
            {
                // Best effort cleanup for transient split markers.
            }

            marker.Dispose();
        }

        _markers.Clear();
    }

    private static Line CreateMarkerLine(Point3d startPoint, Point3d endPoint)
    {
        Line line = new(startPoint, endPoint)
        {
            Color = Autodesk.AutoCAD.Colors.Color.FromRgb(255, 80, 80),
            LineWeight = LineWeight.LineWeight050
        };
        return line;
    }

    private static double GetMarkerSize(Document document)
    {
        using ViewTableRecord view = document.Editor.GetCurrentView();
        return Math.Clamp(view.Height / 120.0, 0.1, 2.0);
    }
}
