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

    public static Polyline? Build(IReadOnlyList<(Point2d Pt, double OutBulge)> vertices, string layer)
    {
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
