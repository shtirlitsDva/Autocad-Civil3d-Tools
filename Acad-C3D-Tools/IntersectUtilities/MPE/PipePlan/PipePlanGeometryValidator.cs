using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace IntersectUtilities.MPE.PipePlan;

internal static class PipePlanGeometryValidator
{
    private const double PointTolerance = 1e-4;
    private const double BulgeTolerance = 1e-6;

    public static bool TryValidateAgainstMetadata(Polyline polyline, PipePlanStoredData data, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (data.BendRadii.Count != data.ControlPoints.Count)
        {
            errorMessage = "Data inkonsistent. Kør PPCONVERT.";
            return false;
        }

        PipePlanSolver solver = new();
        PipePlanAnalysis analysis = solver.Analyze(data.ControlPoints, data.BendRadii);
        if (!analysis.IsFeasible)
        {
            errorMessage = $"Gemte PipePlan-data er ugyldige: {analysis.Message}";
            return false;
        }

        if (polyline.NumberOfVertices != analysis.Vertices.Count)
        {
            errorMessage = "Polylinjen er ændret udenfor PipePlan. Kør PPCONVERT først.";
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
                errorMessage = "Polylinjen er ændret udenfor PipePlan. Kør PPCONVERT først.";
                return false;
            }
        }

        return true;
    }
}
