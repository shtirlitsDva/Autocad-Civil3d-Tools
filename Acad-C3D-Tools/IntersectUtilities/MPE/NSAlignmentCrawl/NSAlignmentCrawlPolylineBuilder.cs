using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace IntersectUtilities.MPE.NSAlignmentCrawl;

/// <summary>
/// Reconstructs the crawl path as an ordered list of (point, outgoing-bulge) vertices and bakes
/// it into a Polyline. "Outgoing bulge" is AutoCAD's convention: the bulge stored on vertex i is
/// the bulge of the segment from i to i+1.
///
/// The risky part is reversing a pipe: the same arc, traversed backwards, has the NEGATED bulge.
/// </summary>
internal static class NSAlignmentCrawlPolylineBuilder
{
    /// <summary>Reads a curve start→end: vertex i carries its stored outgoing bulge; last vertex gets 0.</summary>
    public static List<(Point2d Pt, double OutBulge)> ReadForward(Polyline pl)
    {
        int n = pl.NumberOfVertices;
        List<(Point2d, double)> result = new(n);
        for (int i = 0; i < n; i++)
        {
            double outBulge = i < n - 1 ? pl.GetBulgeAt(i) : 0.0;
            result.Add((pl.GetPoint2dAt(i), outBulge));
        }

        return result;
    }

    /// <summary>
    /// Reads a curve end→start. Emitted in order v(n-1)..v0; the outgoing bulge of emitted vertex
    /// originally at index i is −(bulge of original segment i-1→i), because reversing direction
    /// flips the arc's sense. The last emitted vertex (original v0) gets 0.
    /// </summary>
    public static List<(Point2d Pt, double OutBulge)> ReadReversed(Polyline pl)
    {
        int n = pl.NumberOfVertices;
        List<(Point2d, double)> result = new(n);
        for (int i = n - 1; i >= 0; i--)
        {
            double outBulge = i > 0 ? -pl.GetBulgeAt(i - 1) : 0.0;
            result.Add((pl.GetPoint2dAt(i), outBulge));
        }

        return result;
    }

    public static List<(Point2d Pt, double OutBulge)> Straight(Point2d from, Point2d to)
        => [(from, 0.0), (to, 0.0)];

    /// <summary>
    /// Appends <paramref name="segment"/> onto <paramref name="result"/>. The segment's first
    /// vertex coincides (within tolerance) with the current last vertex, so it is merged rather
    /// than duplicated: the last vertex inherits the segment's first outgoing bulge.
    /// </summary>
    public static void Append(List<(Point2d Pt, double OutBulge)> result, List<(Point2d Pt, double OutBulge)> segment)
    {
        if (segment.Count == 0)
        {
            return;
        }

        if (result.Count == 0)
        {
            result.AddRange(segment);
            return;
        }

        // Carry the incoming segment's first bulge onto the shared join vertex.
        result[^1] = (result[^1].Pt, segment[0].OutBulge);
        for (int i = 1; i < segment.Count; i++)
        {
            result.Add(segment[i]);
        }
    }

    /// <summary>
    /// Removes redundant nodes on straight runs so the baked alignment carries a vertex only where
    /// the direction actually changes.
    ///
    /// A vertex sitting on a component joint (see <see cref="CrawlPinSet"/>) is removed ONLY when it
    /// is genuinely collinear — it is exempt from the short-hop shortcut below, which drops a vertex
    /// for merely being close to its neighbour rather than for being straight. A joint on a dead
    /// straight run still collapses; a joint at any real change of direction always survives.
    ///
    /// Otherwise an interior vertex is dropped when both of its incident
    /// segments are straight (bulge exactly 0) and the three points are collinear to within
    /// <see cref="NSAlignmentCrawlConstants.CollinearDeviation"/> (a
    /// duplicate point counts as collinear). Arc segments carry a non-zero bulge, so an arc endpoint is never dropped — which
    /// is why a reduction keeps its "arc end → single line element → arc start" shape. The endpoints
    /// (start X / end) are always preserved, so station 0 and the terminus are untouched.
    /// </summary>
    public static List<(Point2d Pt, double OutBulge)> Weed(
        IReadOnlyList<(Point2d Pt, double OutBulge)> vertices, CrawlPinSet? pins = null)
    {
        if (vertices.Count <= 2)
        {
            return [.. vertices];
        }

        List<(Point2d Pt, double OutBulge)> result = new(vertices.Count) { vertices[0] };
        for (int i = 1; i < vertices.Count - 1; i++)
        {
            (Point2d Pt, double OutBulge) prev = result[^1];
            (Point2d Pt, double OutBulge) cur = vertices[i];
            (Point2d Pt, double OutBulge) next = vertices[i + 1];

            bool pinned = pins is not null && pins.IsPinned(cur.Pt);
            if (IsRedundantStraightNode(prev, cur, next, pinned))
            {
                // Drop cur: the segment becomes prev → next. prev's outgoing bulge is already 0
                // (guaranteed straight by IsRedundantStraightNode), so the merged run stays straight.
                continue;
            }

            result.Add(cur);
        }

        result.Add(vertices[^1]);
        return result;
    }

    /// <summary>
    /// True when <paramref name="cur"/> is a removable node on a straight run.
    ///
    /// Collinearity is the classic determinant — twice the signed area of the triangle
    /// (prev, cur, next) — divided by the base, which turns that area into the perpendicular distance
    /// from cur to the chord prev→next: exactly the error introduced by dropping the vertex. It is
    /// compared against a precision floor rather than an exact zero because the drawing's own
    /// coordinates are not bit-exactly collinear — see
    /// <see cref="NSAlignmentCrawlConstants.CollinearDeviation"/>.
    ///
    /// <paramref name="pinned"/> governs the one remaining tolerance, which is not a collinearity test
    /// at all: an ordinary vertex is absorbed by a neighbour lying within the 25 mm connection
    /// tolerance, which is how duplicate vertices from drafting and from curve splitting disappear. A
    /// component joint is exempt — for a joint, exact collinearity is the only thing that removes it.
    /// </summary>
    private static bool IsRedundantStraightNode(
        (Point2d Pt, double OutBulge) prev,
        (Point2d Pt, double OutBulge) cur,
        (Point2d Pt, double OutBulge) next,
        bool pinned)
    {
        // An arc on either side means this vertex is an arc endpoint — never redundant. A straight
        // segment stores a bulge of exactly 0, and reversing negates it, so this comparison is exact.
        if (prev.OutBulge != 0.0 || cur.OutBulge != 0.0)
        {
            return false;
        }

        Vector2d incoming = prev.Pt.GetVectorTo(cur.Pt);
        Vector2d outgoing = cur.Pt.GetVectorTo(next.Pt);

        // The same point twice carries no direction, so cur adds nothing whatever its neighbours do.
        if (IsZero(incoming) || IsZero(outgoing))
        {
            return true;
        }

        // The only tolerance left, and it is not a collinearity test: an ordinary vertex sitting on
        // top of its neighbour is drafting/split noise. A component joint is exempt.
        if (!pinned
            && (incoming.Length <= NSAlignmentCrawlConstants.Tolerance
                || outgoing.Length <= NSAlignmentCrawlConstants.Tolerance))
        {
            return true;
        }

        // A reversal is never a straight run, however collinear the three points are: on a spur that
        // doubles back, cur sits exactly on the prev→next line yet is the whole point of the detour.
        if (incoming.DotProduct(outgoing) <= 0.0)
        {
            return false;
        }

        // |2 x triangle area| / base = perpendicular distance from cur to the chord.
        Vector2d chord = prev.Pt.GetVectorTo(next.Pt);
        double doubleArea = Math.Abs((chord.X * incoming.Y) - (chord.Y * incoming.X));
        return doubleArea <= NSAlignmentCrawlConstants.CollinearDeviation * chord.Length;
    }

    private static bool IsZero(Vector2d v) => v.X == 0.0 && v.Y == 0.0;

    public static Polyline? Build(
        IReadOnlyList<(Point2d Pt, double OutBulge)> vertices, string layer, CrawlPinSet? pins = null)
    {
        vertices = Weed(vertices, pins);
        if (vertices.Count < 2)
        {
            return null;
        }

        Polyline pl = new();
        for (int i = 0; i < vertices.Count; i++)
        {
            pl.AddVertexAt(i, vertices[i].Pt, vertices[i].OutBulge, 0.0, 0.0);
        }

        pl.Closed = false;
        pl.Layer = layer;
        pl.Normal = Vector3d.ZAxis;
        pl.Elevation = 0.0;
        return pl;
    }
}
