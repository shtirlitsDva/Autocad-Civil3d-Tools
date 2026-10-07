// Run inside AutoCAD via ACD-MCP after loading PolylineClosestPoints.cs as script
// declarations (remove its file-scoped namespace line). No drawing objects are created.
var closestRegressionResults = new List<object>();
Polyline ClosestTestPolyline(params (double X, double Y, double Bulge)[] vertices)
{
    var polyline = new Polyline();
    for (int index = 0; index < vertices.Length; index++)
        polyline.AddVertexAt(index, new Point2d(vertices[index].X, vertices[index].Y), vertices[index].Bulge, 0, 0);
    return polyline;
}
void ClosestCheck(string name, Polyline first, Polyline second, double expected)
{
    using (first)
    using (second)
    {
        var pair = PolylineClosestPoints.Find(first, second);
        var reversed = PolylineClosestPoints.Find(second, first);
        double firstError = (first.NumberOfVertices == 1 ? first.GetPoint3dAt(0) : first.GetClosestPointTo(pair.First, false)).DistanceTo(pair.First);
        double secondError = (second.NumberOfVertices == 1 ? second.GetPoint3dAt(0) : second.GetClosestPointTo(pair.Second, false)).DistanceTo(pair.Second);
        bool pass = Math.Abs(pair.Distance - expected) < 1e-6 &&
            Math.Abs(pair.Distance - reversed.Distance) < 1e-6 && firstError < 1e-6 && secondError < 1e-6;
        closestRegressionResults.Add(new { name, pass, expected, actual = pair.Distance, firstError, secondError });
    }
}
ClosestCheck("crossing interiors", ClosestTestPolyline((-10,0,0),(10,0,0)), ClosestTestPolyline((0,-10,0),(0,10,0)), 0);
ClosestCheck("parallel overlapping extents", ClosestTestPolyline((-10,0,0),(10,0,0)), ClosestTestPolyline((-5,3,0),(5,3,0)), 3);
ClosestCheck("finite endpoints, no extensions", ClosestTestPolyline((0,0,0),(1,0,0)), ClosestTestPolyline((2,2,0),(2,3,0)), Math.Sqrt(5));
ClosestCheck("line to arc interior", ClosestTestPolyline((-10,7,0),(10,7,0)), ClosestTestPolyline((5,0,1),(-5,0,0)), 2);
ClosestCheck("clockwise arc interior", ClosestTestPolyline((-10,7,0),(10,7,0)), ClosestTestPolyline((-5,0,-1),(5,0,0)), 2);
ClosestCheck("arc to arc interiors", ClosestTestPolyline((0,-5,1),(0,5,0)), ClosestTestPolyline((13,5,1),(13,-5,0)), 3);
ClosestCheck("arc crossing interiors", ClosestTestPolyline((5,0,1),(-5,0,0)), ClosestTestPolyline((8,0,1),(-2,0,0)), 0);
ClosestCheck("line tangent to arc", ClosestTestPolyline((-10,5,0),(10,5,0)), ClosestTestPolyline((5,0,1),(-5,0,0)), 0);
ClosestCheck("concentric overlapping arcs", ClosestTestPolyline((5,0,1),(-5,0,0)), ClosestTestPolyline((7,0,1),(-7,0,0)), 2);
ClosestCheck("coincident arcs", ClosestTestPolyline((5,0,1),(-5,0,0)), ClosestTestPolyline((-5,0,-1),(5,0,0)), 0);
ClosestCheck("arc constrained to endpoints", ClosestTestPolyline((5,0,1),(-5,0,0)), ClosestTestPolyline((-1,-7,0),(1,-7,0)), Math.Sqrt(65));
var closestClosed = ClosestTestPolyline((0,0,0),(10,0,0),(10,10,0));
closestClosed.Closed = true;
ClosestCheck("closing segment participates", closestClosed, ClosestTestPolyline((2,3,0),(3,2,0)), 0);
ClosestCheck("mixed line and arc segments", ClosestTestPolyline((-20,0,0),(5,0,1),(-5,0,0)), ClosestTestPolyline((-10,7,0),(10,7,0)), 2);
ClosestCheck("duplicate vertex", ClosestTestPolyline((0,0,0),(0,0,0),(10,0,0)), ClosestTestPolyline((5,3,0),(5,4,0)), 3);
ClosestCheck("all vertices coincident", ClosestTestPolyline((5,3,0),(5,3,0),(5,3,0)), ClosestTestPolyline((0,0,0),(10,0,0)), 3);
ClosestCheck("single vertex to interior", ClosestTestPolyline((5,3,0)), ClosestTestPolyline((0,0,0),(10,0,0)), 3);
ClosestCheck("single vertex pair", ClosestTestPolyline((0,0,0)), ClosestTestPolyline((3,4,0)), 5);
var closestSkew = ClosestTestPolyline((0,-10,0),(0,10,0));
closestSkew.Elevation = 4;
ClosestCheck("3D skew interiors", ClosestTestPolyline((-10,0,0),(10,0,0)), closestSkew, 4);
var closestElevatedArc = ClosestTestPolyline((5,0,1),(-5,0,0));
closestElevatedArc.Elevation = 4;
ClosestCheck("arcs at different elevations", ClosestTestPolyline((5,0,1),(-5,0,0)), closestElevatedArc, 4);
var closestSurveyFirst = ClosestTestPolyline((-10,7,0),(10,7,0));
var closestSurveySecond = ClosestTestPolyline((5,0,1),(-5,0,0));
var closestSurveyTransform = Matrix3d.Displacement(new Vector3d(700000,6200000,18));
closestSurveyFirst.TransformBy(closestSurveyTransform);
closestSurveySecond.TransformBy(closestSurveyTransform);
ClosestCheck("survey coordinates", closestSurveyFirst, closestSurveySecond, 2);
var closestTiltFirst = ClosestTestPolyline((-10,7,0),(10,7,0));
var closestTiltSecond = ClosestTestPolyline((5,0,1),(-5,0,0));
var closestTiltTransform = Matrix3d.Rotation(1.1, new Vector3d(1,2,3), Point3d.Origin);
closestTiltFirst.TransformBy(closestTiltTransform);
closestTiltSecond.TransformBy(closestTiltTransform);
ClosestCheck("tilted OCS normals", closestTiltFirst, closestTiltSecond, 2);
var closestMajorArc = ClosestTestPolyline((5,0,Math.Tan(3*Math.PI/8)),(0,-5,0));
ClosestCheck("major arc over 180 degrees", closestMajorArc, ClosestTestPolyline((-10,7,0),(10,7,0)), 2);

// A dense point-to-curve oracle is an upper bound. A larger algorithm result exposes
// a missed minimum. Also check symmetry and that both answers lie on finite paths.
var closestRandom = new Random(5936);
int closestRandomFailures = 0;
double closestWorstExcess = 0;
for (int caseIndex = 0; caseIndex < 120; caseIndex++)
{
    Polyline RandomClosestPolyline()
    {
        var vertices = new List<(double X, double Y, double Bulge)>();
        for (int index = 0; index < 4; index++)
            vertices.Add((closestRandom.NextDouble()*60-30, closestRandom.NextDouble()*60-30,
                index == 3 || closestRandom.NextDouble() < 0.4 ? 0 : closestRandom.NextDouble()*6-3));
        var polyline = ClosestTestPolyline(vertices.ToArray());
        polyline.Closed = caseIndex % 4 == 0;
        if (caseIndex % 3 == 0)
            polyline.TransformBy(Matrix3d.Rotation(closestRandom.NextDouble(), new Vector3d(1,2,3), Point3d.Origin));
        polyline.Elevation += closestRandom.NextDouble()*3;
        return polyline;
    }
    using var first = RandomClosestPolyline();
    using var second = RandomClosestPolyline();
    var pair = PolylineClosestPoints.Find(first, second);
    var reverse = PolylineClosestPoints.Find(second, first);
    double sampled = double.PositiveInfinity;
    for (int index = 0; index <= 600; index++)
    {
        var firstPoint = first.GetPointAtParameter(first.EndParam * index / 600.0);
        var secondPoint = second.GetPointAtParameter(second.EndParam * index / 600.0);
        sampled = Math.Min(sampled, firstPoint.DistanceTo(second.GetClosestPointTo(firstPoint, false)));
        sampled = Math.Min(sampled, secondPoint.DistanceTo(first.GetClosestPointTo(secondPoint, false)));
    }
    double excess = pair.Distance - sampled;
    closestWorstExcess = Math.Max(closestWorstExcess, excess);
    if (excess > 1e-6 || Math.Abs(pair.Distance - reverse.Distance) > 1e-6 ||
        first.GetClosestPointTo(pair.First, false).DistanceTo(pair.First) > 1e-6 ||
        second.GetClosestPointTo(pair.Second, false).DistanceTo(pair.Second) > 1e-6)
    {
        closestRandomFailures++;
        closestRegressionResults.Add(new { name = "random " + caseIndex, pass = false, actual = pair.Distance, reverse = reverse.Distance, sampled, excess });
    }
}
new { knownCases = closestRegressionResults, randomCases = 120, randomFailures = closestRandomFailures, worstExcess = closestWorstExcess }
