using Autodesk.AutoCAD.Geometry;

namespace IntersectUtilities.MPE.PipePlan;

/// <summary>
/// Solves a bonded pair from its centreline. The drafter's radii are INNER-pipe radii,
/// so the centreline is filleted at R_inner + c/2; offsetting it by ± c/2 (bulges kept)
/// puts the inner pipe of every bend exactly on R_inner and the outer on R_inner + c.
/// </summary>
internal static class PipePlanPairGeometry
{
    private const double RadiusTolerance = 1e-6;

    /// <summary>
    /// The centreline analysis of a pair. Feasible only when the centreline solves AND
    /// every centreline arc stays wider than <paramref name="half"/> — a tighter arc
    /// would fold the inner pipe (possible inside a crowded arc chain).
    /// </summary>
    public static PipePlanAnalysis Analyze(
        PipePlanSolver solver,
        IReadOnlyList<Point3d> points,
        IReadOnlyList<double> innerRadii,
        double half)
    {
        if (points.Count != innerRadii.Count)
        {
            return PipePlanAnalysis.Invalid(points, "Intern fejl: radier passer ikke til punkter.");
        }

        double[] centreRadii = new double[points.Count];
        for (int i = 1; i < points.Count - 1; i++)
        {
            if (innerRadii[i] <= RadiusTolerance)
            {
                return PipePlanAnalysis.Invalid(points, $"Bukkeradius ved hjørne {i + 1} skal være > 0.");
            }

            centreRadii[i] = innerRadii[i] + half;
        }

        PipePlanAnalysis analysis = solver.Analyze(points, centreRadii);
        if (!analysis.IsFeasible)
        {
            return analysis;
        }

        IReadOnlyList<PolylineVertexData> vertices = analysis.Vertices;
        for (int i = 0; i < vertices.Count - 1; i++)
        {
            if (!PipePlanArcGeometry.IsArcBulge(vertices[i].Bulge))
            {
                continue;
            }

            double innerRadius = PipePlanArcGeometry.ArcRadius(vertices[i].Point, vertices[i + 1].Point, vertices[i].Bulge) - half;
            if (innerRadius <= RadiusTolerance)
            {
                return PipePlanAnalysis.Invalid(points, "En bue er for skarp til rørafstanden: inderrøret kan ikke følge den.");
            }
        }

        return analysis;
    }

    /// <summary>Frem and retur as the ± half offsets of a feasible centreline.</summary>
    public static Result<PipePlanPairSolution> Solve(PipePlanAnalysis centre, double half, bool flip)
    {
        if (!centre.IsFeasible)
        {
            return Result<PipePlanPairSolution>.Failure(centre.Message);
        }

        if (centre.Vertices.Count < 2)
        {
            return Result<PipePlanPairSolution>.Failure("To punkter ligger oven på hinanden.");
        }

        List<PolylineVertexData> left = PipePlanParallelOffset.Offset(centre.Vertices, +half);
        List<PolylineVertexData> right = PipePlanParallelOffset.Offset(centre.Vertices, -half);
        (List<PolylineVertexData> frem, List<PolylineVertexData> retur) = flip ? (right, left) : (left, right);
        return Result<PipePlanPairSolution>.Success(new PipePlanPairSolution(centre, frem, retur));
    }

    /// <summary>Re-solves a run from its stored data, with the spacing it was baked at.</summary>
    public static Result<PipePlanPairSolution> SolveStored(PipePlanSolver solver, PipePlanPairStoredData data) =>
        SolveWith(solver, data.Authoring, data.Spacing);

    public static Result<PipePlanPairSolution> SolveWith(PipePlanSolver solver, PipePlanPairAuthoring authoring, PipePlanPairSpacing spacing)
    {
        PipePlanAnalysis centre = Analyze(solver, authoring.ControlPoints, authoring.InnerRadii, spacing.Half);
        return Solve(centre, spacing.Half, authoring.Flip);
    }

    /// <summary>Sharp mitered frem/retur for the red "infeasible" preview.</summary>
    public static Result<(List<PolylineVertexData> Frem, List<PolylineVertexData> Retur)> Miter(
        IReadOnlyList<Point3d> points, double half, bool flip) =>
        PipePlanParallelOffset.Miter(points, +half).Bind(left =>
            PipePlanParallelOffset.Miter(points, -half).Map(right => flip ? (right, left) : (left, right)));
}
