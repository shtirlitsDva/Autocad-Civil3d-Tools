using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using IntersectUtilities.UtilsCommon.Enums;

namespace IntersectUtilities.MPE.PipePlan;

/// <summary>
/// PPEDIT on a bonded pair. The handles are the centreline's control polygon; every
/// candidate re-solves the centreline at R_inner + c/2 and offsets frem/retur from it.
/// Picking any member opens the run and highlights all three; a commit rewrites all
/// three in one transaction (handles kept, missing members rebuilt).
/// </summary>
internal sealed class PipePlanPairEditSession : PipePlanEditSessionBase
{
    private readonly PipePlanPairSpacing _spacing;
    private PipePlanPairStoredData _data;
    private PipePlanPairSlots _slots;

    private PipePlanPairEditSession(
        Document document,
        PipePlanState state,
        PipePlanPairRun run,
        PipePlanPairSpacing spacing)
        : base(document, state)
    {
        _data = run.Data;
        _slots = run.Slots;
        _spacing = spacing;
        Notes = run.Notes;
    }

    /// <summary>What the user should hear when the session opens: repairs, adopted
    /// transforms and a changed spacing (applied on the first commit).</summary>
    public IReadOnlyList<string> Notes { get; }

    public override string SizeLabel => _data.SizeDisplay;

    public override string RadiusDisplay => _data.Authoring.RadiusDisplay;

    public override IReadOnlyList<Point3d> ControlPoints => _data.Authoring.ControlPoints;

    public override IReadOnlyList<double> CurrentBendRadii => _data.Authoring.InnerRadii;

    public override string RadiusQualifier => " (inderrør)";

    public override bool CanFlip => true;

    public static Result<PipePlanPairEditSession> Open(Document document, PipePlanState state, ObjectId pickedId)
    {
        using Transaction tx = document.Database.TransactionManager.StartTransaction();
        Result<PipePlanPairEditSession> session = PipePlanPairRunLocator.Locate(document.Database, tx, pickedId).Map(run =>
        {
            PipePlanPairSpacing spacing = CurrentSpacing(document.Database, tx, run);
            List<string> notes = [.. run.Notes];
            if (!spacing.SameGeometryAs(run.Data.Spacing))
            {
                notes.Add($"Afstand {run.Data.Spacing.CentreToCentreMm:0} → {spacing.CentreToCentreMm:0} mm (anvendes ved næste ændring).");
            }

            return new PipePlanPairEditSession(document, state, run with { Notes = notes }, spacing);
        });
        tx.Commit();

        session.Switch(
            opened =>
            {
                state.ApplyPairContext(opened._data, opened._spacing, flipLocked: false);
                opened.Highlight(true);
            },
            _ => { });
        return session;
    }

    /// <summary>Q13: the spacing a re-solve uses — current jacket width and min x.</summary>
    public static PipePlanPairSpacing CurrentSpacing(Database db, Transaction tx, PipePlanPairRun run)
    {
        List<Polyline> pipes = new[] { PipePlanPairRole.Frem, PipePlanPairRole.Retur }
            .Select(role => run.Slots.For(role).Match(
                id => new[] { (Polyline)tx.GetObject(id, OpenMode.ForRead) },
                () => Array.Empty<Polyline>()))
            .SelectMany(p => p)
            .ToList();
        return PipePlanPairSpacingResolver.ForExistingRun(db, run.Data, pipes);
    }

    public override void ShowHandles()
    {
        State.ClearPreview();

        IReadOnlyList<PipePlanSegmentDivider> dividers = [];
        IReadOnlyList<PipePlanArcDimension> arcDimensions = [];
        PipePlanPairGeometry.SolveWith(Solver, _data.Authoring, _spacing).Switch(
            solution =>
            {
                dividers = ComputeSegmentDividers(solution.Centre.Vertices);
                arcDimensions = InnerPipeArcDimensions(solution);
            },
            _ => { });

        // Divider ticks span the whole pair.
        MarkerManager.Show(Document, ControlPoints, dividers, arcDimensions, _spacing.CentreToCentre + _spacing.PipeWidth);
    }

    public override void Commit(PipePlanEditCandidate candidate)
    {
        PipePlanPairAuthoring authoring = new(candidate.Draft.ControlPoints, candidate.Draft.BendRadii, _data.Authoring.Flip);
        PipePlanPairGeometry.Solve(candidate.Analysis, _spacing.Half, authoring.Flip).Switch(
            solution => Write(_data with { Authoring = authoring, Spacing = _spacing }, _slots, solution),
            message => State.SetStatus(message, PipePlanStatusKind.Error));
        ClearPendingRadius();
    }

    /// <summary>Swaps which pipe is frem. The polylines stay where they are; their
    /// layers and roles swap.</summary>
    public override Result<string> FlipSides()
    {
        PipePlanPairAuthoring flipped = _data.Authoring with { Flip = !_data.Authoring.Flip };
        PipePlanPairStoredData data = _data with { Authoring = flipped, Spacing = _spacing };
        return PipePlanPairGeometry.SolveWith(Solver, flipped, _spacing).Map(solution =>
        {
            Write(data, _slots.SwapSides(), solution);
            State.ApplyPairContext(_data, _spacing, flipLocked: false);
            return flipped.Flip ? "Frem ligger nu til højre." : "Frem ligger nu til venstre.";
        });
    }

    public override bool TryGetInsertRadius(out double radius, out string error)
    {
        error = string.Empty;
        if (PipePlanRadiusStore.TryGet(Document.Database, _data.System, PipeTypeEnum.Enkelt, _data.Dn, out radius) && radius > 0.0)
        {
            return true;
        }

        radius = CurrentBendRadii.Where(r => r > 0.0).DefaultIfEmpty(0.0).First();
        if (radius > 0.0)
        {
            return true;
        }

        error = $"Ingen standard-radius for {_data.SizeDisplay}. Sæt den i PPSETTINGS.";
        return false;
    }

    public override void Dispose()
    {
        base.Dispose();
        Highlight(false);
        State.LeavePairShape();
    }

    protected override PipePlanAnalysis Analyze(PipePlanEditDraft draft) =>
        PipePlanPairGeometry.Analyze(Solver, draft.ControlPoints, draft.BendRadii, _spacing.Half);

    private void Write(PipePlanPairStoredData data, PipePlanPairSlots slots, PipePlanPairSolution solution)
    {
        Highlight(false);
        using (Document.LockDocument())
        using (Transaction tx = Document.Database.TransactionManager.StartTransaction())
        {
            _slots = PipePlanPairWriter.Write(Document.Database, tx, slots, solution, data);
            tx.Commit();
        }

        _data = data;
        Highlight(true);
    }

    /// <summary>The inner pipe of each bend carries the radius the drafter set, so the
    /// arc dimensions are taken on whichever offset is on the inside.</summary>
    private IReadOnlyList<PipePlanArcDimension> InnerPipeArcDimensions(PipePlanPairSolution solution)
    {
        IReadOnlyList<PipePlanArcDimension> frem = ComputeArcDimensions(solution.Frem);
        IReadOnlyList<PipePlanArcDimension> retur = ComputeArcDimensions(solution.Retur);
        return frem.Zip(retur, (f, r) => f.Radius <= r.Radius ? f : r).ToList();
    }

    private void Highlight(bool on)
    {
        using Transaction tx = Document.Database.TransactionManager.StartTransaction();
        foreach (ObjectId id in _slots.Present())
        {
            if (id.IsErased)
            {
                continue;
            }

            Entity entity = (Entity)tx.GetObject(id, OpenMode.ForRead);
            if (on)
            {
                entity.Highlight();
            }
            else
            {
                entity.Unhighlight();
            }
        }

        tx.Commit();
    }
}
