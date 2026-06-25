using NetTopologySuite.Geometries;

namespace NorsynDistrictZones.Topology;

/// <summary>Internal smoke test for the pure geometry ops (invoked from the dev harness).</summary>
public static class SelfTest
{
    public static string Run()
    {
        var gf = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory();

        Polygon Square(double x0, double y0, double x1, double y1) =>
            gf.CreatePolygon(new[]
            {
                new Coordinate(x0, y0), new Coordinate(x1, y0),
                new Coordinate(x1, y1), new Coordinate(x0, y1), new Coordinate(x0, y0),
            });

        var outer = Square(0, 0, 100, 100);

        // Edge-to-edge vertical cut at x=50 → two 50x100 faces (area 5000 each).
        var cut = gf.CreateLineString(new[] { new Coordinate(50, -10), new Coordinate(50, 110) });
        var split = ZoneGeometryOps.SplitByLine(outer, cut);

        // Closed 20x20 region inside → parent area 10000-400=9600, sub area 400.
        var hole = ZoneGeometryOps.CutHole(outer, Square(20, 20, 40, 40));

        // Pipe from x=-10..60 at y=50 → inside left face (x 0..50) length 50.
        var pipe = gf.CreateLineString(new[] { new Coordinate(-10, 50), new Coordinate(60, 50) });
        double inFirst = split.Count > 0 ? ZoneGeometryOps.InsideLength(split[0], pipe) : 0;

        // Merge the two split halves back → area 10000.
        string mergeArea = split.Count == 2
            ? (ZoneGeometryOps.Merge(split[0], split[1])?.Area.ToString("N0") ?? "null")
            : "n/a";

        return
            $"split={split.Count} faces (areas {string.Join(",", split.Select(p => p.Area.ToString("N0")))}); " +
            $"hole={(hole.HasValue ? $"parent={hole.Value.ParentWithHole.Area:N0}, sub={hole.Value.SubFace.Area:N0}" : "null")}; " +
            $"pipeInsideFirst={inFirst:N1}; mergeArea={mergeArea}";
    }
}
