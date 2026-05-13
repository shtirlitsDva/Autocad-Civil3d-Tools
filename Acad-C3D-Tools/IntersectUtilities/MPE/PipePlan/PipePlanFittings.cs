using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace IntersectUtilities.MPE.PipePlan;

internal static class PipePlanFittingSnapService
{
    private const double SnapTolerance = 2.0;
    private const double DistanceTolerance = 1e-6;
    private const double BulgeTolerance = 1e-6;

    public static bool TryFindBestProposal(
        Document document,
        Point3d anchorPoint,
        Point3d movingPoint,
        ObjectId excludedPolylineId,
        out PipePlanFittingProposal? proposal,
        out Point3d snappedPoint,
        out double snapDistance)
    {
        proposal = null;
        snappedPoint = movingPoint;
        snapDistance = double.MaxValue;

        using Transaction transaction = document.Database.TransactionManager.StartTransaction();
        BlockTable blockTable = (BlockTable)transaction.GetObject(document.Database.BlockTableId, OpenMode.ForRead);
        BlockTableRecord modelSpace = (BlockTableRecord)transaction.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead);

        foreach (ObjectId entityId in modelSpace)
        {
            if (entityId == excludedPolylineId)
            {
                continue;
            }

            if (transaction.GetObject(entityId, OpenMode.ForRead) is not Polyline staticPolyline)
            {
                continue;
            }

            if (!PipePlanMetadata.TryRead(staticPolyline, transaction, out _))
            {
                continue;
            }

            for (int segmentIndex = 0; segmentIndex < staticPolyline.NumberOfVertices - 1; segmentIndex++)
            {
                if (Math.Abs(staticPolyline.GetBulgeAt(segmentIndex)) > BulgeTolerance)
                {
                    continue;
                }

                Point3d segmentStart = staticPolyline.GetPoint3dAt(segmentIndex);
                Point3d segmentEnd = staticPolyline.GetPoint3dAt(segmentIndex + 1);
                if (!TryBuildProposal(anchorPoint, movingPoint, segmentStart, segmentEnd, out PipePlanFittingProposal? candidateProposal, out Point3d candidateSnappedPoint, out double candidateSnapDistance))
                {
                    continue;
                }

                if (candidateSnapDistance >= snapDistance)
                {
                    continue;
                }

                proposal = candidateProposal;
                snappedPoint = candidateSnappedPoint;
                snapDistance = candidateSnapDistance;
            }
        }

        transaction.Commit();
        return proposal is not null;
    }

    private static bool TryBuildProposal(
        Point3d anchorPoint,
        Point3d movingPoint,
        Point3d staticSegmentStart,
        Point3d staticSegmentEnd,
        out PipePlanFittingProposal? proposal,
        out Point3d snappedPoint,
        out double snapDistance)
    {
        proposal = null;
        snappedPoint = movingPoint;
        snapDistance = double.MaxValue;

        if (!PipePlanGeometryUtil.TryProjectPointOntoSegment(anchorPoint, staticSegmentStart, staticSegmentEnd, out Point3d intersectionPoint))
        {
            return false;
        }

        Vector2d branchVector = PipePlanGeometryUtil.To2D(anchorPoint - intersectionPoint);
        if (branchVector.Length <= DistanceTolerance)
        {
            return false;
        }

        Vector2d branchDirection2d = branchVector.GetNormal();
        Point3d projectedPoint = PipePlanGeometryUtil.ProjectPointOntoLine(movingPoint, intersectionPoint, branchDirection2d);
        double lateralDistance = PipePlanGeometryUtil.Distance2D(movingPoint, projectedPoint);
        if (lateralDistance > SnapTolerance)
        {
            return false;
        }

        double longitudinalDistance = PipePlanGeometryUtil.DotProduct(projectedPoint - intersectionPoint, branchDirection2d);
        if (longitudinalDistance > SnapTolerance)
        {
            return false;
        }

        PipePlanFittingKind kind = longitudinalDistance < -SnapTolerance
            ? PipePlanFittingKind.Cross
            : PipePlanFittingKind.Tee;

        snappedPoint = kind == PipePlanFittingKind.Tee
            ? intersectionPoint
            : projectedPoint;

        snapDistance = PipePlanGeometryUtil.Distance2D(movingPoint, snappedPoint);
        proposal = new PipePlanFittingProposal(
            kind,
            intersectionPoint,
            (staticSegmentEnd - staticSegmentStart).GetNormal(),
            new Vector3d(branchDirection2d.X, branchDirection2d.Y, 0.0));
        return true;
    }
}

internal static class PipePlanFittingGeometry
{
    public static IEnumerable<Polyline> CreatePolylines(PipePlanFittingProposal proposal, double globalWidth)
    {
        double throughHalfLength = Math.Max(globalWidth * 4.5, 0.8);
        double branchLength = Math.Max(globalWidth * 4.0, 0.7);
        double previewWidth = Math.Max(globalWidth * 0.35, 0.05);

        Vector3d staticDirection = proposal.StaticDirection.GetNormal();
        Vector3d branchDirection = proposal.BranchDirection.GetNormal();
        Point3d center = proposal.IntersectionPoint;

        List<Polyline> polylines =
        [
            CreateLinePolyline(center - (staticDirection * throughHalfLength), center + (staticDirection * throughHalfLength), previewWidth)
        ];

        if (proposal.Kind == PipePlanFittingKind.Tee)
        {
            polylines.Add(CreateLinePolyline(center, center + (branchDirection * branchLength), previewWidth));
        }
        else
        {
            polylines.Add(CreateLinePolyline(center - (branchDirection * throughHalfLength), center + (branchDirection * throughHalfLength), previewWidth));
        }

        foreach (Polyline polyline in polylines)
        {
            polyline.Color = Autodesk.AutoCAD.Colors.Color.FromRgb(255, 140, 0);
        }

        return polylines;
    }

    private static Polyline CreateLinePolyline(Point3d startPoint, Point3d endPoint, double constantWidth)
    {
        Polyline polyline = new();
        polyline.AddVertexAt(0, new Point2d(startPoint.X, startPoint.Y), 0.0, 0.0, 0.0);
        polyline.AddVertexAt(1, new Point2d(endPoint.X, endPoint.Y), 0.0, 0.0, 0.0);
        polyline.ConstantWidth = constantWidth;
        polyline.Closed = false;
        return polyline;
    }
}
