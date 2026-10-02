using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace IntersectUtilities.MPE.PipePlan;

/// <summary>
/// Tangent snapping for a bonded draft: hovering ANY member of another bonded run snaps
/// to that run's CENTRELINE end, so the new centreline joins it axis to axis. Bonded
/// snaps to bonded only (any DN); single-pipe metadata is never read here.
/// </summary>
internal static class PipePlanPairTangent
{
    private const double EndpointTolerance = 1e-4;

    public static Option<PipePlanTangentSnap> Resolve(
        Polyline member,
        Transaction transaction,
        Point3d cursor,
        string excludedRunToken,
        PipePlanSolver solver) =>
        PipePlanPairMetadata.Read(member, transaction).Match(
            read => read.Match(
                data => data.RunToken == excludedRunToken
                    ? Option<PipePlanTangentSnap>.Nothing
                    : CentrelineEnd(member.ObjectId, data, cursor, solver),
                _ => Option<PipePlanTangentSnap>.Nothing),
            () => Option<PipePlanTangentSnap>.Nothing);

    /// <summary>Commit-time check that the cached snap still points at a centreline end
    /// of a bonded run.</summary>
    public static Result<PipePlanTangentSnap> Revalidate(Database db, PipePlanTangentSnap snap)
    {
        using Transaction transaction = db.TransactionManager.StartTransaction();
        Result<PipePlanTangentSnap> result = transaction.GetObject(snap.SourceId, OpenMode.ForRead) is Polyline member
            ? Resolve(member, transaction, snap.Pp2Anchor, excludedRunToken: string.Empty, new PipePlanSolver()).Match(
                fresh => fresh.Pp2Anchor.DistanceTo(snap.Pp2Anchor) <= EndpointTolerance
                    ? Result<PipePlanTangentSnap>.Success(fresh)
                    : Result<PipePlanTangentSnap>.Failure("Tangent-reference er flyttet."),
                () => Result<PipePlanTangentSnap>.Failure("Tangent-reference er ikke længere et PipePlan-par."))
            : Result<PipePlanTangentSnap>.Failure("Tangent-reference er ikke længere en polylinje.");
        transaction.Commit();
        return result;
    }

    private static Option<PipePlanTangentSnap> CentrelineEnd(
        ObjectId sourceId,
        PipePlanPairStoredData data,
        Point3d cursor,
        PipePlanSolver solver)
    {
        PipePlanAnalysis centre = PipePlanPairGeometry.Analyze(
            solver, data.Authoring.ControlPoints, data.Authoring.InnerRadii, data.Spacing.Half);
        IReadOnlyList<PolylineVertexData> vertices = centre.Vertices;
        if (!centre.IsFeasible || vertices.Count < 2)
        {
            return Option<PipePlanTangentSnap>.Nothing;
        }

        double z = data.Authoring.ControlPoints[0].Z;
        Point3d start = new(vertices[0].Point.X, vertices[0].Point.Y, z);
        Point3d end = new(vertices[^1].Point.X, vertices[^1].Point.Y, z);
        bool atStart = cursor.DistanceTo(start) <= cursor.DistanceTo(end);

        // The direction points INTO the run from the snapped end.
        Vector2d direction = atStart
            ? PipePlanArcGeometry.TangentAtStart(vertices[0].Point, vertices[1].Point, vertices[0].Bulge)
            : PipePlanArcGeometry.TangentAtEnd(vertices[^2].Point, vertices[^1].Point, vertices[^2].Bulge).Negate();
        if (direction.Length < EndpointTolerance)
        {
            return Option<PipePlanTangentSnap>.Nothing;
        }

        using Polyline centreline = centre.CreatePolyline();
        return Option<PipePlanTangentSnap>.Of(new PipePlanTangentSnap(
            sourceId,
            atStart ? start : end,
            direction.GetNormal(),
            centreline.Length));
    }
}
