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
        PipePlanFittingProposal? fittingProposal = handle.Kind == PipePlanEditHandleKind.Vertex
            ? BuildVertexCandidate(handle, dragPoint, controlPoints)
            : BuildSegmentCandidate(handle, dragPoint, controlPoints);

        PipePlanAnalysis analysis = Analyze(controlPoints);
        return new PipePlanEditCandidate(controlPoints, analysis, fittingProposal);
    }

    public void Commit(PipePlanEditCandidate candidate)
    {
        using DocumentLock documentLock = _document.LockDocument();
        using Transaction transaction = _document.Database.TransactionManager.StartTransaction();

        try
        {
            Polyline polyline = (Polyline)transaction.GetObject(_polylineId, OpenMode.ForWrite);
            _data = new PipePlanStoredData(
                _data.System,
                _data.Type,
                _data.Dn,
                _data.Radius,
                _data.StraightSnapToleranceText,
                candidate.ControlPoints,
                _data.ObjectToken);
            Polyline replacement = ReplaceGeometry(polyline, candidate.Analysis, transaction);
            PipePlanMetadata.Write(replacement, _data, transaction);

            if (candidate.FittingProposal is not null)
            {
                AppendFittingGeometry(candidate.FittingProposal, replacement, transaction);
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Abort();
            throw;
        }
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

    private PipePlanFittingProposal? BuildVertexCandidate(PipePlanEditHandle handle, Point3d dragPoint, List<Point3d> controlPoints)
    {
        Point3d original = controlPoints[handle.Index];
        controlPoints[handle.Index] = new Point3d(dragPoint.X, dragPoint.Y, original.Z);
        return TryApplyVertexFittingSnap(handle.Index, controlPoints);
    }

    private PipePlanFittingProposal? BuildSegmentCandidate(PipePlanEditHandle handle, Point3d dragPoint, List<Point3d> controlPoints)
    {
        Vector3d delta = new(dragPoint.X - handle.GripPoint.X, dragPoint.Y - handle.GripPoint.Y, 0.0);
        controlPoints[handle.Index] = controlPoints[handle.Index].Add(delta);
        controlPoints[handle.Index + 1] = controlPoints[handle.Index + 1].Add(delta);
        return TryApplySegmentFittingSnap(handle.Index, controlPoints);
    }

    private PipePlanAnalysis Analyze(IReadOnlyList<Point3d> controlPoints)
    {
        if (_data.Radius <= 0.0)
        {
            return PipePlanAnalysis.Invalid(controlPoints, "Stored radius is not valid.");
        }

        return _solver.Analyze(controlPoints, _data.Radius);
    }

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

    private PipePlanFittingProposal? TryApplyVertexFittingSnap(int movedIndex, List<Point3d> controlPoints)
    {
        PipePlanFittingProposal? bestProposal = null;
        double bestSnapDistance = double.MaxValue;
        Point3d bestSnappedPoint = controlPoints[movedIndex];

        if (movedIndex > 0)
        {
            TryUpdateBestFittingSnap(controlPoints[movedIndex - 1], controlPoints[movedIndex], ref bestProposal, ref bestSnappedPoint, ref bestSnapDistance);
        }

        if (movedIndex < controlPoints.Count - 1)
        {
            TryUpdateBestFittingSnap(controlPoints[movedIndex + 1], controlPoints[movedIndex], ref bestProposal, ref bestSnappedPoint, ref bestSnapDistance);
        }

        if (bestProposal is not null)
        {
            Point3d original = controlPoints[movedIndex];
            controlPoints[movedIndex] = new Point3d(bestSnappedPoint.X, bestSnappedPoint.Y, original.Z);
        }

        return bestProposal;
    }

    private PipePlanFittingProposal? TryApplySegmentFittingSnap(int segmentIndex, List<Point3d> controlPoints)
    {
        PipePlanFittingProposal? bestProposal = null;
        double bestSnapDistance = double.MaxValue;
        Point3d bestSnappedPoint = Point3d.Origin;
        int bestEndpointIndex = -1;

        if (segmentIndex > 0)
        {
            Point3d movingPoint = controlPoints[segmentIndex];
            if (TryUpdateBestFittingSnap(controlPoints[segmentIndex - 1], movingPoint, ref bestProposal, ref bestSnappedPoint, ref bestSnapDistance))
            {
                bestEndpointIndex = segmentIndex;
            }
        }

        if (segmentIndex + 1 < controlPoints.Count - 1)
        {
            Point3d movingPoint = controlPoints[segmentIndex + 1];
            if (TryUpdateBestFittingSnap(controlPoints[segmentIndex + 2], movingPoint, ref bestProposal, ref bestSnappedPoint, ref bestSnapDistance))
            {
                bestEndpointIndex = segmentIndex + 1;
            }
        }

        if (bestProposal is null || bestEndpointIndex < 0)
        {
            return null;
        }

        Vector3d adjustment = bestSnappedPoint - controlPoints[bestEndpointIndex];
        controlPoints[segmentIndex] = controlPoints[segmentIndex].Add(adjustment);
        controlPoints[segmentIndex + 1] = controlPoints[segmentIndex + 1].Add(adjustment);
        return bestProposal;
    }

    private bool TryUpdateBestFittingSnap(
        Point3d anchorPoint,
        Point3d movingPoint,
        ref PipePlanFittingProposal? bestProposal,
        ref Point3d bestSnappedPoint,
        ref double bestSnapDistance)
    {
        if (!PipePlanFittingSnapService.TryFindBestProposal(_document, anchorPoint, movingPoint, _polylineId, out PipePlanFittingProposal? proposal, out Point3d snappedPoint, out double snapDistance) ||
            proposal is null ||
            snapDistance >= bestSnapDistance)
        {
            return false;
        }

        bestProposal = proposal;
        bestSnappedPoint = snappedPoint;
        bestSnapDistance = snapDistance;
        return true;
    }

    private void AppendFittingGeometry(PipePlanFittingProposal fittingProposal, Polyline replacement, Transaction transaction)
    {
        double globalWidth = ResolveWidth(replacement.ConstantWidth);
        BlockTableRecord owner = (BlockTableRecord)transaction.GetObject(replacement.OwnerId, OpenMode.ForWrite);

        foreach (Polyline fittingPolyline in PipePlanFittingGeometry.CreatePolylines(fittingProposal, globalWidth))
        {
            fittingPolyline.LayerId = replacement.LayerId;
            owner.AppendEntity(fittingPolyline);
            transaction.AddNewlyCreatedDBObject(fittingPolyline, add: true);
        }
    }

    private double ResolveWidth(double fallback)
    {
        PipeSeriesEnum series = NSPaletteAdapter.TryGetCurrentSeries(out PipeSeriesEnum s)
            ? s
            : PipeSeriesEnum.S3;

        try
        {
            double kOd = PipeScheduleV2.PipeScheduleV2.GetPipeKOd(_data.System, _data.Dn, _data.Type, series);
            if (kOd > 0.0) return kOd;
        }
        catch
        {
            // fall through
        }

        return fallback;
    }

    private static Point3d Midpoint(Point3d a, Point3d b)
    {
        return new Point3d((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0, (a.Z + b.Z) / 2.0);
    }
}
