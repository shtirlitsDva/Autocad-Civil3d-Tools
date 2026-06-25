using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using NetTopologySuite.Geometries;

using AcPolyline = Autodesk.AutoCAD.DatabaseServices.Polyline;

namespace NorsynDistrictZones.Acad;

/// <summary>
/// Minimal AutoCAD ↔ NetTopologySuite geometry adapter. Deliberately tiny (this
/// is a mechanical projection, not domain logic) so the plugin does not have to
/// link the heavy IntersectUtilities NTSConversion graph.
/// </summary>
internal static class AcadNts
{
    private const double ArcChordTolerance = 0.01; // metres — max sagitta when tessellating arcs

    /// <summary>Project an lwpolyline (with bulges) to a WCS NTS LineString, tessellating arcs.</summary>
    public static LineString ToLineString(AcPolyline pl, Matrix3d transform)
    {
        var coords = new List<Coordinate>();
        int n = pl.NumberOfVertices;
        if (n < 2) return EmptyLine();
        int segCount = pl.Closed ? n : n - 1;

        for (int i = 0; i < segCount; i++)
        {
            if (pl.GetSegmentType(i) == SegmentType.Arc)
            {
                CircularArc2d arc = pl.GetArcSegment2dAt(i);
                double radius = Math.Max(arc.Radius, 1e-6);
                double sweep = Math.Abs(arc.EndAngle - arc.StartAngle);
                double step = 2.0 * Math.Acos(Math.Clamp(1.0 - ArcChordTolerance / radius, -1.0, 1.0));
                int samples = Math.Max(2, (int)Math.Ceiling(sweep / Math.Max(step, 1e-6)) + 1);
                Point2d[] pts = arc.GetSamplePoints(samples);
                for (int k = 0; k < pts.Length - 1; k++) coords.Add(ToCoord(pts[k], transform));
            }
            else
            {
                coords.Add(ToCoord(pl.GetPoint2dAt(i), transform));
            }
        }
        coords.Add(ToCoord(pl.GetPoint2dAt(pl.Closed ? 0 : n - 1), transform));

        Dedup(coords);
        return coords.Count >= 2 ? new LineString(coords.ToArray()) : EmptyLine();
    }

    /// <summary>Project an old-style 2D polyline (vertex list) to a WCS NTS LineString.</summary>
    public static LineString ToLineString(Polyline2d pl, Transaction tx, Matrix3d transform)
    {
        var coords = new List<Coordinate>();
        foreach (ObjectId vid in pl)
        {
            if (tx.GetObject(vid, OpenMode.ForRead) is Vertex2d v)
                coords.Add(ToCoord3(v.Position, transform));
        }
        if (pl.Closed && coords.Count >= 2) coords.Add(coords[0]);
        Dedup(coords);
        return coords.Count >= 2 ? new LineString(coords.ToArray()) : EmptyLine();
    }

    /// <summary>Project a CLOSED lwpolyline to a WCS NTS Polygon (outer ring only).</summary>
    public static Polygon? ToPolygon(AcPolyline pl, Matrix3d transform)
    {
        LineString ring = ToLineString(pl, transform);
        var coords = ring.Coordinates.ToList();
        if (coords.Count >= 1 && !coords[0].Equals2D(coords[^1])) coords.Add(coords[0].Copy());
        if (coords.Count < 4) return null;
        var gf = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory();
        try { return gf.CreatePolygon(coords.ToArray()); }
        catch { return null; }
    }

    private static Coordinate ToCoord(Point2d p, Matrix3d t)
    {
        Point3d w = new Point3d(p.X, p.Y, 0).TransformBy(t);
        return new Coordinate(w.X, w.Y);
    }

    private static Coordinate ToCoord3(Point3d p, Matrix3d t)
    {
        Point3d w = p.TransformBy(t);
        return new Coordinate(w.X, w.Y);
    }

    private static void Dedup(List<Coordinate> coords)
    {
        for (int i = coords.Count - 2; i >= 0; i--)
            if (coords[i].Distance(coords[i + 1]) < 1e-6) coords.RemoveAt(i + 1);
    }

    private static LineString EmptyLine() =>
        new LineString(new[] { new Coordinate(0, 0), new Coordinate(0, 0) });
}
