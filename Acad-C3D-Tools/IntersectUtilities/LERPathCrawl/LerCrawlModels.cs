using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace IntersectUtilities.LerPathCrawl;

internal enum LerCrawlStatus { Success, Failure }

internal readonly record struct LerCrawlResult<T>(LerCrawlStatus Status, T Value, string Error) where T : struct
{
    public static LerCrawlResult<T> Ok(T value) => new(LerCrawlStatus.Success, value, string.Empty);
    public static LerCrawlResult<T> Fault(string error) => new(LerCrawlStatus.Failure, default, error);

#pragma warning disable CS8524 // Only the declared status values are constructed here.
    public TResult Match<TResult>(Func<T, TResult> success, Func<string, TResult> failure) => Status switch
    {
        LerCrawlStatus.Success => success(Value),
        LerCrawlStatus.Failure => failure(Error)
    };
#pragma warning restore CS8524
}

internal static class LerCrawlSettings
{
    public const string Layer = "0-REFERENCELINE";
    public const double JoinTolerance = 0.025; // Drawing units, matching NSALIGNMENTCRAWL (25 mm in metre drawings).
    public const double GeometryTolerance = 1e-7;
}

internal readonly record struct LerCrawlSegment(
    Point2d Start, Point2d End, double Bulge, double StartWidth, double EndWidth, ObjectId SourceId)
{
    public Polyline CreateCurve()
    {
        var curve = new Polyline();
        curve.AddVertexAt(0, Start, Bulge, 0, 0);
        curve.AddVertexAt(1, End, 0, 0, 0);
        return curve;
    }

    public LerCrawlSegment Slice(Polyline curve, double from, double to)
    {
        Point3d start = curve.GetPointAtParameter(from);
        Point3d end = curve.GetPointAtParameter(to);
        return new LerCrawlSegment(new Point2d(start.X, start.Y), new Point2d(end.X, end.Y),
            Math.Tan(Math.Atan(Bulge) * (to - from)),
            StartWidth + (EndWidth - StartWidth) * from,
            StartWidth + (EndWidth - StartWidth) * to, SourceId);
    }

    public LerCrawlSegment Reverse() => new(End, Start, -Bulge, EndWidth, StartWidth, SourceId);
}

internal readonly record struct LerCrawlSource(
    IReadOnlyList<LerCrawlSegment> Segments, ObjectId SelectedId, string SourceLayer,
    string XrefName, Point3d StartPick);

internal readonly record struct LerCrawlRoute(IReadOnlyList<LerCrawlSegment> Segments);
internal readonly record struct LerCrawlStart(LerCrawlSession Session);
