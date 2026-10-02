using Autodesk.AutoCAD.Geometry;
using IntersectUtilities.MPE.PipePlan;

namespace IntersectUtilities.MPE.PipePlanDE;

/// <summary>
/// Builds the three PDDRAW centrelines. In the default (filleted) mode the routing
/// centreline is filleted with the pipe's elastic bending radius (reusing PPDraw's
/// <see cref="PipePlanSolver"/>, which also handles/rejects crowded corners), then the
/// supply and return pipes are produced as a true parallel offset of that filleted
/// centreline. In <c>straight</c> mode (the PDDRAW "Straight" toggle) filleting is
/// skipped entirely and the corners stay sharp mitered — the original PDDRAW behaviour.
///
/// The radius bookkeeping realises "R_min on the inner pipe": we draw the centreline,
/// so the centreline fillet radius is R_min + half-spacing. Offsetting each pipe by
/// ± half-spacing then yields inner = R_min and outer = R_min + spacing. Because a
/// bulge encodes an arc's included angle (not its radius) and a filleted centreline is
/// G1-continuous, the offset simply shifts every vertex along its continuous normal and
/// copies the bulge — the arcs stay concentric with the centreline arc automatically.
/// </summary>
internal static class PipePlanDEGeometryBuilder
{
    private const double DistanceTolerance = 1e-6;

    /// <summary>
    /// Produces the centreline + frem + retur vertex lists. In filleted mode
    /// <paramref name="rMinRadii"/> carries the inner-pipe bending radius per control
    /// point (0 at both endpoints; interior corners > 0), and <paramref name="analysis"/>
    /// returns the solver result (arc/radius annotations for the preview). In straight
    /// mode the radii are ignored and <paramref name="analysis"/> is null. Returns false
    /// with a user-facing (Danish) message when a corner is too tight.
    /// </summary>
    public static bool TryBuild(
        IReadOnlyList<Point3d> controlPoints,
        IReadOnlyList<double> rMinRadii,
        PipePlanDEParameters parameters,
        bool flip,
        bool straight,
        out List<PolylineVertexData> centre,
        out List<PolylineVertexData> frem,
        out List<PolylineVertexData> retur,
        out PipePlanAnalysis? analysis,
        out string error)
    {
        centre = [];
        frem = [];
        retur = [];
        analysis = null;
        error = string.Empty;

        if (controlPoints.Count < 2)
        {
            error = "Mindst to punkter kræves.";
            return false;
        }

        double half = parameters.PipeSpacing / 2.0;
        if (half <= DistanceTolerance)
        {
            error = "Rør-afstanden (d + x) skal være > 0.";
            return false;
        }

        return straight
            ? TryBuildStraight(controlPoints, half, flip, out centre, out frem, out retur, out error)
            : TryBuildFilleted(controlPoints, rMinRadii, half, flip, out centre, out frem, out retur, out analysis, out error);
    }

    private static bool TryBuildFilleted(
        IReadOnlyList<Point3d> controlPoints,
        IReadOnlyList<double> rMinRadii,
        double half,
        bool flip,
        out List<PolylineVertexData> centre,
        out List<PolylineVertexData> frem,
        out List<PolylineVertexData> retur,
        out PipePlanAnalysis? analysis,
        out string error)
    {
        centre = [];
        frem = [];
        retur = [];
        analysis = null;
        error = string.Empty;

        if (rMinRadii.Count != controlPoints.Count)
        {
            error = "Intern fejl: radier passer ikke til punkter.";
            return false;
        }

        // Per-corner centreline fillet radius: the inner pipe (centreline offset inward by
        // half) hits exactly that corner's minimum elastic bending radius. Endpoints stay 0.
        double[] centreRadii = new double[controlPoints.Count];
        for (int i = 0; i < centreRadii.Length; i++)
        {
            centreRadii[i] = (i == 0 || i == centreRadii.Length - 1) ? 0.0 : rMinRadii[i] + half;
        }

        PipePlanAnalysis result = new PipePlanSolver().Analyze(controlPoints, centreRadii);
        if (!result.IsFeasible)
        {
            error = result.Message;
            return false;
        }

        centre = [.. result.Vertices];

        // PipePlanSolver merges vertices that coincide within tolerance, so a degenerate
        // input (e.g. the moving candidate momentarily on the previous point) can collapse
        // to a single vertex. Reject rather than offset an under-length run.
        if (centre.Count < 2)
        {
            error = "To punkter ligger oven på hinanden.";
            centre = [];
            return false;
        }

        analysis = result;
        List<PolylineVertexData> left = PipePlanParallelOffset.Offset(centre, +half);
        List<PolylineVertexData> right = PipePlanParallelOffset.Offset(centre, -half);

        // FREM is the left offset, RETUR the right (matching the drawing direction);
        // Flip swaps which physical side each one is.
        (frem, retur) = flip ? (right, left) : (left, right);
        return true;
    }

    private static bool TryBuildStraight(
        IReadOnlyList<Point3d> controlPoints,
        double half,
        bool flip,
        out List<PolylineVertexData> centre,
        out List<PolylineVertexData> frem,
        out List<PolylineVertexData> retur,
        out string error)
    {
        frem = [];
        retur = [];
        error = string.Empty;

        centre = new List<PolylineVertexData>(controlPoints.Count);
        foreach (Point3d p in controlPoints)
        {
            centre.Add(new PolylineVertexData(new Point2d(p.X, p.Y), 0.0));
        }

        List<PolylineVertexData> left = [];
        List<PolylineVertexData> right = [];
        string miterError = string.Empty;
        bool built = PipePlanParallelOffset.Miter(controlPoints, +half).Bind(l =>
                PipePlanParallelOffset.Miter(controlPoints, -half).Map(r => (Left: l, Right: r)))
            .Match(
                pair =>
                {
                    (left, right) = pair;
                    return true;
                },
                message =>
                {
                    miterError = message;
                    return false;
                });

        if (!built)
        {
            error = miterError;
            centre = [];
            return false;
        }

        (frem, retur) = flip ? (right, left) : (left, right);
        return true;
    }
}
