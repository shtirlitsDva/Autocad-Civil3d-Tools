using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using IntersectUtilities.PipelineNetworkSystem;
using IntersectUtilities.PipelineNetworkSystem.PipelineSizeArray;
using IntersectUtilities.PlanDetailing;
using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.DataManager.CsvData;
using IntersectUtilities.UtilsCommon.Enums;

using System;
using System.Collections.Generic;
using System.Linq;

using static IntersectUtilities.PipeScheduleV2.PipeScheduleV2;

using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;

namespace IntersectUtilities.NdhTrace;

/// <summary>
/// One stretch of constant pipe identity along a traced Centreline, measured as
/// distance along that Centreline.
/// </summary>
/// <param name="StartDist">Centreline distance where the stretch starts.</param>
/// <param name="EndDist">Centreline distance where the stretch ends.</param>
/// <param name="System">The stretch's pipe system.</param>
/// <param name="Type">The stretch's pipe type (Frem, Retur and Enkelt are the bonded pair).</param>
/// <param name="Dn">DN for steel, outer diameter in mm for the others.</param>
/// <param name="Series">The legacy series read with the stretch.</param>
/// <param name="ChangeDist">
/// Where the change INTO this stretch stands: the centre of the part that makes
/// it, where the new pipeline centres its own part. The first stretch starts
/// the pipeline and has no change; its ChangeDist is its StartDist.
/// </param>
/// <param name="HoldsPipeOrPart">
/// Whether anything of the stretch's own lies on it: a legacy pipe of its
/// identity, or a part standing inside it (a valve, an elbow). A stretch with
/// neither is only the length of the parts that make its changes - a
/// materialeskift welded to the end of a Y-model - and is no pipe at all.
/// </param>
internal readonly record struct LegacyIdentitySpan(
    double StartDist,
    double EndDist,
    PipeSystemEnum System,
    PipeTypeEnum Type,
    int Dn,
    PipeSeriesEnum Series,
    double ChangeDist,
    bool HoldsPipeOrPart)
{
    /// <summary>One identity as the pipeline sees it: Frem, Retur and Enkelt are all the bonded pair.</summary>
    public static (PipeSystemEnum System, bool Twin, int Dn) IdentityOf(
        PipeSystemEnum system, PipeTypeEnum type, int dn) => (system, type == PipeTypeEnum.Twin, dn);

    public (PipeSystemEnum System, bool Twin, int Dn) Identity => IdentityOf(System, Type, Dn);
}

internal enum LegacyCornerKind
{
    /// <summary>A fixed-angle elbow fitting: the pipe really turns sharply there.</summary>
    Elbow,
    /// <summary>An F-model: twin and bonded meet at a corner through one fitting.</summary>
    FModel,
}

/// <summary>
/// A component that puts a sharp corner into the Centreline near its position.
/// NominalTurn is the turn the part is made for, in radians; NaN for a part
/// made to any angle.
/// </summary>
internal readonly record struct LegacyCorner(Point2d Position, LegacyCornerKind Kind, double NominalTurn);

/// <summary>
/// A legacy FJV pipeline reduced to what a new pipeline needs: its exact
/// Centreline, the identity spans along it and the components that make its
/// sharp corners. The Centreline is in memory, not database resident; the
/// trace owns and disposes it.
/// </summary>
internal sealed class LegacyPipelineTrace : IDisposable
{
    public LegacyPipelineTrace(
        string name,
        Polyline centreline,
        IReadOnlyList<LegacyIdentitySpan> spans,
        IReadOnlyList<LegacyCorner> corners)
    {
        Name = name;
        Centreline = centreline;
        Spans = spans;
        Corners = corners;
    }

    public string Name { get; }
    public Polyline Centreline { get; }
    public IReadOnlyList<LegacyIdentitySpan> Spans { get; }
    public IReadOnlyList<LegacyCorner> Corners { get; }

    public void Dispose() => Centreline.Dispose();
}

internal sealed class LegacyTraceResult : IDisposable
{
    public List<LegacyPipelineTrace> Traces { get; } = new List<LegacyPipelineTrace>();
    /// <summary>Pipelines that produced no trace, with the reason.</summary>
    public List<string> Skipped { get; } = new List<string>();
    /// <summary>
    /// The pipes and components of every named legacy pipeline, traced or not,
    /// by pipeline name. Database resident in the source drawing: valid only
    /// while its transaction is open.
    /// </summary>
    public Dictionary<string, List<Entity>> Groups { get; } = new Dictionary<string, List<Entity>>();

    public void Dispose()
    {
        foreach (LegacyPipelineTrace t in Traces) t.Dispose();
    }
}

/// <summary>
/// Reads every legacy FJV pipeline of a drawing into a
/// <see cref="LegacyPipelineTrace"/>. A pipeline is the set of pipes and
/// components sharing one BelongsToAlignment value. Its geometry comes from
/// <see cref="FjvCentreline"/> run on that set alone, so frem and retur are
/// only ever paired within their own pipeline. Its identity comes from the
/// pipeline's <see cref="IPipelineSizeArrayV2"/>, built on a pipeline stationed
/// along that Centreline, so size stations are Centreline distances.
/// </summary>
internal static class FjvLegacyPipelineReader
{
    //Shorter size entries are the empty entry of a size block that sits on a
    //pipeline end.
    private const double MinSpanLength = 0.001;
    //How far apart two Centreline pieces of one pipeline may end and still be
    //chained - the two halves of a pipeline fed through a tee meet within ~15 mm
    //on the fixture.
    private const double ChainTol = 0.05;

    public static LegacyTraceResult Read(Database fjvDb, Transaction tx)
    {
        LegacyTraceResult result = new LegacyTraceResult();
        FjvDynamicComponents fjv = Csv.FjvDynamicComponents;
        PropertySetHelper psh = new PropertySetHelper(fjvDb);

        try
        {
            IEnumerable<IGrouping<string, Entity>> pipelines = fjvDb
                .GetFjvEntities(tx, true, true)
                .GroupBy(psh.Pipeline.BelongsToAlignment)
                .OrderBy(x => x.Key);

            foreach (IGrouping<string, Entity> pipeline in pipelines)
            {
                if (pipeline.Key.IsNoE())
                {
                    result.Skipped.Add(
                        $"{pipeline.Count()} element(s) belong to no pipeline.");
                    continue;
                }

                List<Entity> members = pipeline.ToList();
                result.Groups[pipeline.Key] = members;
                try
                {
                    result.Traces.Add(Trace(pipeline.Key, members, fjv, tx));
                }
                catch (Exception ex)
                {
                    result.Skipped.Add($"{pipeline.Key}: {ex.Message}");
                }
            }

            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    private static LegacyPipelineTrace Trace(
        string name, List<Entity> ents, FjvDynamicComponents fjv, Transaction tx)
    {
        List<Polyline> pipes = ents
            .OfType<Polyline>()
            .Where(x => GetPipeType(x) is
                PipeTypeEnum.Twin or PipeTypeEnum.Frem or PipeTypeEnum.Retur)
            .ToList();
        if (pipes.Count == 0) throw new Exception("has no TWIN, FREM or RETUR pipes.");

        List<BlockReference> blocks = ents.OfType<BlockReference>().ToList();

        FjvCentrelineResult cl = FjvCentreline.Build(pipes, blocks, fjv, tx);
        Polyline? centreline = null;
        try
        {
            centreline = ChainPieces(cl.Centrelines);
            if (centreline == null)
                throw new Exception(
                    $"traced to {cl.Centrelines.Count} centrelines that do not chain " +
                    $"into one. {Diagnose(cl)}");

            //Stationed along the Centreline itself, so the size array's stations
            //ARE Centreline distances.
            IPipelineV2 pipeline = PipelineV2Factory.CreateFromTopology(
                ents, (Polyline)centreline.Clone());
            IPipelineSizeArrayV2 sizes = PipelineSizeArrayFactory.CreateSizeArray(pipeline);
            List<(double Station, BlockReference Block)> stationed = blocks
                .Select(x => (pipeline.GetBlockStation(x), x))
                .ToList();
            List<(double Station, BlockReference Block)> sizeBlocks = stationed
                .Where(x => x.Block.ReadDynamicCsvProperty(DynamicProperty.Function, false) == "SizeArray")
                .ToList();
            List<LegacyPipe> pipePieces = pipes.Select(x => PipeOn(x, centreline)).ToList();

            List<LegacyIdentitySpan> spans = new List<LegacyIdentitySpan>();
            for (int i = 0; i < sizes.Length; i++)
            {
                SizeEntryV2 s = sizes[i];
                double start = i == 0 ? 0.0 : Clamp(s.StartStation, centreline.Length);
                double end = i == sizes.Length - 1
                    ? centreline.Length
                    : Clamp(s.EndStation, centreline.Length);

                //A size block sitting on a pipeline end (a reducer at the very
                //end) yields an empty size entry: there is no pipe of it.
                if (end - start < MinSpanLength) continue;

                (PipeSystemEnum, bool, int) identity = LegacyIdentitySpan.IdentityOf(s.System, s.Type, s.DN);
                spans.Add(new LegacyIdentitySpan(
                    start, end, s.System, s.Type, s.DN, s.Series,
                    i == 0 ? start : ChangeCentre(sizeBlocks, s.StartStation, centreline, tx),
                    pipePieces.Any(p => p.Identity == identity &&
                        Math.Min(p.EndDist, end) - Math.Max(p.StartDist, start) >= MinSpanLength) ||
                    stationed.Any(x => x.Station > start + MinSpanLength && x.Station < end - MinSpanLength)));
            }

            if (spans.Count == 0)
                throw new Exception("size array has no stretch of non-zero length.");

            //Dropping an empty entry leaves a gap; close it so the spans tile
            //the whole Centreline.
            spans[0] = spans[0] with { StartDist = 0.0 };
            spans[spans.Count - 1] = spans[spans.Count - 1] with { EndDist = centreline.Length };
            for (int i = 1; i < spans.Count; i++)
                spans[i] = spans[i] with { StartDist = spans[i - 1].EndDist };
            spans[0] = spans[0] with { ChangeDist = 0.0 };

            return new LegacyPipelineTrace(name, centreline, spans, Corners(blocks));
        }
        catch
        {
            centreline?.Dispose();
            throw;
        }
        finally
        {
            foreach (Polyline pl in cl.RunLines.Concat(cl.Centrelines))
                if (!ReferenceEquals(pl, centreline)) pl.Dispose();
        }
    }

    private static string Diagnose(FjvCentrelineResult cl)
    {
        List<string> parts = new List<string>();
        if (cl.UnpairedRuns.Count > 0)
            parts.Add($"Unpaired frem run(s): {string.Join(" | ", cl.UnpairedRuns)}.");
        if (cl.UnjoinedTransitions.Count > 0)
            parts.Add($"Unjoined transition(s): {string.Join(" | ", cl.UnjoinedTransitions)}.");
        if (cl.MixedRuns.Count > 0)
            parts.Add($"Mixed run(s): {string.Join(" | ", cl.MixedRuns)}.");
        return string.Join(" ", parts);
    }

    //The turn, in degrees, each elbow part is made for; NaN for a part made
    //to any angle.
    private static readonly Dictionary<PipelineElementType, double> ElbowTurns = new()
    {
        [PipelineElementType.PræisoleretBøjning90gr] = 90.0,
        [PipelineElementType.PræisoleretBøjning45gr] = 45.0,
        [PipelineElementType.Bøjning45gr] = 45.0,
        [PipelineElementType.Bøjning30gr] = 30.0,
        [PipelineElementType.Bøjning15gr] = 15.0,
        [PipelineElementType.PræisoleretBøjningVariabel] = double.NaN,
        [PipelineElementType.Kedelrørsbøjning] = double.NaN,
    };

    private static List<LegacyCorner> Corners(IEnumerable<BlockReference> blocks)
    {
        List<LegacyCorner> corners = new List<LegacyCorner>();
        foreach (BlockReference br in blocks)
        {
            if (!TryGetType(br, out PipelineElementType type)) continue;

            if (ElbowTurns.TryGetValue(type, out double deg))
                corners.Add(new LegacyCorner(
                    br.Position.To2d(), LegacyCornerKind.Elbow, deg * Math.PI / 180.0));
            else if (type == PipelineElementType.F_Model)
                corners.Add(new LegacyCorner(
                    br.Position.To2d(), LegacyCornerKind.FModel, double.NaN));
        }
        return corners;
    }

    /// <summary>One legacy pipe, as the stretch of Centreline it lies along.</summary>
    private readonly record struct LegacyPipe(
        double StartDist, double EndDist, (PipeSystemEnum System, bool Twin, int Dn) Identity);

    private static LegacyPipe PipeOn(Polyline pipe, Polyline centreline)
    {
        double a = DistAt(centreline, pipe.StartPoint), b = DistAt(centreline, pipe.EndPoint);
        return new LegacyPipe(Math.Min(a, b), Math.Max(a, b),
            LegacyIdentitySpan.IdentityOf(GetPipeSystem(pipe), GetPipeType(pipe), GetPipeDN(pipe)));
    }

    /// <summary>
    /// The Centreline distance of the centre of the size block making the change
    /// at <paramref name="station"/>: the size array breaks at each such block's
    /// station, so it is the one standing there. The centre is the middle of the
    /// block's own geometry, taken in block space so it is the middle along the
    /// part's axis - a Y-model is inserted at one of its ends.
    /// </summary>
    private static double ChangeCentre(
        List<(double Station, BlockReference Block)> sizeBlocks, double station,
        Polyline centreline, Transaction tx)
    {
        BlockReference maker = sizeBlocks.MinBy(x => Math.Abs(x.Station - station)).Block;
        return DefinitionCentre(maker, tx) is Point3d centre
            ? DistAt(centreline, centre)
            : Clamp(station, centreline.Length);
    }

    private static double DistAt(Polyline centreline, Point3d p) =>
        centreline.GetDistAtPoint(centreline.GetClosestPointTo(p, false));

    private static Point3d? DefinitionCentre(BlockReference br, Transaction tx)
    {
        BlockTableRecord btr = (BlockTableRecord)tx.GetObject(br.BlockTableRecord, OpenMode.ForRead);
        Extents3d? ext = null;
        foreach (ObjectId id in btr)
        {
            if (tx.GetObject(id, OpenMode.ForRead) is not Entity e ||
                !e.Visible || e is AttributeDefinition) continue;
            if (e.Bounds is not Extents3d x) continue;

            if (ext is Extents3d grown)
            {
                grown.AddExtents(x);
                ext = grown;
            }
            else ext = x;
        }
        if (ext is not Extents3d all) return null;

        Point3d mid = all.MinPoint + (all.MaxPoint - all.MinPoint) / 2.0;
        return mid.TransformBy(br.BlockTransform);
    }

    //GetPipelineType throws for a type the schedule does not know; such a
    //block is no fitting this reader needs.
    private static bool TryGetType(BlockReference br, out PipelineElementType type)
    {
        try
        {
            type = br.GetPipelineType();
            return true;
        }
        catch (Exception)
        {
            type = default;
            return false;
        }
    }

    private static double Clamp(double station, double length) =>
        Math.Max(0.0, Math.Min(station, length));

    /// <summary>
    /// Chains the Centreline pieces of one pipeline end to end into a new
    /// polyline, or returns the single piece as it is. A pipeline fed from the
    /// middle through another pipeline's tee comes out as two pieces meeting at
    /// that tee, because the tee is not part of this pipeline's elements.
    /// Returns null when the pieces do not form one open chain.
    /// </summary>
    private static Polyline? ChainPieces(List<Polyline> pieces)
    {
        if (pieces.Count == 0) return null;
        if (pieces.Count == 1) return pieces[0];

        List<Polyline> left = pieces.ToList();
        Polyline chain = new Polyline();
        AppendVertices(chain, left[0], false);
        left.RemoveAt(0);

        bool grew = true;
        while (grew && left.Count > 0)
        {
            grew = false;
            for (int i = 0; i < left.Count; i++)
            {
                Polyline p = left[i];
                //Only an end-to-end meeting chains; two candidates at one end
                //would be a real branch.
                if (p.StartPoint.DistanceHorizontalTo(chain.EndPoint) <= ChainTol)
                    AppendVertices(chain, p, false);
                else if (p.EndPoint.DistanceHorizontalTo(chain.EndPoint) <= ChainTol)
                    AppendVertices(chain, p, true);
                else if (p.EndPoint.DistanceHorizontalTo(chain.StartPoint) <= ChainTol ||
                         p.StartPoint.DistanceHorizontalTo(chain.StartPoint) <= ChainTol)
                {
                    chain.ReverseCurve();
                    AppendVertices(chain, p,
                        p.EndPoint.DistanceHorizontalTo(chain.EndPoint) <= ChainTol);
                }
                else continue;

                left.RemoveAt(i);
                grew = true;
                break;
            }
        }

        if (left.Count == 0) return chain;
        chain.Dispose();
        return null;
    }

    /// <summary>
    /// Appends <paramref name="src"/> to <paramref name="target"/>, reversed
    /// when asked. The first vertex of <paramref name="src"/> is merged into the
    /// last vertex of <paramref name="target"/> when they lie within
    /// <see cref="ChainTol"/>.
    /// </summary>
    internal static void AppendVertices(Polyline target, Polyline src, bool reversed)
    {
        int n = src.NumberOfVertices;
        for (int i = 0; i < n; i++)
        {
            int vi = reversed ? n - 1 - i : i;
            //Reversing a polyline moves each bulge to the other end of its
            //segment and flips its sign.
            double bulge = i == n - 1
                ? 0.0
                : reversed ? -src.GetBulgeAt(vi - 1) : src.GetBulgeAt(vi);
            Point2d p = src.GetPoint2dAt(vi);

            int m = target.NumberOfVertices;
            if (i == 0 && m > 0 && target.GetPoint2dAt(m - 1).GetDistanceTo(p) <= ChainTol)
            {
                target.SetBulgeAt(m - 1, bulge);
                continue;
            }
            target.AddVertexAt(m, p, bulge, 0.0, 0.0);
        }
    }
}
