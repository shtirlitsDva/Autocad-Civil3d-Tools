using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using IntersectUtilities.UtilsCommon.Enums;

namespace IntersectUtilities.MPE.PipePlan;

internal sealed class PipePlanEditSession : IDisposable
{
    private readonly Document _document;
    private readonly PipePlanState _state;
    private readonly PipePlanSolver _solver = new();
    private readonly PipePlanHandleMarkerManager _markerManager = new();

    private ObjectId _polylineId;
    private PipePlanStoredData _data;
    private int? _pendingRadiusVertex;
    private double _pendingRadiusValue;

    public PipePlanEditSession(Document document, PipePlanState state, ObjectId polylineId, PipePlanStoredData data)
    {
        _document = document;
        _state = state;
        _polylineId = polylineId;
        _data = data;
    }

    public string SizeLabel => _data.SizeDisplay;

    public string RadiusDisplay => _data.RadiusDisplay;

    public void Dispose()
    {
        ClearVisuals();
    }

    public static bool TryCreate(Document document, PipePlanState state, out PipePlanEditSession? session, out string errorMessage)
    {
        session = null;
        errorMessage = string.Empty;

        if (!TryPickEditablePolyline(document, out ObjectId polylineId, out errorMessage))
        {
            return false;
        }

        if (!TryLoadSessionData(document, polylineId, out PipePlanStoredData? data, out errorMessage) || data is null)
        {
            return false;
        }

        state.ApplyStoredContext(data);
        session = new PipePlanEditSession(document, state, polylineId, data);
        return true;
    }

    public void ShowHandles()
    {
        _state.ClearPreview();
        _markerManager.Show(_document, _data.ControlPoints);
    }

    public void ClearVisuals()
    {
        _markerManager.Clear();
        _state.ClearPreview();
    }

    public bool TryResolveHandle(Point3d pickedPoint, out PipePlanEditHandle? handle, out string message)
    {
        handle = null;
        message = string.Empty;

        double tolerance = _markerManager.GetPickTolerance(_document);
        PipePlanEditHandle? bestVertexHandle = FindClosestVertexHandle(pickedPoint);
        PipePlanEditHandle? bestSegmentHandle = FindClosestSegmentHandle(pickedPoint);

        handle = ChooseClosestHandle(pickedPoint, bestVertexHandle, bestSegmentHandle);
        if (handle is null || PipePlanGeometryUtil.Distance2D(pickedPoint, handle.GripPoint) > tolerance)
        {
            message = "Pick a visible PipePlan control handle.";
            return false;
        }

        return true;
    }

    public PipePlanEditCandidate BuildCandidate(PipePlanEditHandle handle, Point3d dragPoint)
    {
        List<Point3d> controlPoints = [.. _data.ControlPoints];
        if (handle.Kind == PipePlanEditHandleKind.Vertex)
        {
            BuildVertexCandidate(handle, dragPoint, controlPoints);
        }
        else
        {
            BuildSegmentCandidate(handle, dragPoint, controlPoints);
        }

        PipePlanAnalysis analysis = Analyze(controlPoints);
        return new PipePlanEditCandidate(controlPoints, analysis);
    }

    public void Commit(PipePlanEditCandidate candidate)
    {
        IReadOnlyList<double> committedRadii = _data.BendRadii;
        if (_pendingRadiusVertex is int idx && idx >= 0 && idx < committedRadii.Count)
        {
            List<double> updated = [.. committedRadii];
            updated[idx] = _pendingRadiusValue;
            committedRadii = updated;
        }

        using DocumentLock documentLock = _document.LockDocument();
        using Transaction transaction = _document.Database.TransactionManager.StartTransaction();

        try
        {
            Polyline polyline = (Polyline)transaction.GetObject(_polylineId, OpenMode.ForWrite);
            _data = new PipePlanStoredData(
                _data.System,
                _data.Type,
                _data.Dn,
                committedRadii,
                _data.StraightSnapToleranceText,
                candidate.ControlPoints,
                _data.ObjectToken);
            Polyline replacement = ReplaceGeometry(polyline, candidate.Analysis, transaction);
            PipePlanMetadata.Write(replacement, _data, transaction);

            transaction.Commit();
        }
        catch
        {
            transaction.Abort();
            throw;
        }

        ClearPendingRadius();
    }

    public void SetPendingRadius(int vertexIndex, double radius)
    {
        if (vertexIndex <= 0 || vertexIndex >= _data.ControlPoints.Count - 1) return;
        if (radius <= 0.0) return;
        _pendingRadiusVertex = vertexIndex;
        _pendingRadiusValue = radius;
    }

    public void ClearPendingRadius()
    {
        _pendingRadiusVertex = null;
        _pendingRadiusValue = 0.0;
    }

    public bool TryGetPendingRadius(out int vertexIndex, out double radius)
    {
        if (_pendingRadiusVertex is int idx)
        {
            vertexIndex = idx;
            radius = _pendingRadiusValue;
            return true;
        }
        vertexIndex = -1;
        radius = 0.0;
        return false;
    }

    public bool TryAnalyzeVertexRadius(int vertexIndex, double radius, out PipePlanAnalysis analysis, out string error)
    {
        Point3d currentPosition = vertexIndex >= 0 && vertexIndex < _data.ControlPoints.Count
            ? _data.ControlPoints[vertexIndex]
            : default;
        return TryAnalyzeVertexState(vertexIndex, currentPosition, radius, out analysis, out error);
    }

    public bool TrySetVertexRadius(int vertexIndex, double radius, out string error)
    {
        Point3d currentPosition = vertexIndex >= 0 && vertexIndex < _data.ControlPoints.Count
            ? _data.ControlPoints[vertexIndex]
            : default;
        return TrySetVertexState(vertexIndex, currentPosition, radius, out error);
    }

    public bool TryAnalyzeVertexState(int vertexIndex, Point3d newPosition, double radius, out PipePlanAnalysis analysis, out string error)
    {
        error = string.Empty;
        analysis = PipePlanAnalysis.Invalid(_data.ControlPoints, string.Empty);

        if (vertexIndex <= 0 || vertexIndex >= _data.ControlPoints.Count - 1)
        {
            error = "Endpoint vertices do not have a bend radius.";
            return false;
        }

        if (radius <= 0.0)
        {
            error = "Radius must be greater than zero.";
            return false;
        }

        List<Point3d> updatedControlPoints = [.. _data.ControlPoints];
        Point3d original = updatedControlPoints[vertexIndex];
        updatedControlPoints[vertexIndex] = new Point3d(newPosition.X, newPosition.Y, original.Z);

        List<double> updatedRadii = [.. _data.BendRadii];
        updatedRadii[vertexIndex] = radius;

        analysis = _solver.Analyze(updatedControlPoints, updatedRadii);
        if (!analysis.IsFeasible)
        {
            error = analysis.Message;
            return false;
        }

        return true;
    }

    public bool TrySetVertexState(int vertexIndex, Point3d newPosition, double radius, out string error)
    {
        if (!TryAnalyzeVertexState(vertexIndex, newPosition, radius, out PipePlanAnalysis analysis, out error))
        {
            return false;
        }

        List<Point3d> updatedControlPoints = [.. _data.ControlPoints];
        Point3d original = updatedControlPoints[vertexIndex];
        updatedControlPoints[vertexIndex] = new Point3d(newPosition.X, newPosition.Y, original.Z);

        List<double> updatedRadii = [.. _data.BendRadii];
        updatedRadii[vertexIndex] = radius;

        using DocumentLock documentLock = _document.LockDocument();
        using Transaction transaction = _document.Database.TransactionManager.StartTransaction();

        try
        {
            Polyline polyline = (Polyline)transaction.GetObject(_polylineId, OpenMode.ForWrite);
            _data = new PipePlanStoredData(
                _data.System,
                _data.Type,
                _data.Dn,
                updatedRadii,
                _data.StraightSnapToleranceText,
                updatedControlPoints,
                _data.ObjectToken);
            Polyline replacement = ReplaceGeometry(polyline, analysis, transaction);
            PipePlanMetadata.Write(replacement, _data, transaction);
            transaction.Commit();
        }
        catch
        {
            transaction.Abort();
            throw;
        }

        return true;
    }

    private static bool TryPickEditablePolyline(Document document, out ObjectId polylineId, out string errorMessage)
    {
        polylineId = ObjectId.Null;
        errorMessage = string.Empty;

        PromptEntityOptions options = new("\nSelect a PipePlan polyline to edit: ");
        options.SetRejectMessage("\nOnly PipePlan polylines are supported.");
        options.AddAllowedClass(typeof(Polyline), exactMatch: false);

        PromptEntityResult result = document.Editor.GetEntity(options);
        if (result.Status != PromptStatus.OK)
        {
            errorMessage = "Edit cancelled.";
            return false;
        }

        polylineId = result.ObjectId;
        return true;
    }

    private static bool TryLoadSessionData(Document document, ObjectId polylineId, out PipePlanStoredData? data, out string errorMessage)
    {
        data = null;
        errorMessage = string.Empty;

        using Transaction transaction = document.Database.TransactionManager.StartTransaction();
        try
        {
            Polyline polyline = (Polyline)transaction.GetObject(polylineId, OpenMode.ForRead);
            if (!PipePlanMetadata.TryRead(polyline, transaction, out data) || data is null)
            {
                errorMessage = "This PipePlan polyline does not contain editable control-point metadata. Re-bake it with the current PPDRAW version first.";
                return false;
            }

            if (!PipePlanGeometryValidator.TryValidateAgainstMetadata(polyline, data, out errorMessage))
            {
                return false;
            }

            transaction.Commit();
            return true;
        }
        catch
        {
            transaction.Abort();
            throw;
        }
    }

    private PipePlanEditHandle? FindClosestVertexHandle(Point3d pickedPoint)
    {
        double bestDistance = double.MaxValue;
        PipePlanEditHandle? bestHandle = null;

        for (int index = 0; index < _data.ControlPoints.Count; index++)
        {
            Point3d controlPoint = _data.ControlPoints[index];
            double distance = PipePlanGeometryUtil.Distance2D(pickedPoint, controlPoint);
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            bestHandle = new PipePlanEditHandle(PipePlanEditHandleKind.Vertex, index, controlPoint);
        }

        return bestHandle;
    }

    private PipePlanEditHandle? FindClosestSegmentHandle(Point3d pickedPoint)
    {
        double bestDistance = double.MaxValue;
        PipePlanEditHandle? bestHandle = null;

        for (int index = 0; index < _data.ControlPoints.Count - 1; index++)
        {
            Point3d midpoint = Midpoint(_data.ControlPoints[index], _data.ControlPoints[index + 1]);
            double distance = PipePlanGeometryUtil.Distance2D(pickedPoint, midpoint);
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            bestHandle = new PipePlanEditHandle(PipePlanEditHandleKind.Segment, index, midpoint);
        }

        return bestHandle;
    }

    private static PipePlanEditHandle? ChooseClosestHandle(
        Point3d pickedPoint,
        PipePlanEditHandle? vertexHandle,
        PipePlanEditHandle? segmentHandle)
    {
        if (vertexHandle is null)
        {
            return segmentHandle;
        }

        if (segmentHandle is null)
        {
            return vertexHandle;
        }

        double vertexDistance = PipePlanGeometryUtil.Distance2D(pickedPoint, vertexHandle.GripPoint);
        double segmentDistance = PipePlanGeometryUtil.Distance2D(pickedPoint, segmentHandle.GripPoint);
        return vertexDistance <= segmentDistance ? vertexHandle : segmentHandle;
    }

    private static void BuildVertexCandidate(PipePlanEditHandle handle, Point3d dragPoint, List<Point3d> controlPoints)
    {
        Point3d original = controlPoints[handle.Index];
        controlPoints[handle.Index] = new Point3d(dragPoint.X, dragPoint.Y, original.Z);
    }

    private static void BuildSegmentCandidate(PipePlanEditHandle handle, Point3d dragPoint, List<Point3d> controlPoints)
    {
        Vector3d delta = new(dragPoint.X - handle.GripPoint.X, dragPoint.Y - handle.GripPoint.Y, 0.0);
        controlPoints[handle.Index] = controlPoints[handle.Index].Add(delta);
        controlPoints[handle.Index + 1] = controlPoints[handle.Index + 1].Add(delta);
    }

    private PipePlanAnalysis Analyze(IReadOnlyList<Point3d> controlPoints)
    {
        if (controlPoints.Count != _data.BendRadii.Count)
        {
            return PipePlanAnalysis.Invalid(controlPoints, "Control points and bend radii are out of sync.");
        }

        IReadOnlyList<double> radii = _data.BendRadii;
        if (_pendingRadiusVertex is int idx && idx >= 0 && idx < radii.Count)
        {
            List<double> overridden = [.. radii];
            overridden[idx] = _pendingRadiusValue;
            radii = overridden;
        }

        return _solver.Analyze(controlPoints, radii);
    }

    public IReadOnlyList<double> CurrentBendRadii => _data.BendRadii;

    private Polyline ReplaceGeometry(Polyline sourcePolyline, PipePlanAnalysis analysis, Transaction transaction)
    {
        Polyline replacement = analysis.CreatePolyline();
        replacement.SetDatabaseDefaults(_document.Database);
        replacement.SetPropertiesFrom(sourcePolyline);
        replacement.LayerId = sourcePolyline.LayerId;
        replacement.LinetypeId = sourcePolyline.LinetypeId;
        replacement.LineWeight = sourcePolyline.LineWeight;
        replacement.LinetypeScale = sourcePolyline.LinetypeScale;
        replacement.Transparency = sourcePolyline.Transparency;
        replacement.Normal = sourcePolyline.Normal;
        replacement.Elevation = sourcePolyline.Elevation;
        replacement.Thickness = sourcePolyline.Thickness;
        replacement.ConstantWidth = ResolveWidth(sourcePolyline.ConstantWidth);
        replacement.Closed = false;

        BlockTableRecord owner = (BlockTableRecord)transaction.GetObject(sourcePolyline.OwnerId, OpenMode.ForWrite);
        owner.AppendEntity(replacement);
        transaction.AddNewlyCreatedDBObject(replacement, add: true);

        if (!sourcePolyline.IsErased)
        {
            sourcePolyline.Erase();
        }

        _polylineId = replacement.ObjectId;
        return replacement;
    }

    private double ResolveWidth(double fallback)
    {
        return PipePlanWidthCalculator.ResolveDrawingWidth(_data.System, _data.Type, _data.Dn, fallback);
    }

    private static Point3d Midpoint(Point3d a, Point3d b)
    {
        return new Point3d((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0, (a.Z + b.Z) / 2.0);
    }
}
