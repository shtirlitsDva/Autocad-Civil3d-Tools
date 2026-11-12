using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using CSharpFunctionalExtensions;

using IntersectUtilities.MPE.NSAlignmentCrawl;
using IntersectUtilities.UtilsCommon.Enums;

using NetTopologySuite.Geometries;
using NetTopologySuite.LinearReferencing;
using NetTopologySuite.Operation.Distance;

using Norsyn.AecPropertySets;

namespace IntersectUtilities.MPE.TblCrawl;

/// <summary>A stretch of a path that runs on frem or on retur pipe, by station along the path.</summary>
internal sealed record EnkeltRun(PipeTypeEnum Role, double From, double To)
{
    public PipeTypeEnum Partner => Role == PipeTypeEnum.Frem ? PipeTypeEnum.Retur : PipeTypeEnum.Frem;

    public override string ToString() => $"{Role} {From:0.00}-{To:0.00} m";
}

internal sealed record PartnerLine(EnkeltRun Run, Polyline Polyline, double MeanDistance, double RunLength);

/// <summary>
/// The enkelt partner (decision 1a): a crawled path follows only one of a bonded frem/retur pair, so for
/// each part of it on frem (retur) the partner retur (frem) is crawled between the projections of that
/// part's two ends, with the same CrawlSession. The partner must stay near the part it pairs with, or it
/// is skipped with the reason.
/// </summary>
internal static class TblPartner
{
    public const double MaxProjection = 2.0;
    public const double MaxMeanDistance = 2.0;

    // 10 % of the part's length, plus 1 m for short parts that turn a bend on the other side of the pair.
    private const double LengthShare = 0.10;
    private const double LengthSlack = 1.0;
    private const double SampleStep = 0.1;

    /// <summary>
    /// The parts of <paramref name="path"/> that lie on frem or retur pipe. Pipe pieces are taken where they
    /// coincide with the path, then neighbours of one role are joined across the components between them;
    /// a piece of another role (twin, the partner) between two pieces keeps them apart.
    /// </summary>
    public static IReadOnlyList<EnkeltRun> EnkeltRuns(LineString path, IReadOnlyList<(PipeTypeEnum Type, LineString Line)> pipes)
    {
        Geometry near = TblStretchGeometry.Corridor(path, TblStretch.Tolerance);
        Geometry coincident = TblStretchGeometry.Corridor(path, TblStretch.CoincidenceTolerance);
        LengthIndexedLine index = new(path);
        List<(PipeTypeEnum Role, double From, double To)> pieces = [];
        foreach ((PipeTypeEnum type, LineString line) in pipes)
        {
            if (!line.EnvelopeInternal.Intersects(near.EnvelopeInternal))
            {
                continue;
            }

            foreach (LineString part in TblStretchGeometry.Parts(line.Intersection(near)))
            {
                if (part.Intersection(coincident).Length < TblStretch.MinOverlap)
                {
                    continue;
                }

                double a = index.Project(part.StartPoint.Coordinate);
                double b = index.Project(part.EndPoint.Coordinate);
                pieces.Add((type, Math.Min(a, b), Math.Max(a, b)));
            }
        }

        List<EnkeltRun> runs = [];
        foreach ((PipeTypeEnum role, double from, double to) in pieces.OrderBy(p => p.From))
        {
            if (runs.Count > 0 && runs[^1].Role == role)
            {
                runs[^1] = runs[^1] with { To = Math.Max(runs[^1].To, to) };
            }
            else
            {
                runs.Add(new EnkeltRun(role, from, to));
            }
        }

        return runs.Where(r => r.Role is PipeTypeEnum.Frem or PipeTypeEnum.Retur).ToList();
    }

    public static Result<PartnerLine, AecError> Crawl(
        EnkeltRun run, LineString path, IReadOnlyList<(PipeTypeEnum Type, LineString Line)> pipes,
        CrawlNetwork net, CrawlPinSet pins, string layer)
    {
        LengthIndexedLine index = new(path);
        LineString runLine = (LineString)index.ExtractLine(run.From, run.To);
        List<LineString> partners = pipes.Where(p => p.Type == run.Partner).Select(p => p.Line).ToList();

        return Project(index.ExtractPoint(run.From), partners, "start")
            .Bind(start => Project(index.ExtractPoint(run.To), partners, "slut").Map(end => (start, end)))
            .Bind(ends => StartAt(net, ends.start).Bind(session => PathTo(session, ends.end)))
            .Bind(vertices => Maybe.From(NSAlignmentCrawlPolylineBuilder.Build(vertices, layer, pins))
                .ToResult(AecError.Drawing("for få punkter i partnerens sti")))
            .Bind(polyline => Guard(run, runLine, polyline));
    }

    private static Result<Coordinate, AecError> Project(Coordinate at, List<LineString> partners, string end)
    {
        Geometry point = new GeometryFactory().CreatePoint(at);
        Maybe<(Coordinate Point, double Distance)> nearest = partners
            .Select(line => DistanceOp.NearestPoints(line, point)[0])
            .Select(c => (Point: c, Distance: c.Distance(at)))
            .OrderBy(x => x.Distance)
            .TryFirst();
        return nearest
            .ToResult(AecError.Drawing("ingen partnerrør i xref'en"))
            .Ensure(n => n.Distance <= MaxProjection,
                n => AecError.Drawing($"intet partnerrør inden for {MaxProjection:0.0} m af {end}punktet (nærmeste {n.Distance:0.00} m)"))
            .Map(n => n.Point);
    }

    private static Result<CrawlSession, AecError> StartAt(CrawlNetwork net, Coordinate at)
    {
        CrawlSession? session = CrawlSession.Create(net, new Point3d(at.X, at.Y, 0.0), out string error);
        return Maybe.From(session).ToResult(AecError.Drawing(error));
    }

    private static Result<List<(Point2d Pt, double OutBulge)>, AecError> PathTo(CrawlSession session, Coordinate at) =>
        session.TryBuildPath(new Point3d(at.X, at.Y, 0.0), out List<(Point2d Pt, double OutBulge)> vertices)
            ? Result.Success<List<(Point2d Pt, double OutBulge)>, AecError>(vertices)
            : Result.Failure<List<(Point2d Pt, double OutBulge)>, AecError>(AecError.Drawing("ingen sti på partnerrøret mellem de to projektioner"));

    private static Result<PartnerLine, AecError> Guard(EnkeltRun run, LineString runLine, Polyline polyline)
    {
        LineString line = TblStretchGeometry.ToLineString(polyline);
        LengthIndexedLine index = new(line);
        int samples = Math.Max(2, (int)Math.Ceiling(line.Length / SampleStep) + 1);
        GeometryFactory factory = new();
        double mean = Enumerable.Range(0, samples)
            .Select(i => factory.CreatePoint(index.ExtractPoint(line.Length * i / (samples - 1))).Distance(runLine))
            .Average();
        double allowed = (LengthShare * runLine.Length) + LengthSlack;
        double difference = Math.Abs(line.Length - runLine.Length);

        if (mean <= MaxMeanDistance && difference <= allowed)
        {
            return Result.Success<PartnerLine, AecError>(new PartnerLine(run, polyline, mean, runLine.Length));
        }

        polyline.Dispose();
        return Result.Failure<PartnerLine, AecError>(AecError.Drawing(
            $"partnerens sti ligger ikke langs stykket (middelafstand {mean:0.00} m, maks {MaxMeanDistance:0.0}; " +
            $"længde {line.Length:0.00} mod {runLine.Length:0.00} m, maks forskel {allowed:0.00})"));
    }
}
