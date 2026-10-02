using Autodesk.AutoCAD.Geometry;

namespace IntersectUtilities.MPE.PipePlan;

/// <summary>
/// Parallel offsets of a pipe centreline, shared by PPDRAW's bonded pairs and PDDRAW.
/// A positive offset is to the LEFT of the drawing direction.
/// </summary>
internal static class PipePlanParallelOffset
{
    private const double DistanceTolerance = 1e-6;
    // Reject a miter when the two edges approach a 180° reversal: the offset
    // intersection shoots to infinity (denominator 1 + n1·n2 → 0).
    private const double MiterTolerance = 1e-3;

    /// <summary>
    /// Parallel offset of a G1 (filleted) vertex list. Each vertex moves by
    /// <paramref name="offset"/> along the curve's continuous left-normal there; bulges
    /// are preserved because the arcs' included angles are unchanged. At an arc endpoint
    /// the tangent's left-normal is radial, so the offset arc is concentric with the
    /// original — inner radius shrinks by |offset|, outer grows by |offset|.
    /// </summary>
    public static List<PolylineVertexData> Offset(IReadOnlyList<PolylineVertexData> vertices, double offset)
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
    public static Vector2d TangentAt(IReadOnlyList<PolylineVertexData> vertices, int i)
    {
        if (i < vertices.Count - 1)
        {
            return PipePlanArcGeometry.TangentAtStart(vertices[i].Point, vertices[i + 1].Point, vertices[i].Bulge);
        }

        return PipePlanArcGeometry.TangentAtEnd(vertices[i - 1].Point, vertices[i].Point, vertices[i - 1].Bulge);
    }

    /// <summary>
    /// Sharp mitered parallel offset of raw (bulge-free) control points. Each interior
    /// offset vertex is the intersection of the two adjacent offset edges, not a fillet arc.
    /// </summary>
    public static Result<List<PolylineVertexData>> Miter(IReadOnlyList<Point3d> points, double offset)
    {
        if (points.Count < 2)
        {
            return Result<List<PolylineVertexData>>.Failure("Mindst to punkter kræves.");
        }

        Vector2d[] normals = new Vector2d[points.Count - 1];
        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector2d dir = new(points[i + 1].X - points[i].X, points[i + 1].Y - points[i].Y);
            double length = dir.Length;
            if (length <= DistanceTolerance)
            {
                return Result<List<PolylineVertexData>>.Failure($"To punkter ligger oven på hinanden ved hjørne {i + 1}.");
            }

            dir /= length;
            normals[i] = new Vector2d(-dir.Y, dir.X); // rotate +90° (left)
        }

        List<PolylineVertexData> result = new(points.Count);
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
                    return Result<List<PolylineVertexData>>.Failure($"Hjørne {i + 1} er for skarpt (næsten 180°) til at tegne rør.");
                }

                // m satisfies n1·m = n2·m = 1; offset corner = P + offset * m.
                miter = (n1 + n2) / denominator * offset;
            }

            result.Add(new PolylineVertexData(new Point2d(points[i].X + miter.X, points[i].Y + miter.Y), 0.0));
        }

        return Result<List<PolylineVertexData>>.Success(result);
    }
}
