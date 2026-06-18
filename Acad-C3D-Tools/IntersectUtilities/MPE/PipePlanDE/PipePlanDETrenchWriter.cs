using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using IntersectUtilities.UtilsCommon;
using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Buffer;

namespace IntersectUtilities.MPE.PipePlanDE;

/// <summary>
/// Draws the trench as a true SOLID Hatch (80% transparent) on the "Gravearbejde"
/// layer. The hatch boundary is the axis BUFFERED by B/2 (total width = B, the
/// Regelgrabenbreite). Buffering — rather than naively offsetting two edges — yields
/// a valid, non-self-intersecting band outline even at sharp bends where the inner
/// offset would otherwise fold and bowtie the fill. Mitre joins + flat end caps give
/// the square trench corners and ends.
/// </summary>
internal static class PipePlanDETrenchWriter
{
    public const string TrenchLayer = "Gravearbejde";

    // 80% transparent → 20% opaque. Transparency alpha: 0 = clear, 255 = opaque.
    private const byte TrenchAlpha = 51;

    public static bool TryWrite(
        Database db,
        Transaction transaction,
        IReadOnlyList<Point3d> axisPoints,
        double width,
        out string error)
    {
        error = string.Empty;

        // width = B (Regelgrabenbreite), the total trench width centred on the axis.
        if (width <= 0.0)
        {
            error = "Grabenbredden B er 0.";
            return false;
        }

        if (axisPoints.Count < 2)
        {
            error = "Mindst to punkter kræves.";
            return false;
        }

        Geometry buffered = BuildBuffer(axisPoints, width / 2.0);
        List<Polygon> polygons = ExtractPolygons(buffered);
        if (polygons.Count == 0)
        {
            error = "Kunne ikke danne grav-omrids.";
            return false;
        }

        db.CheckOrCreateLayer(TrenchLayer);
        BlockTableRecord modelSpace = db.GetModelspaceForWrite();

        double elevation = axisPoints[0].Z;
        foreach (Polygon polygon in polygons)
        {
            AppendHatch(modelSpace, transaction, polygon, elevation);
        }

        // The trench linework is just the two long sides (open polylines along the
        // route, like the pipes) — no short end caps. This only makes sense for the
        // normal single-band result; the rare split case falls back to closed rings.
        if (polygons.Count == 1)
        {
            AppendLongEdges(modelSpace, transaction, polygons[0], axisPoints, width / 2.0, elevation);
        }
        else
        {
            foreach (Polygon polygon in polygons)
            {
                AppendRingPolyline(modelSpace, transaction, polygon.ExteriorRing, elevation);
            }
        }

        return true;
    }

    private static Geometry BuildBuffer(IReadOnlyList<Point3d> axisPoints, double distance)
    {
        Coordinate[] coordinates = new Coordinate[axisPoints.Count];
        for (int i = 0; i < axisPoints.Count; i++)
        {
            coordinates[i] = new Coordinate(axisPoints[i].X, axisPoints[i].Y);
        }

        GeometryFactory factory = new();
        LineString axis = factory.CreateLineString(coordinates);

        BufferParameters parameters = new()
        {
            EndCapStyle = NetTopologySuite.Operation.Buffer.EndCapStyle.Flat,
            JoinStyle = NetTopologySuite.Operation.Buffer.JoinStyle.Mitre,
            // Keep normal bends square, but bevel very sharp corners so they don't
            // shoot out a long mitre spike (the "triangle"). 2.0 ≈ bevels once the
            // interior bend angle drops below ~60°.
            MitreLimit = 2.0,
        };

        return axis.Buffer(distance, parameters);
    }

    private static List<Polygon> ExtractPolygons(Geometry geometry)
    {
        List<Polygon> polygons = new();
        switch (geometry)
        {
            case Polygon polygon when !polygon.IsEmpty:
                polygons.Add(polygon);
                break;
            case MultiPolygon multi:
                foreach (Geometry part in multi.Geometries)
                {
                    if (part is Polygon p && !p.IsEmpty)
                    {
                        polygons.Add(p);
                    }
                }
                break;
        }

        return polygons;
    }

    private static void AppendHatch(BlockTableRecord modelSpace, Transaction transaction, Polygon polygon, double elevation)
    {
        Hatch hatch = new()
        {
            Normal = Vector3d.ZAxis,
            Elevation = elevation,
            PatternScale = 1.0,
        };
        hatch.SetHatchPattern(HatchPatternType.PreDefined, "SOLID");
        hatch.Layer = TrenchLayer;

        modelSpace.AppendEntity(hatch);
        transaction.AddNewlyCreatedDBObject(hatch, add: true);

        hatch.Associative = false;
        AppendLoop(hatch, polygon.ExteriorRing, HatchLoopTypes.Outermost);
        foreach (LineString hole in polygon.InteriorRings)
        {
            AppendLoop(hatch, hole, HatchLoopTypes.Default);
        }

        hatch.EvaluateHatch(true);
        hatch.Transparency = new Transparency(TrenchAlpha);
    }

    /// <summary>
    /// Splits the buffered outline into its two long sides and draws each as an open
    /// polyline along the route (no short end caps). The sides are taken from the same
    /// buffer ring as the hatch, so the linework matches the fill exactly. The four
    /// band corners (the offset points at the axis ends) locate where the long sides
    /// meet the end caps.
    /// </summary>
    private static void AppendLongEdges(
        BlockTableRecord modelSpace,
        Transaction transaction,
        Polygon polygon,
        IReadOnlyList<Point3d> axisPoints,
        double halfWidth,
        double elevation)
    {
        Coordinate[] coordinates = polygon.ExteriorRing.Coordinates;
        int count = coordinates.Length;
        if (count > 1 && coordinates[0].Equals2D(coordinates[count - 1]))
        {
            count--;
        }

        Vector2d startNormal = LeftNormal(axisPoints[0], axisPoints[1]);
        Vector2d endNormal = LeftNormal(axisPoints[^2], axisPoints[^1]);
        if (count < 4 || startNormal.Length < 0.5 || endNormal.Length < 0.5)
        {
            // Degenerate — fall back to the full closed outline.
            AppendRingPolyline(modelSpace, transaction, polygon.ExteriorRing, elevation);
            return;
        }

        Point2d[] ring = new Point2d[count];
        for (int i = 0; i < count; i++)
        {
            ring[i] = new Point2d(coordinates[i].X, coordinates[i].Y);
        }

        int startLeft = Nearest(ring, OffsetPoint(axisPoints[0], startNormal, halfWidth));
        int startRight = Nearest(ring, OffsetPoint(axisPoints[0], startNormal, -halfWidth));
        int endLeft = Nearest(ring, OffsetPoint(axisPoints[^1], endNormal, halfWidth));
        int endRight = Nearest(ring, OffsetPoint(axisPoints[^1], endNormal, -halfWidth));

        List<Point2d> leftEdge = ArcExcluding(ring, startLeft, endLeft, startRight, endRight);
        List<Point2d> rightEdge = ArcExcluding(ring, endRight, startRight, startLeft, endLeft);
        AppendOpenPolyline(modelSpace, transaction, leftEdge, elevation);
        AppendOpenPolyline(modelSpace, transaction, rightEdge, elevation);
    }

    private static Vector2d LeftNormal(Point3d from, Point3d to)
    {
        double dx = to.X - from.X;
        double dy = to.Y - from.Y;
        double length = Math.Sqrt((dx * dx) + (dy * dy));
        return length < 1e-9 ? new Vector2d(0.0, 0.0) : new Vector2d(-dy / length, dx / length);
    }

    private static Point2d OffsetPoint(Point3d point, Vector2d normal, double distance)
        => new(point.X + (normal.X * distance), point.Y + (normal.Y * distance));

    private static int Nearest(Point2d[] ring, Point2d target)
    {
        int best = 0;
        double bestDistance = double.MaxValue;
        for (int i = 0; i < ring.Length; i++)
        {
            double distance = ring[i].GetDistanceTo(target);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = i;
            }
        }

        return best;
    }

    /// <summary>Cyclic ring arc from <paramref name="from"/> to <paramref name="to"/>
    /// taking the direction that does not pass through the two end-cap corners.</summary>
    private static List<Point2d> ArcExcluding(Point2d[] ring, int from, int to, int avoidA, int avoidB)
    {
        List<int> forward = WalkIndices(ring.Length, from, to, +1);
        List<int> indices = forward.Contains(avoidA) || forward.Contains(avoidB)
            ? WalkIndices(ring.Length, from, to, -1)
            : forward;

        List<Point2d> points = new(indices.Count);
        foreach (int index in indices)
        {
            points.Add(ring[index]);
        }

        return points;
    }

    private static List<int> WalkIndices(int length, int from, int to, int step)
    {
        List<int> indices = new();
        int i = from;
        while (true)
        {
            indices.Add(i);
            if (i == to || indices.Count > length)
            {
                break;
            }

            i = ((i + step) % length + length) % length;
        }

        return indices;
    }

    private static void AppendOpenPolyline(BlockTableRecord modelSpace, Transaction transaction, List<Point2d> points, double elevation)
    {
        if (points.Count < 2)
        {
            return;
        }

        Polyline polyline = new();
        for (int i = 0; i < points.Count; i++)
        {
            polyline.AddVertexAt(i, points[i], 0.0, 0.0, 0.0);
        }

        polyline.Closed = false;
        polyline.Layer = TrenchLayer;
        polyline.Elevation = elevation;
        polyline.Normal = Vector3d.ZAxis;

        modelSpace.AppendEntity(polyline);
        transaction.AddNewlyCreatedDBObject(polyline, add: true);
    }

    private static void AppendRingPolyline(BlockTableRecord modelSpace, Transaction transaction, LineString ring, double elevation)
    {
        Coordinate[] coordinates = ring.Coordinates;
        int count = coordinates.Length;
        if (count > 1 && coordinates[0].Equals2D(coordinates[count - 1]))
        {
            count--;
        }

        if (count < 2)
        {
            return;
        }

        Polyline polyline = new();
        for (int i = 0; i < count; i++)
        {
            polyline.AddVertexAt(i, new Point2d(coordinates[i].X, coordinates[i].Y), 0.0, 0.0, 0.0);
        }

        polyline.Closed = true;
        polyline.Layer = TrenchLayer;
        polyline.Elevation = elevation;
        polyline.Normal = Vector3d.ZAxis;

        modelSpace.AppendEntity(polyline);
        transaction.AddNewlyCreatedDBObject(polyline, add: true);
    }

    private static void AppendLoop(Hatch hatch, LineString ring, HatchLoopTypes loopType)
    {
        Coordinate[] coordinates = ring.Coordinates;
        // NTS rings repeat the first coordinate as the last; keep it for the hatch loop —
        // AppendLoop wants the explicit closing vertex (dropping it leaves the loop open).
        int count = coordinates.Length;

        Point2dCollection vertices = new();
        DoubleCollection bulges = new();
        for (int i = 0; i < count; i++)
        {
            vertices.Add(new Point2d(coordinates[i].X, coordinates[i].Y));
            bulges.Add(0.0);
        }

        hatch.AppendLoop(loopType, vertices, bulges);
    }
}
