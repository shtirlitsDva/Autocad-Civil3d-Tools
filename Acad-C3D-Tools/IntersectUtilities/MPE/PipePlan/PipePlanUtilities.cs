using Autodesk.AutoCAD.Geometry;
using System.Globalization;

namespace PipePlan.Plugin;

internal static class PipePlanParsing
{
    public static bool TryParsePositiveDouble(string? text, out double value)
    {
        value = 0.0;
        return TryParseDouble(text, out value) && value > 0.0;
    }

    public static bool TryParseDouble(string? text, out double value)
    {
        value = 0.0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ||
               double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
    }
}

internal static class PipePlanGeometryUtil
{
    public static double Distance2D(Point3d a, Point3d b)
    {
        return new Point2d(a.X, a.Y).GetDistanceTo(new Point2d(b.X, b.Y));
    }

    public static Vector2d To2D(Vector3d vector)
    {
        return new Vector2d(vector.X, vector.Y);
    }

    public static bool TryProjectPointOntoSegment(Point3d point, Point3d segmentStart, Point3d segmentEnd, out Point3d projectedPoint)
    {
        projectedPoint = point;
        Vector2d segmentDirection = To2D(segmentEnd - segmentStart);
        double lengthSquared = segmentDirection.DotProduct(segmentDirection);
        if (lengthSquared <= 1e-6)
        {
            return false;
        }

        Vector2d offset = To2D(point - segmentStart);
        double parameter = offset.DotProduct(segmentDirection) / lengthSquared;
        if (parameter < 0.0 || parameter > 1.0)
        {
            return false;
        }

        projectedPoint = new Point3d(
            segmentStart.X + ((segmentEnd.X - segmentStart.X) * parameter),
            segmentStart.Y + ((segmentEnd.Y - segmentStart.Y) * parameter),
            point.Z);
        return true;
    }

    public static Point3d ProjectPointOntoLine(Point3d point, Point3d linePoint, Vector2d lineDirection)
    {
        Vector2d normalizedDirection = lineDirection.GetNormal();
        Vector2d offset = To2D(point - linePoint);
        double distance = offset.DotProduct(normalizedDirection);
        return new Point3d(
            linePoint.X + (normalizedDirection.X * distance),
            linePoint.Y + (normalizedDirection.Y * distance),
            point.Z);
    }

    public static double DotProduct(Vector3d vector, Vector2d direction)
    {
        return (vector.X * direction.X) + (vector.Y * direction.Y);
    }
}
