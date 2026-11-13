using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using CSharpFunctionalExtensions;

using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.Enums;

using Norsyn.AecPropertySets;

using AecDataType = Autodesk.Aec.PropertyData.DataType;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using Result = CSharpFunctionalExtensions.Result;

namespace IntersectUtilities.MPE.PlaceLongitudinalBlock;

/// <summary>
/// A polyline end the trim links to a block port: the cut end of a piece, or the end of a polyline that already
/// ended at the port, with nothing to remove (a touched end).
/// </summary>
internal sealed record TrimmedPiece(Polyline Polyline, EndType CutEnd, ComponentPort Port);

/// <summary>
/// What one trim did to one pipe: the pieces that now hold the pipe's start and its end, if any, and, by handle,
/// the end whose piece each component welded on the pipe lies on.
/// </summary>
internal sealed record TrimmedPipe(
    ObjectId OriginalId,
    string OriginalHandle,
    Maybe<TrimmedPiece> StartPiece,
    Maybe<TrimmedPiece> EndPiece,
    IReadOnlyDictionary<string, EndType> WeldSides)
{
    internal IEnumerable<TrimmedPiece> Pieces => new[] { StartPiece, EndPiece }.Choose();

    /// <summary>The piece that is the original polyline, reshaped; None when the original was erased.</summary>
    internal Maybe<TrimmedPiece> Survivor => Pieces.TryFirst(piece => piece.Polyline.ObjectId == OriginalId);

    /// <summary>The piece holding the pipe's start (<see cref="EndType.Start"/>) or its end.</summary>
    internal Maybe<TrimmedPiece> PieceHolding(EndType end) => end == EndType.Start ? StartPiece : EndPiece;
}

/// <summary>
/// Cuts the FJV pipe a placed component block sits on at the block's ports and deletes the stretch between the
/// outermost ones. The pipe is the FJV polylines of the pick's alignment; a port counts when it lies at most
/// <see cref="PortTolerance"/> from one. Every refusal, and every decision that depends on the drawing's data, is
/// made before anything is written.
/// </summary>
internal static class PipeTrimmer
{
    internal const double PortTolerance = 0.05;

    // A remainder shorter than this is not kept as a piece, and a polyline end this close to a cut point is touched.
    private const double MinimumPiece = 0.001;

    // A port on a neighbouring polyline must project onto this polyline's end, within this.
    private const double EndTolerance = 1e-6;

    private static readonly string PipelineSet = PSetDefs.DefinedSets.DriPipelineData.ToString();
    private static readonly string BelongsToAlignment = new PSetDefs.DriPipelineData().BelongsToAlignment.Name;
    private static readonly string FremtidSet = PSetDefs.DefinedSets.FJV_fremtid.ToString();
    private static readonly string LengthProperty = new PSetDefs.FJV_fremtid().Length.Name;

    /// <summary>One port projected onto one polyline: where along it, and how far from it in plan.</summary>
    private readonly record struct Projection(ComponentPort Port, double Parameter, double Along, double Offset)
    {
        internal bool IsOnPipe => Offset <= PortTolerance;
    }

    /// <summary>A polyline of the alignment that holds a port, with every on-pipe port projected onto it.</summary>
    private sealed record Host(Polyline Pipe, IReadOnlyList<Projection> Projections);

    /// <summary>The stretch of one polyline under the block, between its lowest and highest projection.</summary>
    private sealed record HostCut(Polyline Pipe, IReadOnlyList<Projection> Projections, Projection From, Projection To)
    {
        internal bool IsCut => To.Along - From.Along >= MinimumPiece;

        internal bool KeepsStart => From.Along > MinimumPiece;

        internal bool KeepsEnd => Pipe.Length - To.Along > MinimumPiece;
    }

    /// <summary>What the trim will do: the polylines it cuts, and the touched ends it only links.</summary>
    private sealed record TrimPlan(IReadOnlyList<HostCut> Cuts, IReadOnlyList<TrimmedPiece> Touches);

    /// <summary>One cut ready to write: the curves before and after the stretch, made in memory, and its decisions.</summary>
    private sealed record HostSplit(
        HostCut Cut,
        Maybe<Polyline> StartCurve,
        Maybe<Polyline> EndCurve,
        bool WritesLength,
        IReadOnlyDictionary<string, EndType> WeldSides);

    /// <summary>
    /// Trims the pipe under <paramref name="block"/>, then rewrites the DriGraph links. Success describes what
    /// happened to each polyline; a failure is the reason the pipe was left alone.
    /// </summary>
    internal static Result<string, AecError> Trim(
        Database db, Transaction tx, BlockReference block, string alignmentName, GraphLinks graph)
    {
        List<ComponentPort> ports = ComponentPorts.Read(block, tx);

        return HostsOf(db, tx, ports, alignmentName)
            .Bind(hosts => hosts.Traverse(host => CutOf(tx, host)))
            .Bind(PlanOf)
            .Bind(plan => graph.Load()
                .Bind(() => graph.CheckWritable(
                    plan.Cuts.Select(cut => cut.Pipe).ToList(),
                    plan.Touches.Select(touch => touch.Polyline).ToList()))
                .Map(() => plan))
            .Bind(plan => plan.Cuts
                .Traverse(cut => CheckWrites(tx, cut).Map(writesLength => (Cut: cut, WritesLength: writesLength)))
                .Bind(checkedCuts => SplitAll(checkedCuts, graph))
                .Map(splits => Apply(tx, block, splits, plan.Touches, graph)));
    }

    private static Result<IReadOnlyList<Host>, AecError> HostsOf(
        Database db, Transaction tx, IReadOnlyList<ComponentPort> ports, string alignmentName)
    {
        if (ports.Count == 0)
        {
            return Result.Failure<IReadOnlyList<Host>, AecError>(
                AecError.Drawing("blokken har ingen porte (MuffeIntern)"));
        }

        // The alignment first: a polyline of another alignment is never projected onto.
        return db.GetFjvPipes(tx)
            .Where(pipe => ports.Any(port => IsNearBounds(pipe, port.Position)))
            .Traverse(pipe => PropertySetFields.ReadText(pipe, tx, PipelineSet, BelongsToAlignment)
                .Map((Maybe<string> alignment) => (Pipe: pipe, Alignment: alignment)))
            .Map(read => read
                .Where(x => x.Alignment.Match(name => name == alignmentName, () => false))
                .Select(x => x.Pipe)
                .ToList())
            .Bind(pipes => pipes.Traverse(pipe => ports
                .Traverse(port => Project(pipe, port))
                .Map(projections => new Host(pipe, projections))))
            .Map(near => near.Where(host => host.Projections.Any(projection => projection.IsOnPipe)).ToList())
            .Bind(hosts => KeepOnPipePorts(hosts, ports.Count, alignmentName));
    }

    /// <summary>Refuses fewer than two on-pipe ports, and keeps only on-pipe ports' projections.</summary>
    private static Result<IReadOnlyList<Host>, AecError> KeepOnPipePorts(
        IReadOnlyList<Host> hosts, int portCount, string alignmentName)
    {
        HashSet<ComponentPort> onPipe = hosts
            .SelectMany(host => host.Projections.Where(projection => projection.IsOnPipe))
            .Select(projection => projection.Port)
            .ToHashSet();

        if (onPipe.Count < 2)
        {
            return Result.Failure<IReadOnlyList<Host>, AecError>(AecError.Drawing(
                $"kun {onPipe.Count} af blokkens {portCount} porte ligger på røret " +
                $"(maks. 0,05 m fra en FJV-polylinje på {alignmentName})"));
        }

        return Result.Success<IReadOnlyList<Host>, AecError>(hosts
            .Select(host => host with
            {
                Projections = host.Projections.Where(projection => onPipe.Contains(projection.Port)).ToList(),
            })
            .ToList());
    }

    private static bool IsNearBounds(Polyline pipe, Point3d point) =>
        pipe.Bounds is Extents3d box &&
        point.X >= box.MinPoint.X - PortTolerance && point.X <= box.MaxPoint.X + PortTolerance &&
        point.Y >= box.MinPoint.Y - PortTolerance && point.Y <= box.MaxPoint.Y + PortTolerance;

    /// <summary>The port's closest point on the polyline in plan (the port taken at the polyline's elevation).</summary>
    private static Result<Projection, AecError> Project(Polyline pipe, ComponentPort port) =>
        AutoCadCall.Run($"nærmeste punkt på polylinje {pipe.Handle}", () =>
        {
            Point3d closest = pipe.GetClosestPointTo(InPlaneOf(pipe, port.Position), false);
            double parameter = pipe.GetParameterAtPoint(closest);
            return new Projection(
                port, parameter, pipe.GetDistanceAtParameter(parameter), closest.DistanceHorizontalTo(port.Position));
        });

    /// <summary>The point moved to the polyline's elevation, so a closest point is measured in plan.</summary>
    internal static Point3d InPlaneOf(Polyline pipe, Point3d point) => new(point.X, point.Y, pipe.Elevation);

    /// <summary>
    /// The name of the entity's layer when it is locked and the entity is not already open for write in this
    /// transaction, so opening it for write (to change it or to attach a property set) would throw.
    /// </summary>
    internal static Maybe<string> LockedLayer(Entity entity, Transaction tx)
    {
        if (entity.IsWriteEnabled)
        {
            return Maybe<string>.None;
        }

        LayerTableRecord layer = (LayerTableRecord)tx.GetObject(entity.LayerId, OpenMode.ForRead);
        return layer.IsLocked ? Maybe.From(layer.Name) : Maybe<string>.None;
    }

    /// <summary>
    /// The stretch under the block on one polyline: from the lowest to the highest projection. A port on a
    /// neighbouring polyline (a joint) projects onto this polyline's end, so the polyline is cut at its own port.
    /// </summary>
    private static Result<HostCut, AecError> CutOf(Transaction tx, Host host)
    {
        Polyline pipe = host.Pipe;
        if (pipe.Closed)
        {
            return Result.Failure<HostCut, AecError>(AecError.Drawing($"polylinje {pipe.Handle} er lukket"));
        }

        Maybe<string> lockedLayer = LockedLayer(pipe, tx);
        if (lockedLayer.HasValue)
        {
            return Result.Failure<HostCut, AecError>(AecError.Drawing(
                $"polylinje {pipe.Handle} ligger på det låste lag {lockedLayer.GetValueOrDefault(string.Empty)}"));
        }

        bool neighboursAtEnds = host.Projections
            .Where(projection => !projection.IsOnPipe)
            .All(projection => projection.Along <= EndTolerance || pipe.Length - projection.Along <= EndTolerance);
        if (!neighboursAtEnds)
        {
            return Result.Failure<HostCut, AecError>(AecError.Drawing(
                $"kan ikke afgøre, hvilken del af polylinje {pipe.Handle} der ligger under blokken"));
        }

        return Result.Success<HostCut, AecError>(new HostCut(
            pipe,
            host.Projections,
            host.Projections.MinBy(projection => projection.Along),
            host.Projections.MaxBy(projection => projection.Along)));
    }

    /// <summary>
    /// Separates the polylines that are cut from those with nothing to remove; of those, an end an on-pipe port
    /// touches gets linked to the block. Refuses when nothing is cut.
    /// </summary>
    private static Result<TrimPlan, AecError> PlanOf(IReadOnlyList<HostCut> cuts)
    {
        List<HostCut> cut = cuts.Where(host => host.IsCut).ToList();
        if (cut.Count == 0)
        {
            return Result.Failure<TrimPlan, AecError>(AecError.Drawing("portene ligger oven i hinanden på røret"));
        }

        return cuts
            .Where(host => !host.IsCut)
            .Traverse(host => TouchedEnds(host))
            .Map(touched => new TrimPlan(cut, touched.SelectMany(ends => ends).ToList()));
    }

    private static Result<IReadOnlyList<TrimmedPiece>, AecError> TouchedEnds(HostCut host) =>
        TouchedEnd(host, EndType.Start, host.Pipe.StartPoint, host.Pipe.StartParam)
            .Bind((Maybe<TrimmedPiece> start) => TouchedEnd(host, EndType.End, host.Pipe.EndPoint, host.Pipe.EndParam)
                .Map((Maybe<TrimmedPiece> end) => (IReadOnlyList<TrimmedPiece>)new[] { start, end }.Choose().ToList()));

    /// <summary>
    /// The end touched by an on-pipe port: one whose projection onto the infinite line of the end segment (its
    /// tangent at the end) lies within <see cref="MinimumPiece"/> of the end, so it projects onto the end without
    /// clamping. A port beyond the end, however near, does not touch it.
    /// </summary>
    private static Result<Maybe<TrimmedPiece>, AecError> TouchedEnd(
        HostCut host, EndType end, Point3d endPoint, double endParameter) =>
        AutoCadCall
            .Run($"tangent ved enden af polylinje {host.Pipe.Handle}", () => host.Pipe.GetFirstDerivative(endParameter))
            .Map(tangent => PlanDirection(tangent).Bind(direction => host.Projections
                .Where(projection => projection.IsOnPipe)
                .Select(projection => (
                    projection.Port,
                    Along: Math.Abs(
                        ((projection.Port.Position.X - endPoint.X) * direction.X) +
                        ((projection.Port.Position.Y - endPoint.Y) * direction.Y))))
                .Where(x => x.Along <= MinimumPiece)
                .OrderBy(x => x.Along)
                .Select(x => new TrimmedPiece(host.Pipe, end, x.Port))
                .TryFirst()));

    /// <summary>The tangent's direction in plan as a unit vector; None for a degenerate end segment.</summary>
    private static Maybe<Vector2d> PlanDirection(Vector3d tangent)
    {
        double length = Math.Sqrt((tangent.X * tangent.X) + (tangent.Y * tangent.Y));
        return length > 1e-12
            ? Maybe.From(new Vector2d(tangent.X / length, tangent.Y / length))
            : Maybe<Vector2d>.None;
    }

    /// <summary>
    /// Refuses a pipe whose property sets the write phase could not write: every set, when a new piece must copy
    /// them, and FJV_fremtid Length, when a piece remains. Success says whether the pieces get Length.
    /// </summary>
    private static Result<bool, AecError> CheckWrites(Transaction tx, HostCut cut)
    {
        if (!cut.KeepsStart && !cut.KeepsEnd)
        {
            return Result.Success<bool, AecError>(false);
        }

        return (cut.KeepsStart && cut.KeepsEnd
                ? PropertySetFields.CheckCopyable(cut.Pipe, tx)
                : UnitResult.Success<AecError>())
            .Bind(() => PropertySetFields.CarriesWritable(cut.Pipe, tx, FremtidSet, LengthProperty, AecDataType.Real));
    }

    /// <summary>Splits every polyline in memory; on a failure, the curves already made are disposed.</summary>
    private static Result<IReadOnlyList<HostSplit>, AecError> SplitAll(
        IReadOnlyList<(HostCut Cut, bool WritesLength)> cuts, GraphLinks graph) =>
        cuts.Aggregate(
                Result.Success<List<HostSplit>, AecError>([]),
                (done, item) => done.Bind(splits => SplitOf(item.Cut, item.WritesLength)
                    .Bind(split => WithWeldSides(split, graph))
                    .Map(split =>
                    {
                        splits.Add(split);
                        return splits;
                    })
                    .TapError(_ => splits.ForEach(Dispose))))
            .Map(splits => (IReadOnlyList<HostSplit>)splits);

    private static Result<HostSplit, AecError> SplitOf(HostCut cut, bool writesLength)
    {
        Polyline pipe = cut.Pipe;
        List<double> parameters = [];
        if (cut.KeepsStart)
        {
            parameters.Add(cut.From.Parameter);
        }

        if (cut.KeepsEnd)
        {
            parameters.Add(cut.To.Parameter);
        }

        if (parameters.Count == 0)
        {
            return Result.Success<HostSplit, AecError>(new HostSplit(
                cut, Maybe<Polyline>.None, Maybe<Polyline>.None, writesLength, new Dictionary<string, EndType>()));
        }

        return AutoCadCall
            .Run(
                $"del polylinje {pipe.Handle}",
                () => pipe.GetSplitCurves(new DoubleCollection(parameters.ToArray())).Cast<DBObject>().ToList())
            .Bind(curves => KeptCurves(cut, curves, parameters.Count + 1, writesLength));
    }

    /// <summary>Picks the first and last curve as the kept pieces and disposes every other curve.</summary>
    private static Result<HostSplit, AecError> KeptCurves(
        HostCut cut, List<DBObject> curves, int expected, bool writesLength)
    {
        List<Polyline> polylines = curves.OfType<Polyline>().ToList();
        if (curves.Count != expected || polylines.Count != expected)
        {
            curves.ForEach(curve => curve.Dispose());
            return Result.Failure<HostSplit, AecError>(AecError.Drawing(
                $"GetSplitCurves gav {curves.Count} dele af polylinje {cut.Pipe.Handle}, forventede {expected}"));
        }

        Maybe<Polyline> start = cut.KeepsStart ? Maybe.From(polylines[0]) : Maybe<Polyline>.None;
        Maybe<Polyline> end = cut.KeepsEnd ? Maybe.From(polylines[^1]) : Maybe<Polyline>.None;
        polylines
            .Where((_, index) => !(cut.KeepsStart && index == 0) && !(cut.KeepsEnd && index == expected - 1))
            .ToList()
            .ForEach(polyline => polyline.Dispose());

        return Result.Success<HostSplit, AecError>(
            new HostSplit(cut, start, end, writesLength, new Dictionary<string, EndType>()));
    }

    /// <summary>Decides, against the curves in memory, which piece each welded-on component moves to.</summary>
    private static Result<HostSplit, AecError> WithWeldSides(HostSplit split, GraphLinks graph)
    {
        IReadOnlyList<(EndType Held, Polyline Curve)> curves = new[]
            {
                split.StartCurve.Map(curve => (Held: EndType.Start, Curve: curve)),
                split.EndCurve.Map(curve => (Held: EndType.End, Curve: curve)),
            }
            .Choose()
            .ToList();

        return graph.WeldSides(split.Cut.Pipe, curves)
            .Map(sides => split with { WeldSides = sides })
            .TapError(_ => Dispose(split));
    }

    private static void Dispose(HostSplit split)
    {
        split.StartCurve.Tap(curve => curve.Dispose());
        split.EndCurve.Tap(curve => curve.Dispose());
    }

    /// <summary>
    /// The write phase: geometry, then FJV_fremtid Length, then the graph links. Everything the drawing's data
    /// decides was decided before; a failure here is a defect.
    /// </summary>
    private static string Apply(
        Transaction tx,
        BlockReference block,
        IReadOnlyList<HostSplit> splits,
        IReadOnlyList<TrimmedPiece> touches,
        GraphLinks graph)
    {
        List<TrimmedPipe> pipes = splits.Select(split => Reshape(tx, split)).ToList();

        foreach (TrimmedPiece piece in splits
            .Zip(pipes)
            .Where(pair => pair.First.WritesLength)
            .SelectMany(pair => pair.Second.Pieces))
        {
            PropertySetFields.WriteWhereCarried(piece.Polyline, tx, FremtidSet, LengthProperty, piece.Polyline.Length);
        }

        graph.Rewrite(pipes, touches, block);

        return string.Join(", ", pipes
            .Select(Describe)
            .Concat(touches.Select(touch => $"{touch.Polyline.Handle} tilsluttet").Distinct()));
    }

    /// <summary>
    /// The original polyline becomes the piece before the stretch (or after it, when nothing remains before) and
    /// keeps its handle; a piece after the stretch beside it is one new polyline; nothing left erases the original.
    /// </summary>
    private static TrimmedPipe Reshape(Transaction tx, HostSplit split)
    {
        Polyline pipe = split.Cut.Pipe;
        ObjectId id = pipe.ObjectId;
        string handle = pipe.Handle.ToString();
        Maybe<double> width = ConstantWidth(pipe);
        pipe.CheckOrOpenForWrite();

        return split.StartCurve.Match(
            startCurve =>
            {
                CopyVertices(startCurve, pipe, width);
                Maybe<TrimmedPiece> end = split.EndCurve.Map(endCurve =>
                    new TrimmedPiece(AddPiece(tx, pipe, endCurve, width), EndType.Start, split.Cut.To.Port));
                return new TrimmedPipe(
                    id,
                    handle,
                    Maybe.From(new TrimmedPiece(pipe, EndType.End, split.Cut.From.Port)),
                    end,
                    split.WeldSides);
            },
            () => split.EndCurve.Match(
                endCurve =>
                {
                    CopyVertices(endCurve, pipe, width);
                    return new TrimmedPipe(
                        id,
                        handle,
                        Maybe<TrimmedPiece>.None,
                        Maybe.From(new TrimmedPiece(pipe, EndType.Start, split.Cut.To.Port)),
                        split.WeldSides);
                },
                () =>
                {
                    pipe.Erase();
                    return new TrimmedPipe(
                        id, handle, Maybe<TrimmedPiece>.None, Maybe<TrimmedPiece>.None, split.WeldSides);
                }));
    }

    /// <summary>The polyline's global width, when every vertex has it at both ends.</summary>
    private static Maybe<double> ConstantWidth(Polyline pipe)
    {
        double first = pipe.GetStartWidthAt(0);
        bool isConstant = Enumerable.Range(0, pipe.NumberOfVertices)
            .All(index => pipe.GetStartWidthAt(index) == first && pipe.GetEndWidthAt(index) == first);
        return isConstant ? Maybe.From(first) : Maybe<double>.None;
    }

    /// <summary>Gives <paramref name="target"/> the vertices of <paramref name="source"/>, then disposes the source.</summary>
    private static void CopyVertices(Polyline source, Polyline target, Maybe<double> width)
    {
        int count = source.NumberOfVertices;
        for (int index = 0; index < count; index++)
        {
            Point2d point = source.GetPoint2dAt(index);
            double bulge = source.GetBulgeAt(index);
            double startWidth = source.GetStartWidthAt(index);
            double endWidth = source.GetEndWidthAt(index);

            if (index < target.NumberOfVertices)
            {
                target.SetPointAt(index, point);
                target.SetBulgeAt(index, bulge);
                target.SetStartWidthAt(index, startWidth);
                target.SetEndWidthAt(index, endWidth);
            }
            else
            {
                target.AddVertexAt(index, point, bulge, startWidth, endWidth);
            }
        }

        while (target.NumberOfVertices > count)
        {
            target.RemoveVertexAt(target.NumberOfVertices - 1);
        }

        width.Tap(value => target.ConstantWidth = value);
        source.Dispose();
    }

    /// <summary>
    /// Adds the split curve beside the original: the original's entity properties, linetype generation, width
    /// and property sets, and its place in the draw order, just above the original.
    /// </summary>
    private static Polyline AddPiece(Transaction tx, Polyline pipe, Polyline piece, Maybe<double> width)
    {
        piece.SetPropertiesFrom(pipe);
        piece.Plinegen = pipe.Plinegen;
        width.Tap(value => piece.ConstantWidth = value);

        BlockTableRecord owner = (BlockTableRecord)tx.GetObject(pipe.OwnerId, OpenMode.ForWrite);
        owner.AppendEntity(piece);
        tx.AddNewlyCreatedDBObject(piece, true);

        DrawOrderTable drawOrder = (DrawOrderTable)tx.GetObject(owner.DrawOrderTableId, OpenMode.ForWrite);
        ObjectIdCollection moved = new();
        moved.Add(piece.ObjectId);
        drawOrder.MoveAbove(moved, pipe.ObjectId);

        PropertySetFields.CopySets(pipe, piece, tx);
        return piece;
    }

    private static string Describe(TrimmedPipe pipe) =>
        pipe.StartPiece.Match(
            start => pipe.EndPiece.Match(
                end => $"{pipe.OriginalHandle} delt i {start.Polyline.Handle} og {end.Polyline.Handle}",
                () => $"{pipe.OriginalHandle} afkortet"),
            () => pipe.EndPiece.Match(
                _ => $"{pipe.OriginalHandle} afkortet",
                () => $"{pipe.OriginalHandle} slettet"));
}
