using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using System.Windows.Forms;

namespace IntersectUtilities.MPE.PipePlan;

internal sealed class CandidatePointTracker : IDisposable
{
    private const double EndpointMatchTolerance = 1e-4;

    private readonly Document _document;
    private readonly PipePlanState _state;
    // Sticky cache: native AC OSnap drops the picked entity between ticks even
    // when the cursor stays visually on the endpoint. Without this cache the
    // cyan tangent preview flickers off after 1-2 seconds and reverts to the
    // green standard preview. The cache is invalidated when the cursor moves
    // outside StickyTolerance of the anchor or when a different polyline is
    // picked. Tracker lifetime is per GetPoint call, so the cache also resets
    // automatically between picks and tangent-mode toggles.
    private PipePlanTangentSnap? _stickySnap;

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
        PipePlanTangentSnap? tangent = _state.IsTangentMode
            ? ResolveTangentWithStickyCache(eventArgs, candidate)
            : null;
        _state.PreviewCandidate(candidate, allowStraightSnap, tangent);
    }

    private PipePlanTangentSnap? ResolveTangentWithStickyCache(PointMonitorEventArgs eventArgs, Point3d candidate)
    {
        PipePlanTangentSnap? fresh = TryResolveTangentSnap(eventArgs, candidate);
        if (fresh.HasValue)
        {
            // Fresh data always wins. If fresh.SourceId differs from the cached
            // SourceId, the cursor has moved to a different polyline near the
            // same anchor — replacement here is exactly the identity invalidation
            // the Codex finding called out as missing.
            _stickySnap = fresh;
            return fresh;
        }

        // Fresh resolution returned nothing (typical mid-hover OSnap drop).
        // Reuse the cache only while the cursor stays near the cached anchor AND
        // the cached SourceId is non-null. We do NOT re-verify the entity here;
        // that's the commit hook's job (PipePlanState.TryRevalidateLatestTangent).
        if (_stickySnap.HasValue &&
            !_stickySnap.Value.SourceId.IsNull &&
            candidate.DistanceTo(_stickySnap.Value.Pp2Anchor) <= GetStickyTolerance())
        {
            return _stickySnap;
        }

        _stickySnap = null;
        return null;
    }

    private double GetStickyTolerance()
    {
        try
        {
            using ViewTableRecord view = _document.Editor.GetCurrentView();
            // ≈2.5% of the visible drawing height — generous enough to absorb
            // OSnap dropouts at typical zoom levels, tight enough that a real
            // cursor move away from the anchor invalidates the cache.
            return Math.Clamp(view.Height / 40.0, 0.1, 5.0);
        }
        catch
        {
            return 0.5;
        }
    }

    private PipePlanTangentSnap? TryResolveTangentSnap(PointMonitorEventArgs eventArgs, Point3d candidate)
    {
        FullSubentityPath[] pickedEntities;
        try
        {
            pickedEntities = eventArgs.Context.GetPickedEntities();
        }
        catch
        {
            return null;
        }

        if (pickedEntities is null || pickedEntities.Length == 0)
        {
            return null;
        }

        using Transaction transaction = _document.Database.TransactionManager.StartTransaction();
        try
        {
            Autodesk.AutoCAD.DatabaseServices.Polyline? bestPolyline = null;
            Point3d bestEndpoint = candidate;
            double bestEndpointDistance = double.MaxValue;

            foreach (FullSubentityPath path in pickedEntities)
            {
                ObjectId[] containerIds = path.GetObjectIds();
                if (containerIds is null || containerIds.Length == 0)
                {
                    continue;
                }

                ObjectId entityId = containerIds[^1];
                if (entityId == _state.ContinuedPolylineId)
                {
                    continue;
                }

                DBObject obj = transaction.GetObject(entityId, OpenMode.ForRead);
                if (obj is not Autodesk.AutoCAD.DatabaseServices.Polyline polyline)
                {
                    continue;
                }

                if (!PipePlanMetadata.TryRead(polyline, transaction, out _))
                {
                    continue;
                }

                double distanceToStart = polyline.StartPoint.DistanceTo(candidate);
                double distanceToEnd = polyline.EndPoint.DistanceTo(candidate);
                Point3d endpoint;
                double endpointDistance;
                if (distanceToStart <= distanceToEnd)
                {
                    endpoint = polyline.StartPoint;
                    endpointDistance = distanceToStart;
                }
                else
                {
                    endpoint = polyline.EndPoint;
                    endpointDistance = distanceToEnd;
                }

                if (endpointDistance < bestEndpointDistance)
                {
                    bestEndpointDistance = endpointDistance;
                    bestPolyline = polyline;
                    bestEndpoint = endpoint;
                }
            }

            if (bestPolyline is null)
            {
                transaction.Commit();
                return null;
            }

            Vector2d direction = ResolveTangentDirection(bestPolyline, bestEndpoint);
            transaction.Commit();

            if (direction.Length < EndpointMatchTolerance)
            {
                return null;
            }

            return new PipePlanTangentSnap(bestPolyline.ObjectId, bestEndpoint, direction, bestPolyline.Length);
        }
        catch
        {
            transaction.Abort();
            return null;
        }
    }

    private static Vector2d ResolveTangentDirection(Autodesk.AutoCAD.DatabaseServices.Polyline polyline, Point3d snapPoint)
    {
        bool atStart = snapPoint.DistanceTo(polyline.StartPoint) <= EndpointMatchTolerance;
        bool atEnd = snapPoint.DistanceTo(polyline.EndPoint) <= EndpointMatchTolerance;
        if (!atStart && !atEnd)
        {
            return new Vector2d(0.0, 0.0);
        }

        Point3d sampleOn = atStart ? polyline.StartPoint : polyline.EndPoint;
        try
        {
            Vector3d derivative = polyline.GetFirstDerivative(sampleOn);
            // GetFirstDerivative points along the polyline's parameter direction
            // (start → end). For our fillet math we need the vector pointing INTO PP2
            // from the endpoint, so flip at the end-endpoint.
            if (atEnd)
            {
                derivative = derivative.Negate();
            }
            // GetFirstDerivative's magnitude reflects local parameterization
            // (≈ segment length for straight polyline segments), not unit. Normalize so
            // downstream code can treat the vector as a direction.
            Vector2d direction = new(derivative.X, derivative.Y);
            double length = direction.Length;
            return length < EndpointMatchTolerance ? new Vector2d(0.0, 0.0) : direction / length;
        }
        catch
        {
            return new Vector2d(0.0, 0.0);
        }
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
        _state.LastEditDragPoint = candidatePoint;
        PipePlanEditCandidate candidate = _session.BuildCandidate(_handle, candidatePoint);
        _state.ShowPreview(candidate.Analysis);

        if (candidate.Analysis.IsFeasible)
        {
            _state.SetStatus("Edit preview is feasible. Click to apply.", PipePlanStatusKind.Ok);
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
