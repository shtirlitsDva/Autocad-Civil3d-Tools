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
    // Reject a miter when the two edges approach a 180° reversal: the offset
    // intersection shoots to infinity (denominator 1 + n1·n2 → 0).
    private const double MiterTolerance = 1e-3;

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
        List<PolylineVertexData> left = Offset(centre, +half);
        List<PolylineVertexData> right = Offset(centre, -half);

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

        centre = new List<PolylineVertexData>(controlPoints.Count);
        foreach (Point3d p in controlPoints)
        {
            centre.Add(new PolylineVertexData(new Point2d(p.X, p.Y), 0.0));
        }

        if (!TryMiterOffset(controlPoints, +half, out List<PolylineVertexData> left, out error) ||
            !TryMiterOffset(controlPoints, -half, out List<PolylineVertexData> right, out error))
        {
            centre = [];
            return false;
        }

        (frem, retur) = flip ? (right, left) : (left, right);
        return true;
    }

    /// <summary>
    /// Parallel offset of a G1 (filleted) vertex list. Each vertex moves by
    /// <paramref name="offset"/> along the curve's continuous left-normal there; bulges
    /// are preserved because the arcs' included angles are unchanged. At an arc endpoint
    /// the tangent's left-normal is radial, so the offset arc is concentric with the
    /// original — inner radius shrinks by |offset|, outer grows by |offset|.
    /// </summary>
    private static List<PolylineVertexData> Offset(IReadOnlyList<PolylineVertexData> vertices, double offset)
    {
        List<PolylineVertexData> result = new(vertices.Count);
        for (int i = 0; i < vertices.Count; i++)
        {
            Vector2d tangent = TangentAt(vertices, i);
            Vector2d normal = new(-tangent.Y, tangent.X); // rotate +90° (left)
            Point2d point = vertices[i].Point + (normal * offset);
            double bulge = i < vertices.Count - 1 ? vertices[i].Bulge : 0.0;
            result.Add(new PolylineVertexData(point, bulge));
        }

        return result;
    }

    /// <summary>Unit tangent of the G1 curve at vertex <paramref name="i"/>. Continuous,
    /// so the outgoing segment's start tangent equals the incoming segment's end tangent;
    /// the last vertex uses the final segment's end tangent.</summary>
    private static Vector2d TangentAt(IReadOnlyList<PolylineVertexData> vertices, int i)
    {
        if (i < vertices.Count - 1)
        {
            return PipePlanArcGeometry.TangentAtStart(vertices[i].Point, vertices[i + 1].Point, vertices[i].Bulge);
        }

        return PipePlanArcGeometry.TangentAtEnd(vertices[i - 1].Point, vertices[i].Point, vertices[i - 1].Bulge);
    }

    /// <summary>
    /// Sharp mitered parallel offset of the raw control points (bulge-free), for straight
    /// mode. Each interior offset vertex is the intersection of the two adjacent offset
    /// edges, not a fillet arc — mirrors the retired PipePlanDEOffsetBuilder.
    /// </summary>
    private static bool TryMiterOffset(IReadOnlyList<Point3d> points, double offset, out List<PolylineVertexData> result, out string error)
    {
        result = new List<PolylineVertexData>(points.Count);
        error = string.Empty;

        Vector2d[] normals = new Vector2d[points.Count - 1];
        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector2d dir = To2D(points[i + 1] - points[i]);
            double length = dir.Length;
            if (length <= DistanceTolerance)
            {
                error = $"To punkter ligger oven på hinanden ved hjørne {i + 1}.";
                return false;
            }

            dir /= length;
            normals[i] = new Vector2d(-dir.Y, dir.X); // rotate +90° (left)
        }

        for (int i = 0; i < points.Count; i++)
        {
            Vector2d miter;
            if (i == 0)
            {
                miter = normals[0] * offset;
            }
            else if (i == points.Count - 1)
            {
                miter = normals[^1] * offset;
            }
            else
            {
                Vector2d n1 = normals[i - 1];
                Vector2d n2 = normals[i];
                double denominator = 1.0 + n1.DotProduct(n2);
                if (denominator <= MiterTolerance)
                {
                    error = $"Hjørne {i + 1} er for skarpt (næsten 180°) til at tegne rør.";
                    return false;
                }

                // m satisfies n1·m = n2·m = 1; offset corner = P + offset * m.
                miter = (n1 + n2) / denominator * offset;
            }

            result.Add(new PolylineVertexData(new Point2d(points[i].X + miter.X, points[i].Y + miter.Y), 0.0));
        }

        return true;
    }

    private static Vector2d To2D(Vector3d vector) => new(vector.X, vector.Y);
}
