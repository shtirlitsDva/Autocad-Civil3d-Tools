using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace IntersectUtilities.LerPathCrawl;

internal static class LerCrawlPolylineBuilder
{
    public static Polyline Centerline(IReadOnlyList<LerCrawlSegment> segments)
    {
        var polyline = new Polyline();
        foreach (var segment in segments)
        {
            if (polyline.NumberOfVertices == 0)
                polyline.AddVertexAt(0, segment.Start, segment.Bulge, 0, 0);
            else
            {
                int last = polyline.NumberOfVertices - 1;
                Point2d previous = polyline.GetPoint2dAt(last);
                if (previous.GetDistanceTo(segment.Start) > LerCrawlSettings.GeometryTolerance)
                {
                    // Keep small gaps between connected source endpoints explicit.
                    polyline.SetBulgeAt(last, 0);
                    polyline.AddVertexAt(polyline.NumberOfVertices, segment.Start, segment.Bulge, 0, 0);
                }
                else
                    polyline.SetBulgeAt(last, segment.Bulge);
            }
            polyline.AddVertexAt(polyline.NumberOfVertices, segment.End, 0, 0, 0);
        }
        return polyline;
    }
}
