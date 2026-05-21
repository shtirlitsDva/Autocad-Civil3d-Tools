using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using IntersectUtilities.UtilsCommon.Enums;

namespace IntersectUtilities.MPE.PipePlan;

internal static class PipePlanSplitService
{
    private const double DistanceTolerance = 1e-6;
    private const double BulgeTolerance = 1e-6;
    private const double PointTolerance = 1e-4;

    public static bool TrySplit(Document document, out string message)
    {
        message = string.Empty;
        if (!TryPickSplitPolyline(document, out ObjectId polylineId, out message))
        {
            return false;
        }

        using DocumentLock documentLock = document.LockDocument();
        using Transaction transaction = document.Database.TransactionManager.StartTransaction();

        try
        {
            if (!TryPrepareSplit(transaction, polylineId, out Polyline? polyline, out PipePlanStoredData? data, out double radius, out message) ||
                polyline is null ||
                data is null)
            {
                return false;
            }

            if (!TryPromptSplitPoint(document, polyline, data.ControlPoints, radius, out Point3d pickedSplitPoint, out message))
            {
                return false;
            }

            if (!TryBuildSplitResult(polyline, data, radius, pickedSplitPoint, out SplitResult? splitResult, out message) || splitResult is null)
            {
                return false;
            }

            WriteSplitResult(transaction, polyline, data, splitResult);
            transaction.Commit();

            message = $"{data.SizeDisplay} splittet i to.";
            return true;
        }
        catch
        {
            transaction.Abort();
            throw;
        }
    }

    public static bool TryResolveSplit(
        Polyline polyline,
        IReadOnlyList<Point3d> controlPoints,
        double radius,
        Point3d pickedPoint,
        out int controlSegmentIndex,
        out Point3d normalizedSplitPoint,
        out string message)
    {
        message = string.Empty;
        controlSegmentIndex = -1;
        normalizedSplitPoint = pickedPoint;

        Point3d splitPoint = polyline.GetClosestPointTo(pickedPoint, extend: false);
        int displaySegmentIndex = GetDisplaySegmentIndex(polyline, splitPoint);
        if (displaySegmentIndex < 0)
        {
            message = "Kan ikke finde splitpunkt.";
            return false;
        }

        if (Math.Abs(polyline.GetBulgeAt(displaySegmentIndex)) > BulgeTolerance)
        {
            message = "PPSPLIT virker kun på lige segmenter.";
            return false;
        }

        if (!TryResolveControlSegment(controlPoints, radius, splitPoint, out controlSegmentIndex, out normalizedSplitPoint))
        {
            message = "Vælg et punkt på et lige segment, væk fra hjørner og endepunkter.";
            return false;
        }

        return true;
    }

    private static bool TryPickSplitPolyline(Document document, out ObjectId polylineId, out string message)
    {
        polylineId = ObjectId.Null;
        message = string.Empty;

        PromptEntityOptions options = new("\nSelect a PipePlan polyline to split: ");
        options.SetRejectMessage("\nOnly PipePlan polylines are supported.");
        options.AddAllowedClass(typeof(Polyline), exactMatch: false);

        PromptEntityResult result = document.Editor.GetEntity(options);
        if (result.Status != PromptStatus.OK)
        {
            message = "Split annulleret.";
            return false;
        }

        polylineId = result.ObjectId;
        return true;
    }

    private static bool TryPrepareSplit(
        Transaction transaction,
        ObjectId polylineId,
        out Polyline? polyline,
        out PipePlanStoredData? data,
        out double radius,
        out string message)
    {
        polyline = null;
        data = null;
        radius = 0.0;
        message = string.Empty;

        polyline = (Polyline)transaction.GetObject(polylineId, OpenMode.ForWrite);
        if (!PipePlanMetadata.TryRead(polyline, transaction, out data) || data is null)
        {
            message = "Polylinjen har ingen PipePlan-data. Kør PPCONVERT først.";
            return false;
        }

        if (!PipePlanGeometryValidator.TryValidateAgainstMetadata(polyline, data, out message))
        {
            return false;
        }

        radius = data.BendRadii.Where(r => r > 0.0).DefaultIfEmpty(0.0).First();
        if (radius <= 0.0)
        {
            message = "Gemte radier er ugyldige. Kør PPCONVERT.";
            return false;
        }

        return true;
    }

    private static bool TryPromptSplitPoint(
        Document document,
        Polyline polyline,
        IReadOnlyList<Point3d> controlPoints,
        double radius,
        out Point3d pickedSplitPoint,
        out string message)
    {
        pickedSplitPoint = Point3d.Origin;
        message = string.Empty;

        using PipePlanSplitTracker tracker = new(document, polyline, controlPoints, radius);
        PromptPointOptions splitOptions = new("\nPick split point on a straight segment: ")
        {
            AllowNone = true
        };

        PromptPointResult splitResult = document.Editor.GetPoint(splitOptions);
        if (splitResult.Status != PromptStatus.OK)
        {
            message = "Split annulleret.";
            return false;
        }

        pickedSplitPoint = splitResult.Value;
        return true;
    }

    private static bool TryBuildSplitResult(
        Polyline polyline,
        PipePlanStoredData data,
        double radius,
        Point3d pickedSplitPoint,
        out SplitResult? splitResult,
        out string message)
    {
        splitResult = null;
        message = string.Empty;

        if (!TryResolveSplit(polyline, data.ControlPoints, radius, pickedSplitPoint, out int controlSegmentIndex, out Point3d normalizedSplitPoint, out message))
        {
            return false;
        }

        List<Point3d> leftControlPoints = [.. data.ControlPoints.Take(controlSegmentIndex + 1), normalizedSplitPoint];
        List<Point3d> rightControlPoints = [normalizedSplitPoint, .. data.ControlPoints.Skip(controlSegmentIndex + 1)];
        if (leftControlPoints.Count < 2 || rightControlPoints.Count < 2)
        {
            message = "Split ville give et ugyldigt PipePlan-objekt.";
            return false;
        }

        List<double> leftRadii = [.. data.BendRadii.Take(controlSegmentIndex + 1), 0.0];
        List<double> rightRadii = [0.0, .. data.BendRadii.Skip(controlSegmentIndex + 1)];

        PipePlanSolver solver = new();
        PipePlanAnalysis leftAnalysis = solver.Analyze(leftControlPoints, leftRadii);
        PipePlanAnalysis rightAnalysis = solver.Analyze(rightControlPoints, rightRadii);
        if (!leftAnalysis.IsFeasible || !rightAnalysis.IsFeasible)
        {
            message = !leftAnalysis.IsFeasible ? leftAnalysis.Message : rightAnalysis.Message;
            return false;
        }

        splitResult = new SplitResult(leftControlPoints, rightControlPoints, leftRadii, rightRadii, leftAnalysis, rightAnalysis);
        return true;
    }

    private static void WriteSplitResult(
        Transaction transaction,
        Polyline sourcePolyline,
        PipePlanStoredData data,
        SplitResult splitResult)
    {
        BlockTableRecord owner = (BlockTableRecord)transaction.GetObject(sourcePolyline.OwnerId, OpenMode.ForWrite);

        PipePlanStoredData leftMetadata = new(data.System, data.Type, data.Dn, splitResult.LeftBendRadii, data.StraightSnapToleranceText, splitResult.LeftControlPoints);
        PipePlanStoredData rightMetadata = new(data.System, data.Type, data.Dn, splitResult.RightBendRadii, data.StraightSnapToleranceText, splitResult.RightControlPoints);

        PipePlanPolylineWriter.AppendFromAnalysis(sourcePolyline, splitResult.LeftAnalysis, leftMetadata, owner, transaction);
        PipePlanPolylineWriter.AppendFromAnalysis(sourcePolyline, splitResult.RightAnalysis, rightMetadata, owner, transaction);

        sourcePolyline.Erase();
    }

    private static int GetDisplaySegmentIndex(Polyline polyline, Point3d splitPoint)
    {
        if (polyline.NumberOfVertices < 2)
        {
            return -1;
        }

        try
        {
            double parameter = polyline.GetParameterAtPoint(splitPoint);
            int segmentIndex = (int)Math.Floor(parameter);
            return Math.Clamp(segmentIndex, 0, polyline.NumberOfVertices - 2);
        }
        catch
        {
            return -1;
        }
    }

    private static bool TryResolveControlSegment(
        IReadOnlyList<Point3d> controlPoints,
        double radius,
        Point3d splitPoint,
        out int controlSegmentIndex,
        out Point3d normalizedSplitPoint)
    {
        controlSegmentIndex = -1;
        normalizedSplitPoint = splitPoint;
        if (controlPoints.Count < 2)
        {
            return false;
        }

        PipePlanBendGeometry?[] bends = BuildBends(controlPoints, radius);
        for (int segmentIndex = 0; segmentIndex < controlPoints.Count - 1; segmentIndex++)
        {
            Point3d straightStart = bends[segmentIndex]?.TangentOut ?? controlPoints[segmentIndex];
            Point3d straightEnd = bends[segmentIndex + 1]?.TangentIn ?? controlPoints[segmentIndex + 1];
            if (PipePlanGeometryUtil.Distance2D(straightStart, straightEnd) <= DistanceTolerance)
            {
                continue;
            }

            if (!TryProjectOntoLineSegment(straightStart, straightEnd, splitPoint, out Point3d projectedPoint))
            {
                continue;
            }

            if (PipePlanGeometryUtil.Distance2D(projectedPoint, splitPoint) > PointTolerance)
            {
                continue;
            }

            if (PipePlanGeometryUtil.Distance2D(projectedPoint, straightStart) <= PointTolerance ||
                PipePlanGeometryUtil.Distance2D(projectedPoint, straightEnd) <= PointTolerance)
            {
                return false;
            }

            controlSegmentIndex = segmentIndex;
            normalizedSplitPoint = projectedPoint;
            return true;
        }

        return false;
    }

    private static PipePlanBendGeometry?[] BuildBends(IReadOnlyList<Point3d> controlPoints, double radius)
    {
        PipePlanBendGeometry?[] bends = new PipePlanBendGeometry?[controlPoints.Count];
        for (int index = 1; index < controlPoints.Count - 1; index++)
        {
            if (PipePlanBendCalculator.TryCompute(controlPoints[index - 1], controlPoints[index], controlPoints[index + 1], radius, out PipePlanBendGeometry bend) == PipePlanBendStatus.Bend)
            {
                bends[index] = bend;
            }
        }

        return bends;
    }

    private static bool TryProjectOntoLineSegment(Point3d start, Point3d end, Point3d point, out Point3d projectedPoint)
    {
        projectedPoint = point;
        Vector2d direction = To2D(end - start);
        double lengthSquared = direction.DotProduct(direction);
        if (lengthSquared <= DistanceTolerance)
        {
            return false;
        }

        Vector2d offset = To2D(point - start);
        double parameter = offset.DotProduct(direction) / lengthSquared;
        if (parameter < -PointTolerance || parameter > 1.0 + PointTolerance)
        {
            return false;
        }

        double clampedParameter = Math.Clamp(parameter, 0.0, 1.0);
        projectedPoint = new Point3d(
            start.X + ((end.X - start.X) * clampedParameter),
            start.Y + ((end.Y - start.Y) * clampedParameter),
            point.Z);
        return true;
    }

    private static Vector2d To2D(Vector3d vector)
    {
        return new Vector2d(vector.X, vector.Y);
    }

    private sealed record SplitResult(
        IReadOnlyList<Point3d> LeftControlPoints,
        IReadOnlyList<Point3d> RightControlPoints,
        IReadOnlyList<double> LeftBendRadii,
        IReadOnlyList<double> RightBendRadii,
        PipePlanAnalysis LeftAnalysis,
        PipePlanAnalysis RightAnalysis);
}
