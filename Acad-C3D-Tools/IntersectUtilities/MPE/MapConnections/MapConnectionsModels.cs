using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace IntersectUtilities.MPE.MapConnections;

/// <summary>
/// The graph rectangle of one profile view in model space, with the linear station ↔ X mapping of an
/// L-to-R view. Pure data, so it outlives the transaction it was read in.
/// </summary>
internal sealed record LpProfileView(
    string Name, double StationStart, double StationEnd, double XStart, double XEnd, double YMin, double YMax)
{
    public double MidY => (YMin + YMax) / 2.0;

    public double XAt(double station) =>
        XStart + (station - StationStart) * (XEnd - XStart) / (StationEnd - StationStart);

    public double StationAt(double x) =>
        StationStart + (x - XStart) * (StationEnd - StationStart) / (XEnd - XStart);

    public Point2d PointAt(double station) => new(XAt(station), MidY);

    public Extents3d Extents => new(
        new Point3d(Math.Min(XStart, XEnd), YMin, 0.0),
        new Point3d(Math.Max(XStart, XEnd), YMax, 0.0));

    /// <summary>Distance from a point to the graph rectangle (0 inside).</summary>
    public double DistanceTo(Point2d p)
    {
        double minX = Math.Min(XStart, XEnd), maxX = Math.Max(XStart, XEnd);
        double dx = p.X < minX ? minX - p.X : p.X > maxX ? p.X - maxX : 0.0;
        double dy = p.Y < YMin ? YMin - p.Y : p.Y > YMax ? p.Y - YMax : 0.0;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}

/// <summary>One alignment (= one LP): its plan geometry sampled for the network view, and its profile view if the drawing has one.</summary>
internal sealed record LpLine(
    string Name, double StartStation, double Length, IReadOnlyList<Point2d> PlanSamples, LpProfileView? View);

internal enum LpConnectionKind
{
    /// <summary>The end of <see cref="LpConnection.To"/> lies on the interior of <see cref="LpConnection.From"/>.</summary>
    Branch,

    /// <summary>Two alignments meet end to end; From is the lower-numbered one.</summary>
    EndToEnd,

    /// <summary>A branch block on From points to an NA pipeline, which has no alignment and no LP.</summary>
    Na,
}

internal sealed record LpConnection(
    string From, double FromStation, string To, double ToStation, Point2d PlanPoint, LpConnectionKind Kind, string? Label)
{
    public bool Involves(string lp) => From == lp || To == lp;
}

/// <summary>A DRISizeChangeAnno branch marker read off a profile view.</summary>
internal sealed record LpBranchBlock(string Host, double Station, Point2d Position, string Type, string Right);

internal sealed record LpHopTarget(string Lp, double Station);

/// <summary>
/// A spot on a profile view that FWD can hop from: a connection, a branch block, or both. The marker is the
/// block's position when a block sits there, otherwise the connection's station at the view's mid height.
/// </summary>
internal sealed record LpHopPoint(
    string Host, double Station, Point2d Marker, string? Label, IReadOnlyList<LpHopTarget> Targets, string? NaName);
