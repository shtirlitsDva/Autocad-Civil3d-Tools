using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace PipePlan.Plugin;

internal static class PipePlanGeometryValidator
{
    private const double PointTolerance = 1e-4;
    private const double BulgeTolerance = 1e-6;

    public static bool TryValidateAgainstMetadata(Polyline polyline, PipePlanStoredData data, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (!TryParseRadius(data.RadiusText, out double radius))
        {
            errorMessage = $"Stored radius '{data.RadiusText}' is not valid.";
            return false;
        }

        PipePlanSolver solver = new();
        PipePlanAnalysis analysis = solver.Analyze(data.ControlPoints, radius);
        if (!analysis.IsFeasible)
        {
            errorMessage = $"Stored PipePlan metadata is no longer valid: {analysis.Message}";
            return false;
        }

        if (polyline.NumberOfVertices != analysis.Vertices.Count)
        {
            errorMessage = "PipePlan metadata does not match the current polyline geometry. The polyline was likely edited outside PipePlan. Re-bake it before using PPEDIT or Continue.";
            return false;
        }

        for (int index = 0; index < analysis.Vertices.Count; index++)
        {
            PolylineVertexData expected = analysis.Vertices[index];
            Point2d actualPoint = polyline.GetPoint2dAt(index);
            double actualBulge = polyline.GetBulgeAt(index);

            if (actualPoint.GetDistanceTo(expected.Point) > PointTolerance ||
                Math.Abs(actualBulge - expected.Bulge) > BulgeTolerance)
            {
                errorMessage = "PipePlan metadata does not match the current polyline geometry. The polyline was likely edited outside PipePlan. Re-bake it before using PPEDIT or Continue.";
                return false;
            }
        }

        return true;
    }

    private static bool TryParseRadius(string radiusText, out double radius)
    {
        return PipePlanParsing.TryParsePositiveDouble(radiusText, out radius);
    }
}
