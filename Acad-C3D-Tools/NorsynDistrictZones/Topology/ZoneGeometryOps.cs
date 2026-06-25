using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Polygonize;

namespace NorsynDistrictZones.Topology;

/// <summary>
/// Pure NTS geometry operations behind the zone edits — no AutoCAD types, fully
/// testable. Splitting a face by a cut line, cutting a hole + sub-face, merging
/// two faces (boundary deletion), point-in-face, and per-face pipe clipping.
/// </summary>
public static class ZoneGeometryOps
{
    /// <summary>
    /// Split a face by an open cutting line that runs edge-to-edge. Returns the
    /// resulting faces (≥2 on a real split). If the line does not divide the face,
    /// the original is returned unchanged (caller treats single-element result as "no split").
    /// </summary>
    public static IReadOnlyList<Polygon> SplitByLine(Polygon face, LineString cut)
    {
        Geometry noded = NdzGeometry.Union(face.Boundary, cut);
        var polygonizer = new Polygonizer();
        polygonizer.Add(noded);

        var faces = polygonizer.GetPolygons()
            .OfType<Polygon>()
            .Where(p => face.Contains(p.InteriorPoint))
            .ToList();

        return faces.Count >= 2 ? faces : new List<Polygon> { face };
    }

    /// <summary>
    /// Cut a closed region out of <paramref name="parent"/>: the parent gets a hole,
    /// and the closed region becomes a new sub-face. Returns null if the closed region
    /// is not fully inside the parent.
    /// </summary>
    public static (Polygon ParentWithHole, Polygon SubFace)? CutHole(Polygon parent, Polygon closed)
    {
        if (!parent.Contains(closed)) return null;

        Geometry diff = NdzGeometry.Difference(parent, closed);
        Polygon? parentWithHole = diff switch
        {
            Polygon p => p,
            MultiPolygon mp => LargestPolygon(mp),
            _ => null,
        };
        return parentWithHole is null ? null : (parentWithHole, closed);
    }

    /// <summary>
    /// Merge two adjacent faces into one (boundary deletion). Returns null if the
    /// union is not a single polygon (faces were not actually adjacent/contiguous).
    /// </summary>
    public static Polygon? Merge(Polygon a, Polygon b)
    {
        Geometry union = NdzGeometry.Union(a, b);
        return union as Polygon; // non-adjacent faces union to a MultiPolygon → null
    }

    /// <summary>True if the WCS point lies inside the face (holes excluded).</summary>
    public static bool ContainsPoint(Polygon face, Coordinate point) =>
        face.Contains(face.Factory.CreatePoint(point));

    /// <summary>The portion of a pipe line that falls inside the face — clipped in memory.</summary>
    public static double InsideLength(Polygon face, Geometry pipeLine)
    {
        if (pipeLine is null) return 0;
        Geometry inside = NdzGeometry.Intersection(face, pipeLine);
        return inside?.Length ?? 0.0;
    }

    /// <summary>Which single face (if any) wholly contains the given closed region.</summary>
    public static ZoneFace? FaceContaining(IEnumerable<ZoneFace> faces, Polygon region) =>
        faces.FirstOrDefault(f => f.Polygon.Contains(region.InteriorPoint));

    /// <summary>Which single face (if any) contains the given point.</summary>
    public static ZoneFace? FaceAt(IEnumerable<ZoneFace> faces, Coordinate point) =>
        faces.FirstOrDefault(f => f.Polygon.Contains(f.Polygon.Factory.CreatePoint(point)));

    private static Polygon LargestPolygon(MultiPolygon mp)
    {
        Polygon best = (Polygon)mp.GetGeometryN(0);
        for (int i = 1; i < mp.NumGeometries; i++)
        {
            var p = (Polygon)mp.GetGeometryN(i);
            if (p.Area > best.Area) best = p;
        }
        return best;
    }
}
