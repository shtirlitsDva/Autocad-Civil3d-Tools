using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace IntersectUtilities.MPE.PipePlan;

internal sealed class PipePlanSolver
{
    private const double DistanceTolerance = 1e-6;
    private const double AngleTolerance = 1e-6;

    public PipePlanAnalysis Analyze(IReadOnlyList<Point3d> points, double radius)
    {
        double[] radii = new double[points.Count];
        for (int i = 0; i < points.Count; i++)
        {
            radii[i] = (i == 0 || i == points.Count - 1) ? 0.0 : radius;
        }

        return Analyze(points, radii);
    }

    public PipePlanAnalysis Analyze(IReadOnlyList<Point3d> points, IReadOnlyList<double> radii)
    {
        if (points.Count < 2)
        {
            return PipePlanAnalysis.Raw(points, true, "Vælg mindst to punkter.");
        }

        if (radii.Count != points.Count)
        {
            return PipePlanAnalysis.Invalid(points, "Intern fejl: data inkonsistent. Kør PPCONVERT.");
        }

        PipePlanBendGeometry?[] bends = new PipePlanBendGeometry?[points.Count];

        for (int i = 1; i < points.Count - 1; i++)
        {
            double radius = radii[i];
            if (radius <= DistanceTolerance)
            {
                return PipePlanAnalysis.Invalid(points, $"Bukkeradius ved hjørne {i + 1} skal være > 0.");
            }

            PipePlanBendStatus status = PipePlanBendCalculator.TryCompute(points[i - 1], points[i], points[i + 1], radius, out PipePlanBendGeometry bend);
            switch (status)
            {
                case PipePlanBendStatus.Bend:
                    bends[i] = bend;
                    break;
                case PipePlanBendStatus.Straight:
                    continue;
                case PipePlanBendStatus.Degenerate:
                    return PipePlanAnalysis.Invalid(points, $"To punkter ligger oven på hinanden ved hjørne {i + 1}.");
                case PipePlanBendStatus.Reversal:
                    return PipePlanAnalysis.Invalid(points, "180° vending kan ikke bukkes.");
                case PipePlanBendStatus.Infeasible:
                    return PipePlanAnalysis.Invalid(points, "Hjørnet er for skarpt for denne radius.");
            }
        }

        // A crowded internal segment (two fillets whose tangent lengths overrun the shared
        // straight) can't stay a straight; consecutive crowded segments form a "run" that is
        // solved together as one G1 tangent arc chain. Only interior segments with a real
        // bend at BOTH ends can crowd; a fillet crowding a polyline endpoint has no partner
        // arc and still fails the length check below.
        bool[] crowded = new bool[Math.Max(0, points.Count - 1)];
        for (int seg = 1; seg <= points.Count - 3; seg++)
        {
            if (bends[seg] is PipePlanBendGeometry cb1 && bends[seg + 1] is PipePlanBendGeometry cb2)
            {
                double length = points[seg].DistanceTo(points[seg + 1]);
                if ((cb1.TangentLength + cb2.TangentLength) - length > DistanceTolerance)
                {
                    crowded[seg] = true;
                }
            }
        }

        // Tangent consumption per corner. A normal fillet consumes its tangent length on both
        // adjacent segments; a corner inside an arc chain consumes only the chain's slid
        // tangent point on the run's OUTER legs, nothing on the fully-arced interior.
        double[] consumeAfter = new double[points.Count];
        double[] consumeBefore = new double[points.Count];
        bool[] mergedSegment = new bool[Math.Max(0, points.Count - 1)];
        for (int i = 1; i < points.Count - 1; i++)
        {
            if (bends[i] is PipePlanBendGeometry b)
            {
                consumeBefore[i] = b.TangentLength;
                consumeAfter[i] = b.TangentLength;
            }
        }

        // Group consecutive crowded segments [segStart..segEnd] into a run spanning corners
        // cs=segStart .. ce=segEnd+1 and solve the whole run as a tangent arc chain.
        Dictionary<int, (int EndCorner, PipePlanCrowdedCornerSolver.RunResult Result)> runs = [];
        for (int seg = 1; seg <= points.Count - 3;)
        {
            if (!crowded[seg])
            {
                seg++;
                continue;
            }

            int segEnd = seg;
            while (segEnd + 1 <= points.Count - 3 && crowded[segEnd + 1])
            {
                segEnd++;
            }

            int cs = seg;
            int ce = segEnd + 1;
            List<Point3d> cornerPoints = [];
            List<double> cornerRadii = [];
            for (int k = cs; k <= ce; k++)
            {
                cornerPoints.Add(points[k]);
                cornerRadii.Add(radii[k]);
            }

            if (!PipePlanCrowdedCornerSolver.TrySolveRun(
                    points[cs - 1], cornerPoints, cornerRadii, points[ce + 1],
                    out PipePlanCrowdedCornerSolver.RunResult runResult, out string runError))
            {
                return PipePlanAnalysis.Invalid(points, $"Hjørne {cs + 1}–{ce + 1}: {runError}");
            }

            runs[cs] = (ce, runResult);
            for (int s = cs; s <= segEnd; s++)
            {
                mergedSegment[s] = true;
            }
            consumeBefore[cs] = runResult.EntryRetreat;
            consumeAfter[cs] = 0.0;
            for (int k = cs + 1; k < ce; k++)
            {
                consumeBefore[k] = 0.0;
                consumeAfter[k] = 0.0;
            }
            consumeBefore[ce] = 0.0;
            consumeAfter[ce] = runResult.ExitAdvance;

            seg = segEnd + 1;
        }

        for (int seg = 0; seg < points.Count - 1; seg++)
        {
            if (mergedSegment[seg])
            {
                continue;
            }

            double length = points[seg].DistanceTo(points[seg + 1]);
            double required = consumeAfter[seg] + consumeBefore[seg + 1];
            if (required - length > DistanceTolerance)
            {
                return PipePlanAnalysis.Invalid(points, $"Segment {seg + 1} for kort til radius.");
            }
        }

        List<PolylineVertexData> vertexData = [new PolylineVertexData(To2D(points[0]), 0.0)];
        List<PipePlanRadiusAnnotation> radiusAnnotations = [];
        List<PipePlanFilletEndpointMarker> filletEndpointMarkers = [];

        int index = 1;
        while (index <= points.Count - 2)
        {
            if (runs.TryGetValue(index, out (int EndCorner, PipePlanCrowdedCornerSolver.RunResult Result) run))
            {
                double z = points[index].Z;
                List<Point2d> chainPoints = run.Result.Points;
                List<double> chainBulges = run.Result.Bulges;
                for (int k = 0; k < chainPoints.Count; k++)
                {
                    AppendVertex(vertexData, chainPoints[k], chainBulges[k]);
                }
                for (int k = 0; k < chainPoints.Count - 1; k++)
                {
                    double arcBulge = chainBulges[k];
                    if (PipePlanArcGeometry.IsArcBulge(arcBulge))
                    {
                        Point2d center = PipePlanArcGeometry.ArcCenter(chainPoints[k], chainPoints[k + 1], arcBulge);
                        // Derive the radius from the arc itself: vanishing (absorbed) arcs are
                        // dropped, so the k-th emitted arc no longer maps to radii[index + k].
                        double arcRadius = PipePlanArcGeometry.ArcRadius(chainPoints[k], chainPoints[k + 1], arcBulge);
                        radiusAnnotations.Add(CreateArcAnnotation(center, chainPoints[k], chainPoints[k + 1], arcRadius, z));
                    }
                    filletEndpointMarkers.Add(new PipePlanFilletEndpointMarker(To3D(chainPoints[k], z), To3D(chainPoints[k + 1], z)));
                }
                index = run.EndCorner + 1;
                continue;
            }

            if (bends[index] is not PipePlanBendGeometry bend)
            {
                index++;
                continue;
            }

            Point2d tangentIn2D = new(bend.TangentIn.X, bend.TangentIn.Y);
            Point2d tangentOut2D = new(bend.TangentOut.X, bend.TangentOut.Y);
            double bulge = bend.Sign * Math.Tan(bend.Deflection / 4.0);

            AppendVertex(vertexData, tangentIn2D, bulge);
            AppendVertex(vertexData, tangentOut2D, 0.0);
            radiusAnnotations.Add(CreateRadiusAnnotation(tangentIn2D, bend));
            filletEndpointMarkers.Add(new PipePlanFilletEndpointMarker(bend.TangentIn, bend.TangentOut));
            index++;
        }

        AppendVertex(vertexData, To2D(points[^1]), 0.0);

        return PipePlanAnalysis.Curved(points, vertexData, radiusAnnotations, filletEndpointMarkers, "Tegning OK.");
    }

    private static void AppendVertex(List<PolylineVertexData> vertices, Point2d point, double bulge)
    {
        if (vertices.Count == 0)
        {
            vertices.Add(new PolylineVertexData(point, bulge));
            return;
        }

        PolylineVertexData last = vertices[^1];
        if (last.Point.GetDistanceTo(point) <= DistanceTolerance)
        {
            vertices[^1] = last with { Bulge = bulge };
            return;
        }

        vertices.Add(new PolylineVertexData(point, bulge));
    }

    private static Point2d To2D(Point3d point)
    {
        return new Point2d(point.X, point.Y);
    }

    private static Vector2d To2D(Vector3d vector)
    {
        return new Vector2d(vector.X, vector.Y);
    }

    private static Point3d To3D(Point2d point, double z)
    {
        return new Point3d(point.X, point.Y, z);
    }

    private static PipePlanRadiusAnnotation CreateArcAnnotation(Point2d center, Point2d from, Point2d to, double radius, double z)
    {
        Vector2d v1 = (from - center).GetNormal();
        Vector2d v2 = (to - center).GetNormal();
        Vector2d midDir = v1 + v2;
        // Near-180° sweep: the two radii cancel; fall back to a perpendicular direction.
        midDir = midDir.Length < 1e-9 ? new Vector2d(-v1.Y, v1.X) : midDir.GetNormal();
        Point2d arcMid = center + (midDir * radius);
        return new PipePlanRadiusAnnotation(
            new Point3d(center.X, center.Y, z),
            new Point3d(arcMid.X, arcMid.Y, z),
            radius);
    }

    private static PipePlanRadiusAnnotation CreateRadiusAnnotation(Point2d tangentIn, PipePlanBendGeometry bend)
    {
        Vector2d offsetToCenter = RotateLeft(bend.IncomingDirection) * (bend.Sign * bend.Radius);
        Point2d center = tangentIn + offsetToCenter;

        Vector2d startRadius = tangentIn - center;
        Vector2d midRadius = Rotate(startRadius, bend.Sign * bend.Deflection / 2.0);
        Point2d arcMidPoint = center + midRadius;

        return new PipePlanRadiusAnnotation(
            new Point3d(center.X, center.Y, bend.Vertex.Z),
            new Point3d(arcMidPoint.X, arcMidPoint.Y, bend.Vertex.Z),
            bend.Radius);
    }

    private static Vector2d RotateLeft(Vector2d vector)
    {
        return new Vector2d(-vector.Y, vector.X);
    }

    private static Vector2d Rotate(Vector2d vector, double angle)
    {
        double cos = Math.Cos(angle);
        double sin = Math.Sin(angle);
        return new Vector2d(
            (vector.X * cos) - (vector.Y * sin),
            (vector.X * sin) + (vector.Y * cos));
    }

}
