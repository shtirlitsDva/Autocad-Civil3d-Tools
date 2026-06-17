using Autodesk.AutoCAD.Geometry;

namespace IntersectUtilities.MPE.PipePlanDE;

/// <summary>
/// Builds the supply and return pipe centrelines as mitered parallel offsets of a
/// routing centreline. German pipes have no bending radius, so corners are sharp:
/// each interior offset vertex is the intersection of the two adjacent offset
/// edges (a miter), not a fillet arc. This is why we compute the offset by hand
/// rather than using <c>Polyline.GetOffsetCurves</c>, which would round convex
/// corners with arcs.
/// </summary>
internal static class PipePlanDEOffsetBuilder
{
    private const double DistanceTolerance = 1e-6;
    // Reject a miter when the two edges approach a 180° reversal: the offset
    // intersection shoots to infinity (denominator 1 + n1·n2 → 0).
    private const double MiterTolerance = 1e-3;

    /// <summary>
    /// Produces the two offset polylines. <paramref name="spacing"/> is the
    /// centre-to-centre distance; each side is offset by spacing/2. Supply is the
    /// left offset, return the right (relative to the drawing direction).
    /// </summary>
    public static bool TryBuild(
        IReadOnlyList<Point3d> centerline,
        double spacing,
        out List<Point3d> supply,
        out List<Point3d> ret,
        out string error)
    {
        supply = [];
        ret = [];
        error = string.Empty;

        if (centerline.Count < 2)
        {
            error = "Mindst to punkter kræves.";
            return false;
        }

        if (spacing <= DistanceTolerance)
        {
            error = "Rør-afstanden (d + x) skal være > 0.";
            return false;
        }

        double half = spacing / 2.0;

        if (!TryOffset(centerline, half, out supply, out error))
        {
            return false;
        }

        if (!TryOffset(centerline, -half, out ret, out error))
        {
            return false;
        }

        return true;
    }

    private static bool TryOffset(IReadOnlyList<Point3d> points, double offset, out List<Point3d> result, out string error)
    {
        result = new List<Point3d>(points.Count);
        error = string.Empty;

        // Unit direction and left-normal of every segment.
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

            result.Add(new Point3d(points[i].X + miter.X, points[i].Y + miter.Y, points[i].Z));
        }

        return true;
    }

    private static Vector2d To2D(Vector3d vector) => new(vector.X, vector.Y);
}
