using System.Globalization;

using Autodesk.AutoCAD.DatabaseServices;

using CSharpFunctionalExtensions;

using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.Enums;
using IntersectUtilities.UtilsCommon.Graphs;

using Norsyn.AecPropertySets;

using AecDataType = Autodesk.Aec.PropertyData.DataType;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;

namespace IntersectUtilities.MPE.PlaceLongitudinalBlock;

/// <summary>One DriGraph ConnectedEntities entry: "own end type:other end type:other handle;".</summary>
internal sealed record GraphEntry(int Own, int Other, string Handle)
{
    public override string ToString() => $"{Own}:{Other}:{Handle};";
}

/// <summary>
/// FV_Fremtid's DriGraph links (ConnectedEntities) during one PLACELONGITUDINALBLOCK run: read at the run's first
/// trim, kept current after every rewrite, and written only where an entity's set of entries changes.
/// </summary>
internal sealed class GraphLinks
{
    private const int StartCode = (int)EndType.Start;
    private const int EndCode = (int)EndType.End;
    private const int WeldOnCode = (int)EndType.WeldOn;

    // How far a welded-on component's port may lie from the piece it moves to.
    private const double WeldTolerance = 0.01;

    private static readonly string SetName = PSetDefs.DefinedSets.DriGraph.ToString();
    private static readonly PSetDefs.DriGraph Definition = new();

    private readonly Database _db;
    private readonly Transaction _tx;

    // Each FJV entity's entries (entities with none are left out), and every live FJV entity by handle.
    private readonly Dictionary<ObjectId, List<GraphEntry>> _entries = [];
    private readonly Dictionary<string, ObjectId> _ids = new(StringComparer.OrdinalIgnoreCase);
    private bool _isLoaded;

    internal GraphLinks(Database db, Transaction tx)
    {
        _db = db;
        _tx = tx;
    }

    /// <summary>Reads every FJV entity's entries on the first call; later calls read nothing.</summary>
    internal UnitResult<AecError> Load()
    {
        if (_isLoaded)
        {
            return UnitResult.Success<AecError>();
        }

        return _db.GetFjvEntities(_tx, true, false)
            .Traverse(entity => PropertySetFields
                .ReadText(entity, _tx, SetName, Definition.ConnectedEntities.Name)
                .Map((Maybe<string> text) => (Entity: entity, Entries: Parse(text.GetValueOrDefault(string.Empty)))))
            .Tap(read =>
            {
                foreach ((Entity entity, List<GraphEntry> entries) in read)
                {
                    _ids[entity.Handle.ToString()] = entity.ObjectId;
                    if (entries.Count > 0)
                    {
                        _entries[entity.ObjectId] = entries;
                    }
                }

                _isLoaded = true;
            })
            .Bind(_ => UnitResult.Success<AecError>());
    }

    /// <summary>
    /// Refuses, before anything is written, a rewrite that could not be written: a DriGraph definition that cannot
    /// take the entries, or an entity the rewrite may write on a locked layer. Those entities are the cut and the
    /// touched polylines and every entity whose entries name a cut polyline; the new pieces and the block are new.
    /// Writing an entry opens the entity's DriGraph set for write, and attaching a missing one the entity itself.
    /// </summary>
    internal UnitResult<AecError> CheckWritable(IReadOnlyList<Polyline> cutPipes, IReadOnlyList<Polyline> touched)
    {
        EnsureLoaded();
        HashSet<string> cutHandles = cutPipes
            .Select(pipe => pipe.Handle.ToString())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        List<ObjectId> written = cutPipes.Concat(touched)
            .Select(polyline => polyline.ObjectId)
            .Concat(_entries
                .Where(x => x.Value.Any(entry => cutHandles.Contains(entry.Handle)))
                .Select(x => x.Key))
            .Distinct()
            .ToList();

        return PropertySetFields
            .CheckDefinition(
                _db,
                _tx,
                SetName,
                Definition.ConnectedEntities.Name,
                AecDataType.Text,
                Definition.AppliesTo.Cast<string>())
            .Bind(() => written
                .Select(id => (Entity)_tx.GetObject(id, OpenMode.ForRead))
                .Select(entity => PipeTrimmer.LockedLayer(entity, _tx)
                    .Map(layer => $"{entity.Handle} ligger på det låste lag {layer}"))
                .Choose()
                .TryFirst()
                .Match(
                    reason => UnitResult.Failure(AecError.Drawing(
                        $"grafforbindelserne kan ikke opdateres: {reason}")),
                    () => UnitResult.Success<AecError>()));
    }

    /// <summary>
    /// For each component welded on the pipe (a WeldOn entry either way), the pipe end whose curve one of its ports
    /// lies on, at most WeldTolerance away; a component on neither curve is left out, and its links are dropped.
    /// </summary>
    internal Result<IReadOnlyDictionary<string, EndType>, AecError> WeldSides(
        Polyline pipe, IReadOnlyList<(EndType Held, Polyline Curve)> curves)
    {
        EnsureLoaded();
        string handle = pipe.Handle.ToString();
        List<string> components = _entries.GetValueOrDefault(pipe.ObjectId, [])
            .Where(entry => entry.Own == WeldOnCode)
            .Select(entry => entry.Handle)
            .Concat(_entries
                .Where(x => x.Value.Any(entry =>
                    entry.Other == WeldOnCode && string.Equals(entry.Handle, handle, StringComparison.OrdinalIgnoreCase)))
                .Select(x => x.Key.Handle.ToString()))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return components
            .Traverse(component => HeldEndNear(component, curves)
                .Map((Maybe<EndType> held) => (Component: component, Held: held)))
            .Map(found => (IReadOnlyDictionary<string, EndType>)found
                .Select(x => x.Held.Map(held => (x.Component, Held: held)))
                .Choose()
                .ToDictionary(x => x.Component, x => x.Held, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The end whose curve one of the component's ports lies on; None for a component that is not a block in the
    /// index or lies on neither curve. Only the closest-point calls are AutoCAD calls that can fail on the data.
    /// </summary>
    private Result<Maybe<EndType>, AecError> HeldEndNear(
        string componentHandle, IReadOnlyList<(EndType Held, Polyline Curve)> curves)
    {
        List<ComponentPort> ports = _ids.TryFind(componentHandle)
            .Bind(id => _tx.GetObject(id, OpenMode.ForRead) is BlockReference component
                ? Maybe.From(component)
                : Maybe<BlockReference>.None)
            .Match(component => ComponentPorts.Read(component, _tx), () => new List<ComponentPort>());

        return AutoCadCall
            .Run($"nærmeste punkt for komponent {componentHandle}", () => ports
                .SelectMany(port => curves.Select(curve => (
                    curve.Held,
                    Distance: curve.Curve
                        .GetClosestPointTo(PipeTrimmer.InPlaneOf(curve.Curve, port.Position), false)
                        .DistanceHorizontalTo(port.Position))))
                .ToList())
            .Map(distances => distances
                .Where(x => x.Distance <= WeldTolerance)
                .OrderBy(x => x.Distance)
                .Select(x => x.Held)
                .TryFirst());
    }

    /// <summary>
    /// After one trim: links the block to each cut or touched end, hands each kept end the pipe's entries for that
    /// end, re-points every other entry that named a trimmed pipe, and writes each entity whose entries changed.
    /// </summary>
    internal void Rewrite(IReadOnlyList<TrimmedPipe> pipes, IReadOnlyList<TrimmedPiece> touches, BlockReference block)
    {
        EnsureLoaded();
        Dictionary<string, TrimmedPipe> byHandle =
            pipes.ToDictionary(pipe => pipe.OriginalHandle, StringComparer.OrdinalIgnoreCase);
        List<(Entity Entity, List<GraphEntry> Entries, bool IsNew)> next = [];

        foreach (TrimmedPipe pipe in pipes)
        {
            List<GraphEntry> before = _entries.GetValueOrDefault(pipe.OriginalId, []);
            foreach (TrimmedPiece piece in pipe.Pieces)
            {
                List<GraphEntry> entries = Repoint(Carried(before, pipe, piece), piece.Polyline.ObjectId, byHandle)
                    .Append(CutLink(piece, block))
                    .Distinct()
                    .ToList();
                next.Add((piece.Polyline, entries, piece.Polyline.ObjectId != pipe.OriginalId));
            }
        }

        foreach (IGrouping<ObjectId, TrimmedPiece> touched in touches.GroupBy(touch => touch.Polyline.ObjectId))
        {
            List<GraphEntry> entries = Repoint(_entries.GetValueOrDefault(touched.Key, []), touched.Key, byHandle)
                .Concat(touched.Select(touch => CutLink(touch, block)))
                .Distinct()
                .ToList();
            next.Add((touched.First().Polyline, entries, false));
        }

        List<GraphEntry> blockEntries = Repoint(_entries.GetValueOrDefault(block.ObjectId, []), block.ObjectId, byHandle)
            .Concat(pipes.SelectMany(pipe => pipe.Pieces).Concat(touches).Select(BlockLink))
            .Distinct()
            .ToList();
        next.Add((block, blockEntries, false));

        HashSet<ObjectId> handled = next.Select(x => x.Entity.ObjectId)
            .Concat(pipes.Select(pipe => pipe.OriginalId))
            .ToHashSet();
        foreach ((ObjectId id, List<GraphEntry> entries) in _entries
            .Where(x => !handled.Contains(x.Key) && x.Value.Any(entry => byHandle.ContainsKey(entry.Handle)))
            .ToList())
        {
            next.Add(((Entity)_tx.GetObject(id, OpenMode.ForRead), Repoint(entries, id, byHandle).Distinct().ToList(), false));
        }

        Write(next
            .Where(x => x.IsNew || !x.Entries.ToHashSet().SetEquals(_entries.GetValueOrDefault(x.Entity.ObjectId, [])))
            .ToList());

        foreach (TrimmedPipe erased in pipes.Where(pipe => pipe.Survivor.HasNoValue))
        {
            _entries.Remove(erased.OriginalId);
            _ids.Remove(erased.OriginalHandle);
        }
    }

    private static List<GraphEntry> Parse(string text) =>
        Con.ConRegex.Matches(text)
            .Select(match => new GraphEntry(
                int.Parse(match.Groups["OwnEndType"].Value, CultureInfo.InvariantCulture),
                int.Parse(match.Groups["ConEndType"].Value, CultureInfo.InvariantCulture),
                match.Groups["Handle"].Value.ToUpperInvariant()))
            .Distinct()
            .ToList();

    /// <summary>
    /// The pipe's own entries a piece keeps: those of the end it holds, the welded-on components decided to lie on
    /// it, and, on the original polyline, entries of no pipe end type.
    /// </summary>
    private static IEnumerable<GraphEntry> Carried(IEnumerable<GraphEntry> before, TrimmedPipe pipe, TrimmedPiece piece)
    {
        EndType held = piece.CutEnd == EndType.End ? EndType.Start : EndType.End;
        bool isSurvivor = piece.Polyline.ObjectId == pipe.OriginalId;

        return before.Where(entry =>
            entry.Own == (int)held ||
            (entry.Own == WeldOnCode && pipe.WeldSides.TryFind(entry.Handle).Match(side => side == held, () => false)) ||
            (entry.Own != StartCode && entry.Own != EndCode && entry.Own != WeldOnCode && isSurvivor));
    }

    /// <summary>Entries naming a trimmed pipe now name the piece holding that end, or are dropped with it.</summary>
    private static IEnumerable<GraphEntry> Repoint(
        IEnumerable<GraphEntry> entries, ObjectId owner, IReadOnlyDictionary<string, TrimmedPipe> byHandle) =>
        entries.SelectMany(entry => byHandle.TryFind(entry.Handle).Match(
            pipe => Target(entry, owner, pipe).Match(
                piece => new[] { entry with { Handle = piece.Polyline.Handle.ToString() } },
                () => Array.Empty<GraphEntry>()),
            () => new[] { entry }));

    private static Maybe<TrimmedPiece> Target(GraphEntry entry, ObjectId owner, TrimmedPipe pipe) => entry.Other switch
    {
        StartCode => pipe.StartPiece,
        EndCode => pipe.EndPiece,
        WeldOnCode => pipe.WeldSides.TryFind(owner.Handle.ToString()).Bind(held => pipe.PieceHolding(held)),

        // Populate links a pipe only at its start, end or a welded-on port; anything else stays while the
        // original polyline does.
        _ => pipe.Survivor,
    };

    private static GraphEntry CutLink(TrimmedPiece piece, BlockReference block) =>
        new((int)piece.CutEnd, PortCode(piece.Port), block.Handle.ToString());

    private static GraphEntry BlockLink(TrimmedPiece piece) =>
        new(PortCode(piece.Port), (int)piece.CutEnd, piece.Polyline.Handle.ToString());

    private static int PortCode(ComponentPort port) =>
        port.Role == ComponentPortRole.Branch ? (int)EndType.Branch : (int)EndType.Main;

    /// <summary>
    /// Writes each changed entity's entries and keeps the cache current. PropertySetManager attaches DriGraph where
    /// it is missing and creates the definition only in a drawing that has none.
    /// </summary>
    private void Write(IReadOnlyList<(Entity Entity, List<GraphEntry> Entries, bool IsNew)> changes)
    {
        if (changes.Count == 0)
        {
            return;
        }

        PropertySetManager graphSets = new(_db, PSetDefs.DefinedSets.DriGraph);
        foreach ((Entity entity, List<GraphEntry> entries, bool _) in changes)
        {
            graphSets.WritePropertyString(entity, Definition.ConnectedEntities, string.Concat(entries));
            _ids[entity.Handle.ToString()] = entity.ObjectId;
            if (entries.Count > 0)
            {
                _entries[entity.ObjectId] = entries;
            }
            else
            {
                _entries.Remove(entity.ObjectId);
            }
        }
    }

    private void EnsureLoaded()
    {
        if (!_isLoaded)
        {
            throw new InvalidOperationException("GraphLinks was used before Load succeeded.");
        }
    }
}
