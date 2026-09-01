using Autodesk.AutoCAD.Geometry;

namespace IntersectUtilities.MPE.NSAlignmentCrawl;

/// <summary>
/// The points the weeder is never allowed to remove: every component block's connection ports, its
/// centre, and the endpoints of its own centreline chains. These are the physical joints of the
/// network — where a pipe stops and a fitting begins — so an alignment that weeds one away stops
/// describing the fitting it runs through, and visibly passes beside the joint instead.
///
/// Matching is positional (within <see cref="NSAlignmentCrawlConstants.Tolerance"/>) rather than a
/// per-vertex flag on purpose. The path is round-tripped through a Polyline whenever the direction
/// is flipped, and a Polyline vertex has nowhere to carry a flag — positions survive that round
/// trip, flags would not. The 25 mm radius is the same slack the graph already allows between a
/// pipe end and its port, so the emitted pipe vertex matches its port even when they are not
/// exactly coincident in the drawing.
/// </summary>
internal sealed class CrawlPinSet
{
    private const double Tol = NSAlignmentCrawlConstants.Tolerance;

    private readonly Dictionary<(long, long), List<Point2d>> _cells = [];

    public static CrawlPinSet FromSnapshot(NSAlignmentCrawlSnapshot snapshot)
    {
        CrawlPinSet set = new();
        foreach (CrawlComponent component in snapshot.Components)
        {
            set.Add(component.Center);

            foreach (Point2d port in component.Ports)
            {
                set.Add(port);
            }

            // A 2-port component crawls along its own centreline, whose ends are the real graph nodes
            // (the MuffeIntern ports are only the nominal positions), so pin both.
            foreach (IReadOnlyList<(Point2d Pt, double Bulge)> centerline in component.Centerlines)
            {
                if (centerline.Count < 2)
                {
                    continue;
                }

                set.Add(centerline[0].Pt);
                set.Add(centerline[^1].Pt);
            }
        }

        return set;
    }

    public bool IsPinned(Point2d p)
    {
        long kx = Key(p.X);
        long ky = Key(p.Y);

        for (long dx = -1; dx <= 1; dx++)
        {
            for (long dy = -1; dy <= 1; dy++)
            {
                if (!_cells.TryGetValue((kx + dx, ky + dy), out List<Point2d>? bucket))
                {
                    continue;
                }

                foreach (Point2d q in bucket)
                {
                    if (q.GetDistanceTo(p) <= Tol)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private void Add(Point2d p)
    {
        (long, long) key = (Key(p.X), Key(p.Y));
        if (!_cells.TryGetValue(key, out List<Point2d>? bucket))
        {
            bucket = [];
            _cells[key] = bucket;
        }

        bucket.Add(p);
    }

    private static long Key(double v) => (long)Math.Round(v / Tol);
}
