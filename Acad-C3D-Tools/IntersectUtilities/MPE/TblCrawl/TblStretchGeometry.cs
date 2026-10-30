using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Buffer;

using NtsPoint = NetTopologySuite.Geometries.Point;

namespace IntersectUtilities.MPE.TblCrawl;

/// <summary>
/// The TBL stretch: a 1D line on its Belægning's layer (0-TBL-STRÆK-*, see TblBelægninger) drawn by
/// TBLCRAWL on top of the FJV Fremtid xref, carrying its Vejklasse and Belægning in the DriOmråder
/// property set. NSTBL's line mode counts the pipe and components under it with the same tolerance
/// (NSTBL\TblLineMode.cs).
/// </summary>
internal static class TblStretch
{
    // The crawl's joint tolerance: a pipe end sits up to ~10 mm from its port, and a line crawled on a
    // pipe coincides with it to the millimetre, so 25 mm takes the whole pipe and nothing beside it.
    public const double Tolerance = NSAlignmentCrawl.NSAlignmentCrawlConstants.Tolerance;

    // Two stretches crawled over the same pipe coincide to well under a millimetre; a stretch that only
    // touches another (a branch leaving a T) comes within 1 mm of it for a few millimetres at most.
    public const double CoincidenceTolerance = 0.001;
    public const double MinOverlap = 0.05;

    public static readonly string[] Vejklasser = ["1", "2", "3", "4"];
}

/// <summary>
/// Curves as NTS line strings for overlap work. Arcs are densified to a chord error of 0.5 mm, so a
/// line and the pipe it was crawled on stay within a millimetre of each other wherever either is cut.
/// </summary>
internal static class TblStretchGeometry
{
    private const double MaxSagitta = 0.0005;
    private static readonly GeometryFactory Factory = new();
    private static readonly BufferParameters FlatEnds = new(8, EndCapStyle.Flat, NetTopologySuite.Operation.Buffer.JoinStyle.Round, 5.0);

    public static List<(Point2d Pt, double Bulge)> Vertices(Polyline pl)
    {
        List<(Point2d Pt, double Bulge)> vertices = new(pl.NumberOfVertices);
        for (int i = 0; i < pl.NumberOfVertices; i++)
        {
            vertices.Add((pl.GetPoint2dAt(i), pl.GetBulgeAt(i)));
        }

        return vertices;
    }

    public static LineString ToLineString(Polyline pl) => ToLineString(Vertices(pl));

    public static LineString ToLineString(IReadOnlyList<(Point2d Pt, double Bulge)> vertices)
    {
        List<Coordinate> coordinates = [];
        for (int i = 0; i < vertices.Count; i++)
        {
            Point2d p0 = vertices[i].Pt;
            if (coordinates.Count == 0 || !coordinates[^1].Equals2D(new Coordinate(p0.X, p0.Y)))
            {
                coordinates.Add(new Coordinate(p0.X, p0.Y));
            }

            if (i == vertices.Count - 1)
            {
                break;
            }

            Point2d p1 = vertices[i + 1].Pt;
            double bulge = vertices[i].Bulge;
            double chord = p0.GetDistanceTo(p1);
            if (bulge != 0.0 && chord > 0.0)
            {
                coordinates.AddRange(ArcInterior(p0, p1, bulge, chord));
            }
        }

        if (coordinates.Count == 1)
        {
            coordinates.Add(coordinates[0].Copy());
        }

        return Factory.CreateLineString(coordinates.ToArray());
    }

    // The arc's interior points, p0 and p1 left out. Positive bulge = counter-clockwise from p0 to p1;
    // the centre sits chord(1 - b²)/(4b) to the left of the chord's midpoint.
    private static IEnumerable<Coordinate> ArcInterior(Point2d p0, Point2d p1, double bulge, double chord)
    {
        double radius = chord * (1 + (bulge * bulge)) / (4 * Math.Abs(bulge));
        double sweep = 4 * Math.Atan(bulge);
        Vector2d along = p0.GetVectorTo(p1) / chord;
        Vector2d left = new(-along.Y, along.X);
        Point2d mid = new((p0.X + p1.X) / 2, (p0.Y + p1.Y) / 2);
        Point2d centre = mid + (left * (chord * (1 - (bulge * bulge)) / (4 * bulge)));
        double start = Math.Atan2(p0.Y - centre.Y, p0.X - centre.X);
        double step = MaxSagitta < radius ? 2 * Math.Acos(1 - (MaxSagitta / radius)) : Math.Abs(sweep);
        int count = Math.Max(1, (int)Math.Ceiling(Math.Abs(sweep) / step));
        for (int k = 1; k < count; k++)
        {
            double a = start + (sweep * k / count);
            yield return new Coordinate(centre.X + (radius * Math.Cos(a)), centre.Y + (radius * Math.Sin(a)));
        }
    }

    /// <summary>The band within <paramref name="halfWidth"/> of a line, cut square at its two ends.</summary>
    public static Geometry Corridor(Geometry line, double halfWidth) => line.Buffer(halfWidth, FlatEnds);

    public static NtsPoint Point(Point2d p) => Factory.CreatePoint(new Coordinate(p.X, p.Y));

    /// <summary>Every non-empty line string of an overlay result, whatever collection it came in.</summary>
    public static IEnumerable<LineString> Parts(Geometry geometry)
    {
        for (int i = 0; i < geometry.NumGeometries; i++)
        {
            if (geometry.GetGeometryN(i) is LineString part && !part.IsEmpty && part.Length > 0)
            {
                yield return part;
            }
        }
    }
}
