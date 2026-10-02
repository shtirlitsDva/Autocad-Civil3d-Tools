using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

namespace IntersectUtilities.MPE.PipePlan;

/// <summary>The polyline that fills each role of a run, when there is one.</summary>
internal sealed record PipePlanPairSlots(Option<ObjectId> Centre, Option<ObjectId> Frem, Option<ObjectId> Retur)
{
    public static readonly PipePlanPairSlots Empty = new(
        Option<ObjectId>.Nothing, Option<ObjectId>.Nothing, Option<ObjectId>.Nothing);

    public static readonly IReadOnlyList<PipePlanPairRole> Roles =
        [PipePlanPairRole.Centerline, PipePlanPairRole.Frem, PipePlanPairRole.Retur];

    public Option<ObjectId> For(PipePlanPairRole role) => role switch
    {
        PipePlanPairRole.Centerline => Centre,
        PipePlanPairRole.Frem => Frem,
        PipePlanPairRole.Retur => Retur,
    };

    public PipePlanPairSlots With(PipePlanPairRole role, Option<ObjectId> id) => role switch
    {
        PipePlanPairRole.Centerline => this with { Centre = id },
        PipePlanPairRole.Frem => this with { Frem = id },
        PipePlanPairRole.Retur => this with { Retur = id },
    };

    /// <summary>Flip on an existing pair swaps which polyline is frem: geometry stays put.</summary>
    public PipePlanPairSlots SwapSides() => this with { Frem = Retur, Retur = Frem };

    public IReadOnlyList<ObjectId> Present() =>
        Roles.SelectMany(role => For(role).Match(id => new[] { id }, () => Array.Empty<ObjectId>())).ToList();

    public int Count => Present().Count;
}

/// <summary>
/// A located run, ready to edit: its (possibly re-tokened or transformed) data, the
/// polylines that fill its roles, and what the user should be told about repairs.
/// </summary>
internal sealed record PipePlanPairRun(
    PipePlanPairStoredData Data,
    PipePlanPairSlots Slots,
    IReadOnlyList<string> Notes);

/// <summary>What a picked polyline is, from PipePlan's point of view.</summary>
internal abstract record PipePlanPick
{
    private PipePlanPick() { }

    public const string ConvertParkedMessage =
        "Enkeltrør uden PipePlan-data kan ikke konverteres endnu. Tegn parret med PPDRAW.";

    public abstract T Match<T>(Func<ObjectId, T> pairMember, Func<string, T> unmanagedBonded, Func<T> other);

    /// <summary>Carries pair metadata (valid or not — the locator decides).</summary>
    internal sealed record PairMember(ObjectId Id) : PipePlanPick
    {
        public override T Match<T>(Func<ObjectId, T> pairMember, Func<string, T> unmanagedBonded, Func<T> other) => pairMember(Id);
    }

    /// <summary>A steel FREM/RETUR polyline with no pair metadata: needs PPCONVERT (parked).</summary>
    internal sealed record UnmanagedBonded(string Layer) : PipePlanPick
    {
        public override T Match<T>(Func<ObjectId, T> pairMember, Func<string, T> unmanagedBonded, Func<T> other) => unmanagedBonded(Layer);
    }

    /// <summary>Anything else: the single-pipe PipePlan flow handles it.</summary>
    internal sealed record Other : PipePlanPick
    {
        public override T Match<T>(Func<ObjectId, T> pairMember, Func<string, T> unmanagedBonded, Func<T> other) => other();
    }

    public static PipePlanPick Classify(Database db, ObjectId id)
    {
        using Transaction tx = db.TransactionManager.StartTransaction();
        PipePlanPick pick = tx.GetObject(id, OpenMode.ForRead) switch
        {
            Polyline polyline when PipePlanPairMetadata.HasPairData(polyline, tx) => new PairMember(id),
            Polyline polyline when PipePlanPairSpacingResolver.IsBondedPair(
                PipeScheduleV2.PipeScheduleV2.GetPipeSystem(polyline.Layer),
                PipeScheduleV2.PipeScheduleV2.GetPipeType(polyline.Layer)) => new UnmanagedBonded(polyline.Layer),
            _ => new Other(),
        };
        tx.Commit();
        return pick;
    }
}

/// <summary>
/// Finds the run a picked pair member belongs to and decides membership
/// (ppdraw-bonded-pairs.md, entities):
/// - a member is a polyline with the run's token whose geometry matches the stored data;
/// - when all three match under ONE shared rigid transform (move/rotate/mirror), the
///   transform is adopted (a mirror inverts Flip; a copy is re-tokened);
/// - a missing or deformed member is rebuilt from the valid ones;
/// - fewer than three members under a non-identity transform are not a run.
/// Only polylines carrying the SAME authoring data as the reference member can join it, so
/// a stale copy left over from before an edit never stands in for a real member.
/// </summary>
internal static class PipePlanPairRunLocator
{
    private const double PointTolerance = 1e-4;
    private const double BulgeTolerance = 1e-6;

    public static Result<PipePlanPairRun> Locate(Database db, Transaction tx, ObjectId pickedId)
    {
        Polyline picked = (Polyline)tx.GetObject(pickedId, OpenMode.ForRead);
        return PipePlanPairMetadata.Read(picked, tx).Match(
            read => read.Bind(pickedData => LocateFrom(db, tx, picked, pickedData)),
            () => Result<PipePlanPairRun>.Failure("Polylinjen er ikke en del af et PipePlan-par."));
    }

    private static Result<PipePlanPairRun> LocateFrom(Database db, Transaction tx, Polyline picked, PipePlanPairStoredData pickedData)
    {
        PipePlanSolver solver = new();
        List<Candidate> candidates = FindCandidates(db, tx, pickedData.RunToken, solver);
        Candidate[] selves = candidates.Where(c => c.Id == picked.ObjectId).ToArray();
        if (selves.Length == 0)
        {
            return Result<PipePlanPairRun>.Failure("Polylinjen ligger ikke i modelrummet.");
        }

        Candidate self = selves[0];
        List<Candidate> family = candidates.Where(c => SameRun(c.Data, self.Data)).ToList();

        if (self.Matches(Rigid2d.Identity))
        {
            return Result<PipePlanPairRun>.Success(IdentityRun(family, self.Data, picked.Elevation));
        }

        List<(PipePlanPairSlots Slots, Rigid2d Transform)> moved = self.RigidMatches
            .Select(t => (Slots: Group(family, t), Transform: t))
            .OrderByDescending(g => g.Slots.Count)
            .ToList();

        if (moved.Count > 0)
        {
            (PipePlanPairSlots slots, Rigid2d transform) = moved[0];
            if (slots.Count < PipePlanPairSlots.Roles.Count)
            {
                return Result<PipePlanPairRun>.Failure(
                    $"Polylinjen ({picked.Handle}) er flyttet eller kopieret uden resten af parret og behandles som en almindelig polylinje.");
            }

            return Result<PipePlanPairRun>.Success(AdoptTransform(candidates, slots, self.Data, transform, picked.Elevation));
        }

        // The picked member is deformed (edited outside PipePlan): rebuild it from the
        // members that still match where the run was baked.
        if (Group(family, Rigid2d.Identity).Count == 0)
        {
            string handles = string.Join(", ", family.Select(c => c.Handle));
            return Result<PipePlanPairRun>.Failure(
                $"Ingen gyldige medlemmer af parret ({handles}). {PipePlanPick.ConvertParkedMessage}");
        }

        return Result<PipePlanPairRun>.Success(IdentityRun(family, self.Data, picked.Elevation));
    }

    private static PipePlanPairRun IdentityRun(List<Candidate> family, PipePlanPairStoredData data, double elevation)
    {
        List<string> notes = [];
        PipePlanPairSlots slots = RepairMissing(family, Group(family, Rigid2d.Identity), notes);
        return new PipePlanPairRun(WithElevation(data, elevation), slots, notes);
    }

    private static PipePlanPairRun AdoptTransform(
        List<Candidate> candidates, PipePlanPairSlots slots, PipePlanPairStoredData data, Rigid2d transform, double elevation)
    {
        List<string> notes = [transform.Mirror ? "Parret er spejlet — PipePlan følger med." : "Parret er flyttet — PipePlan følger med."];
        IReadOnlyList<ObjectId> members = slots.Present();
        string token = data.RunToken;
        if (candidates.Any(c => !members.Contains(c.Id)))
        {
            // Another polyline still carries this token: the run in hand becomes its own run.
            token = Guid.NewGuid().ToString("N");
            notes.Add("Kopien har fået sin egen identitet.");
        }

        List<Point3d> points = data.Authoring.ControlPoints
            .Select(p => transform.Apply(new Point2d(p.X, p.Y)))
            .Select(p => new Point3d(p.X, p.Y, elevation))
            .ToList();
        PipePlanPairAuthoring authoring = data.Authoring with
        {
            ControlPoints = points,
            Flip = data.Authoring.Flip ^ transform.Mirror,
        };

        return new PipePlanPairRun(data with { RunToken = token, Authoring = authoring }, slots, notes);
    }

    /// <summary>
    /// Fills an empty role with the one polyline of that role that matches no rigid
    /// transform at all (a member deformed outside PipePlan): it keeps its handle and is
    /// re-drawn. A role with no such polyline stays empty and gets a new one on write.
    /// </summary>
    private static PipePlanPairSlots RepairMissing(List<Candidate> family, PipePlanPairSlots slots, List<string> notes)
    {
        PipePlanPairSlots result = slots;
        foreach (PipePlanPairRole role in PipePlanPairSlots.Roles)
        {
            result = slots.For(role).Match(
                _ => result,
                () =>
                {
                    Candidate[] deformed = family
                        .Where(c => c.Data.Role == role && !c.Matches(Rigid2d.Identity) && c.RigidMatches.Count == 0)
                        .ToArray();
                    if (deformed.Length == 1)
                    {
                        notes.Add($"{RoleName(role)} ({deformed[0].Handle}) er ændret uden for PipePlan og gentegnes.");
                        return result.With(role, Option<ObjectId>.Of(deformed[0].Id));
                    }

                    notes.Add($"{RoleName(role)} mangler og tegnes igen.");
                    return result;
                });
        }

        return result;
    }

    private static PipePlanPairSlots Group(List<Candidate> family, Rigid2d transform)
    {
        PipePlanPairSlots slots = PipePlanPairSlots.Empty;
        foreach (PipePlanPairRole role in PipePlanPairSlots.Roles)
        {
            Candidate[] matching = family.Where(c => c.Data.Role == role && c.Matches(transform)).Take(1).ToArray();
            if (matching.Length == 1)
            {
                slots = slots.With(role, Option<ObjectId>.Of(matching[0].Id));
            }
        }

        return slots;
    }

    /// <summary>Same run token, same authoring, same baked spacing (record equality would
    /// only compare the list references).</summary>
    private static bool SameRun(PipePlanPairStoredData a, PipePlanPairStoredData b) =>
        a.RunToken == b.RunToken &&
        a.Spacing == b.Spacing &&
        a.Authoring.Flip == b.Authoring.Flip &&
        a.Authoring.InnerRadii.SequenceEqual(b.Authoring.InnerRadii) &&
        a.Authoring.ControlPoints.Count == b.Authoring.ControlPoints.Count &&
        a.Authoring.ControlPoints.Zip(b.Authoring.ControlPoints)
            .All(p => new Point2d(p.First.X, p.First.Y).GetDistanceTo(new Point2d(p.Second.X, p.Second.Y)) <= PointTolerance);

    private static PipePlanPairStoredData WithElevation(PipePlanPairStoredData data, double elevation) =>
        data with
        {
            Authoring = data.Authoring with
            {
                ControlPoints = data.Authoring.ControlPoints.Select(p => new Point3d(p.X, p.Y, elevation)).ToList(),
            },
        };

    public static string RoleName(PipePlanPairRole role) => role switch
    {
        PipePlanPairRole.Centerline => "Centerlinjen",
        PipePlanPairRole.Frem => "Frem-røret",
        PipePlanPairRole.Retur => "Retur-røret",
    };

    private static List<Candidate> FindCandidates(Database db, Transaction tx, string token, PipePlanSolver solver)
    {
        List<Candidate> candidates = [];
        BlockTable blockTable = (BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForRead);
        BlockTableRecord modelSpace = (BlockTableRecord)tx.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead);
        string polylineClass = RXObject.GetClass(typeof(Polyline)).DxfName;

        foreach (ObjectId id in modelSpace)
        {
            if (id.ObjectClass.DxfName != polylineClass)
            {
                continue;
            }

            Polyline polyline = (Polyline)tx.GetObject(id, OpenMode.ForRead);
            PipePlanPairMetadata.Read(polyline, tx).Switch(
                read => read.Switch(
                    data =>
                    {
                        if (data.RunToken == token)
                        {
                            candidates.Add(Candidate.From(polyline, data, solver));
                        }
                    },
                    _ => { }),
                () => { });
        }

        return candidates;
    }

    private sealed class Candidate
    {
        private readonly Option<IReadOnlyList<PolylineVertexData>> _expected;
        private readonly IReadOnlyList<PolylineVertexData> _actual;

        private Candidate(
            ObjectId id,
            string handle,
            PipePlanPairStoredData data,
            Option<IReadOnlyList<PolylineVertexData>> expected,
            IReadOnlyList<PolylineVertexData> actual)
        {
            Id = id;
            Handle = handle;
            Data = data;
            _expected = expected;
            _actual = actual;
            RigidMatches = expected.Match(
                e => DeriveTransforms(e, actual).Where(t => !t.IsIdentity && VerticesMatch(e, actual, t)).ToList(),
                () => new List<Rigid2d>());
        }

        public ObjectId Id { get; }

        public string Handle { get; }

        public PipePlanPairStoredData Data { get; }

        /// <summary>Every non-identity rigid transform (proper or mirrored) that maps the
        /// expected geometry onto the actual one. A straight member matches both.</summary>
        public IReadOnlyList<Rigid2d> RigidMatches { get; }

        public static Candidate From(Polyline polyline, PipePlanPairStoredData data, PipePlanSolver solver)
        {
            List<PolylineVertexData> actual = new(polyline.NumberOfVertices);
            for (int i = 0; i < polyline.NumberOfVertices; i++)
            {
                actual.Add(new PolylineVertexData(polyline.GetPoint2dAt(i), polyline.GetBulgeAt(i)));
            }

            Option<IReadOnlyList<PolylineVertexData>> expected = PipePlanPairGeometry.SolveStored(solver, data).Match(
                solution => Option<IReadOnlyList<PolylineVertexData>>.Of(solution.VerticesFor(data.Role)),
                _ => Option<IReadOnlyList<PolylineVertexData>>.Nothing);

            return new Candidate(polyline.ObjectId, polyline.Handle.ToString(), data, expected, actual);
        }

        public bool Matches(Rigid2d transform) =>
            _expected.Match(expected => VerticesMatch(expected, _actual, transform), () => false);
    }

    private static bool VerticesMatch(IReadOnlyList<PolylineVertexData> expected, IReadOnlyList<PolylineVertexData> actual, Rigid2d transform)
    {
        if (expected.Count == 0 || expected.Count != actual.Count)
        {
            return false;
        }

        for (int i = 0; i < expected.Count; i++)
        {
            if (transform.Apply(expected[i].Point).GetDistanceTo(actual[i].Point) > PointTolerance)
            {
                return false;
            }

            if (i < expected.Count - 1)
            {
                double expectedBulge = transform.Mirror ? -expected[i].Bulge : expected[i].Bulge;
                if (Math.Abs(expectedBulge - actual[i].Bulge) > BulgeTolerance)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static List<Rigid2d> DeriveTransforms(IReadOnlyList<PolylineVertexData> expected, IReadOnlyList<PolylineVertexData> actual)
    {
        if (expected.Count < 2 || expected.Count != actual.Count)
        {
            return [];
        }

        // The vertex farthest from the first one gives the most stable angle estimate.
        Point2d e0 = expected[0].Point;
        Point2d a0 = actual[0].Point;
        int far = 1;
        for (int i = 2; i < expected.Count; i++)
        {
            if (expected[i].Point.GetDistanceTo(e0) > expected[far].Point.GetDistanceTo(e0))
            {
                far = i;
            }
        }

        Vector2d e = expected[far].Point - e0;
        Vector2d a = actual[far].Point - a0;
        if (e.Length <= PointTolerance || Math.Abs(e.Length - a.Length) > PointTolerance)
        {
            return [];
        }

        return
        [
            Rigid2d.Between(e0, a0, e, a, mirror: false),
            Rigid2d.Between(e0, a0, new Vector2d(e.X, -e.Y), a, mirror: true),
        ];
    }

    /// <summary>
    /// p → To + R(θ)·M·(p − From), with M = reflection in the local x-axis when
    /// <see cref="Mirror"/>. The pivot form keeps the arithmetic local: project coordinates
    /// are ~10^6, and rotating about the origin would turn a tiny angle error into
    /// millimetres.
    /// </summary>
    private readonly record struct Rigid2d(Point2d From, Point2d To, double Cos, double Sin, bool Mirror)
    {
        public static readonly Rigid2d Identity = new(Point2d.Origin, Point2d.Origin, 1.0, 0.0, false);

        public bool IsIdentity =>
            !Mirror &&
            Math.Abs(Cos - 1.0) < 1e-12 &&
            Math.Abs(Sin) < 1e-12 &&
            (To - From).Length < PointTolerance;

        public static Rigid2d Between(Point2d from, Point2d to, Vector2d expectedDirection, Vector2d actualDirection, bool mirror)
        {
            double angle = Math.Atan2(actualDirection.Y, actualDirection.X) - Math.Atan2(expectedDirection.Y, expectedDirection.X);
            return new Rigid2d(from, to, Math.Cos(angle), Math.Sin(angle), mirror);
        }

        public Point2d Apply(Point2d point)
        {
            double x = point.X - From.X;
            double y = point.Y - From.Y;
            if (Mirror)
            {
                y = -y;
            }

            return new Point2d(To.X + (Cos * x) - (Sin * y), To.Y + (Sin * x) + (Cos * y));
        }
    }
}
