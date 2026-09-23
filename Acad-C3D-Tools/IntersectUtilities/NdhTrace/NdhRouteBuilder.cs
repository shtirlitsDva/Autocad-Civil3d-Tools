using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using IntersectUtilities.UtilsCommon.Enums;

using MathNet.Numerics.LinearAlgebra;

using System;
using System.Collections.Generic;
using System.Linq;

namespace IntersectUtilities.NdhTrace;

/// <summary>
/// A route vertex as the new pipeline wants it: a corner point with its bend
/// radius. An arc is ONE vertex at its tangent intersection (PI) carrying the
/// arc's radius; radius 0 is an elbow (or a straight-through vertex when the
/// route does not turn there).
/// </summary>
internal readonly record struct NdhRouteVertex(double X, double Y, double BendRadius);

/// <summary>"From this vertex on, the pipe IS this identity."</summary>
internal readonly record struct NdhIdentityBoundary(
    int VertexIndex, PipeSystemEnum System, PipeTypeEnum Type, int Dn);

/// <summary>
/// The straight the parts on one vertex take, as the new pipeline lays them:
/// <paramref name="Back"/> metres back from the vertex and
/// <paramref name="Forward"/> metres on. <paramref name="MinimumPipe"/> is the
/// drawing's shortest pipe between two parts that do not touch.
/// </summary>
internal readonly record struct PartStraight(double Back, double Forward, double MinimumPipe);

/// <summary>
/// Asks the new pipeline how much straight the things standing at a vertex
/// take. ONE question (NDH's reach-and-radius.md Law R1): the pipe arriving,
/// the pipe leaving - equal where the pipe does not change - how far the route
/// turns, and whether a PART makes that turn or the pipe itself is bent.
/// <para>
/// It names no part and no role. Which part stands at the vertex, and how far
/// it reaches, is the new pipeline's answer; this importer's job is to say what
/// MEETS there. It was two questions, and two questions could answer neither a
/// vertex carrying both a change and a corner nor a vertex carrying neither -
/// which is why four bare metre constants once stood in this file.
/// </para>
/// </summary>
internal interface INdhPartStraight
{
    PartStraight At(
        LegacyIdentitySpan before, LegacyIdentitySpan after, double turnDegrees,
        bool turnedByAPart);
}

/// <summary>
/// The straight a connection takes out of its main: <paramref name="Back"/>
/// metres back from the branch point and <paramref name="Forward"/> metres on,
/// both measured from the branch point and not from any one part.
/// </summary>
internal readonly record struct JunctionStraight(double Back, double Forward);

/// <summary>
/// Asks the new pipeline how much of a main a connection will occupy - Law R1
/// asked of the one thing standing on a main that is not at a vertex of the
/// main's own route.
/// <para>
/// It is asked BEFORE the main is routed, which is the whole point: NDH resolves
/// a junction when the connection is made, from a branch identity and an outlet
/// the route does not otherwise have, and until this door existed the route
/// stood in for the answer with two invented metres. The Produkt is PINNED - the
/// import translates, so the Afgreningsmatrix is never asked.
/// </para>
/// </summary>
internal interface INdhJunctionStraight
{
    JunctionStraight At(
        LegacyIdentitySpan main, LegacyIdentitySpan branch, bool branchAtStart,
        NdhBranchOutlet outlet, string produkt);
}

/// <summary>
/// A connection standing on the pipeline being routed: its branch point on the
/// centreline, and the straight the new pipeline says it takes there.
/// </summary>
internal sealed record NdhJunctionSeat(Point2d Site, JunctionStraight Straight);

internal sealed class NdhRoute
{
    public List<NdhRouteVertex> Vertices { get; } = new List<NdhRouteVertex>();

    /// <summary>
    /// The legacy block whose corner made each vertex, index-aligned with
    /// <see cref="Vertices"/>; empty for a vertex no block made (an end, a
    /// bend the centreline itself turns, a vertex a boundary asked for).
    /// </summary>
    public List<string> VertexCauseHandles { get; } = new List<string>();

    /// <summary>
    /// What the block at each vertex said about its legs, index-aligned with
    /// <see cref="Vertices"/>. A vertex no block made states none.
    /// </summary>
    public List<LegacyLegs> VertexLegs { get; } = new List<LegacyLegs>();
    public List<NdhIdentityBoundary> Boundaries { get; } = new List<NdhIdentityBoundary>();
    /// <summary>Every place the route had to deviate from the trace, for the report.</summary>
    public List<string> Adjustments { get; } = new List<string>();
}

/// <summary>
/// Converts a traced Centreline (lines and arcs) plus its identity spans into
/// the vertex route of a new pipeline.
///
/// Straights are kept to within <see cref="MaxLineDeviation"/>: a line vertex
/// is dropped only where the route stays that close to the trace without it.
/// An arc becomes a PI vertex at the intersection of the straights on either
/// side of it, so a fitted arc that is not quite tangent to its straights is
/// re-filleted instead of kinking them. A corner stays sharp (radius 0) only
/// where a legacy elbow or F-model makes it; any other turn was an elastic bend
/// of the pipe and gets the largest radius its legs allow. A run of such
/// corners - a large bend drafted as a polygon of short straights - becomes
/// the fewest arcs that follow the trace within 5 cm (see FitArcs).
///
/// A corner made by a fixed-angle legacy elbow is nudged to turn exactly that
/// angle: the block is the evidence of the part, the drafted corner only near it.
///
/// An identity change stands where the centre of its legacy part stands, which
/// is where the new pipeline centres its own part - unless the parts the new
/// pipeline lays for it would reach into a bend or a corner: the straight they
/// take is the new pipeline's answer (<see cref="INdhPartStraight"/>), never a
/// length kept here, and the change moves along its straight until they fit;
/// elastic bends are sized around them. A change inside a legacy arc or on a
/// sharp corner is moved onto a straight the same way and reported. A stretch holding no legacy pipe and no part of
/// its own is only the length of the parts around it, so the changes on either
/// side of it are ONE change: a Y-model with a materialeskift welded to its end is sent as bonded
/// steel to twin AluPex, and the new pipeline lays the Y-rør and the
/// materialeskift itself, at the catalogue's distances.
///
/// A legacy branch junction stands on a straight, so the route stays straight
/// across every junction seated on it (<see cref="NdhJunctionSeat"/>): no fitted
/// arc or elastic bend reaches into it, and a drafting kink standing inside it
/// is moved out along the junction's straight. Only a legacy ARC through a
/// junction is kept: the legacy drawing really curves there, and NDH says so.
/// </summary>
internal static partial class NdhRouteBuilder
{
    //Segments shorter than this are drafting noise.
    private const double MinSegmentLength = 1e-6;
    //An arc bowing less than this off its chord is a straight: the bisector
    //fit emits such near-straight arcs, and filleting one of them into a
    //neighbouring corner would swing a huge radius through it.
    private const double MaxStraightSagitta = 0.005;
    //A single fillet must turn less than 180 degrees; longer arcs are split.
    private static readonly double MaxArcSweep = ToRad(150.0);
    //How far the route may stray from the trace where it drops a line vertex.
    private const double MaxLineDeviation = 0.01;
    //The route stays this close to the trace wherever it rounds what the
    //trace drew: a fitted arc (see FitArcs) or an elastic bend (see SizeBends).
    //
    //NOT A REACH: it answers HOW FAITHFUL the new route must be to the old
    //drawing, which is this import's own accuracy promise and belongs to no
    //part. It bounds a radius from above; what a part occupies bounds it from
    //below, and that is asked.
    private const double MaxTraceDeviation = 0.05;
    //The pipeline's own straight test: a vertex turning more than this
    //carries a fitting, so it must be given a bend radius or be an elbow.
    private const double StraightTurn = 1e-6;
    //A junction between an arc and its neighbour turning more than this is
    //a real corner next to the arc, not a slightly off fitted tangent.
    private static readonly double ArcJoinTolerance = ToRad(3.0);
    //How far from a legacy fitting a corner may be and still be made by it.
    //A bonded elbow pair stands half the carrier spacing (up to ~0.75 m) off
    //the centreline corner; an F-model join corner is where the twin and
    //bonded end tangents meet, up to a few metres from its insertion point.
    //
    //NOT A REACH: these answer RECOGNITION - "is this legacy block what made
    //this corner?" - about a drawing NDH did not make and cannot be asked
    //about. Once the corner is attributed, what the NEW part there occupies is
    //asked (reach-and-radius.md <out-of-scope> names both of these).
    private const double ElbowReach = 1.5;
    //NOT A REACH either, and for the same reason: the F-model's recognition
    //window.
    private const double FModelReach = 3.0;
    //The smallest standard elbow is 15 degrees; a kink much smaller than
    //that next to an elbow is not the elbow's.
    private static readonly double MinFittingTurn = ToRad(5.0);
    //The route must not fold back on itself: the pipeline cannot model it.
    private static readonly double MaxTurn = ToRad(179.0);
    //A boundary this close to a vertex is put on that vertex.
    private const double VertexSnap = 1e-3;
    //Setbacks must fit their leg with this much to spare.
    private const double LegSlack = 1e-6;
    //An F-rør merges twin and bonded AND turns exactly 90 degrees, so a
    //Twin<->Enkelt change sits on its corner only when the corner turns 90
    //degrees to within this (radians) - NDH's own angle tolerance, the one its
    //square-corner test reads (kPipeAngleTolerance). The corner is snapped
    //there first (SnapFittingAngles), as a fixed-angle elbow's is. (Live run
    //2026-09-19, F5: 014's F corner turned 90.087 degrees; NDH stopped
    //accepting that on 2026-09-16 and refused the whole pipeline with
    //TwoFittingsAtOneVertex, losing its five branches.)
    private const double FCornerSquare = 1e-6;
    //A corner by a fixed-angle elbow is snapped to the elbow's angle when it
    //turns within this of it; further off it is not that part.
    private static readonly double MaxAngleSnap = ToRad(2.0);
    //Snapped corners turn their angle to within this (radians), and elbow
    //legs keep their length to within this (metres) - ten times inside NDH's
    //own angle tolerance, kPipeAngleTolerance = 1e-6 radians (NorsynCore
    //BendCalculator.h; about 5.7e-5 degrees), the figure FCornerSquare above
    //reads too.
    private const double SnappedTurnTolerance = 1e-7;
    //A leg up to this long between two snapped elbows, or from one to a
    //pipeline end, is the elbows' own leg: snapping keeps its length.
    //
    //NOT A REACH: it answers RECOGNITION of the legacy drawing - which short
    //legs were drawn as one elbow assembly and must keep their length when the
    //corners are snapped square. It is about what the old drawing IS, never
    //about what a new part occupies.
    private const double ElbowLegLength = 5.0;
    //An arc split at a junction keeps at least this much arc on each side of
    //the straight: less is not an arc, it is drafting noise.
    //
    //NOT A REACH: it answers WHAT COUNTS AS AN ARC in a traced drawing, a
    //noise floor on the legacy geometry. Nothing stands on it.
    private const double ArcSplitKeep = 0.15;
    //Gauss-Newton settles the snap in a handful of steps; needing more means
    //the constraints conflict.
    private const int MaxSnapIterations = 50;

    private enum Kind { Line, Arc }

    private sealed class Seg
    {
        public Kind Kind;
        public Point2d A, B;
        public double R, D0, D1;
        public Vector2d T0, T1;
    }

    private enum VertexKind
    {
        End,
        /// <summary>A straight-through vertex carrying a boundary.</summary>
        Straight,
        /// <summary>A legacy arc: its radius is given.</summary>
        Fillet,
        /// <summary>A sharp corner made by a legacy elbow.</summary>
        Elbow,
        /// <summary>A sharp corner made by a legacy F-model.</summary>
        FCorner,
        /// <summary>An elastic bend: its radius is what the legs allow.</summary>
        Bend,
    }

    /// <summary>
    /// A CHANGE OF PIPE ON A VERTEX: the pipe arriving and the pipe leaving,
    /// which are only ever known together.
    /// </summary>
    private readonly record struct IdentityChange(
        LegacyIdentitySpan From, LegacyIdentitySpan To)
    {
        //A CHANGE TO THE PIPE ALREADY RUNNING IS NOT ONE. While boundaries are
        //being placed a vertex can hold A -> A for a moment - a boundary was
        //dropped onto it and the pipe either side is the same - and the dedup
        //pass only clears that at the end of the call. NDH answers nought for
        //such a vertex, so every reader of this field must agree that nothing
        //stands there.
        public bool Stands => !SameIdentity(From, To);
    }

    /// <summary>One vertex of the route under construction.</summary>
    private sealed class RouteVertex
    {
        public Point2d P;
        public VertexKind Kind;
        public double Radius;
        //Centreline distances where the vertex's geometry starts and ends: the
        //two tangent points of a fillet or a sized bend, one distance otherwise.
        public double D0, D1;
        //The turn of the legacy elbow making this corner; NaN if none or any.
        public double NominalTurn = double.NaN;
        //WHICH LEGACY BLOCK MADE THIS CORNER, empty on a vertex no block made.
        //It rides here so the import can name the component this vertex will
        //carry once the pipeline stands - the route itself is only geometry.
        public string CauseHandle = "";
        //WHAT THAT BLOCK SAID ABOUT ITS LEGS, read while the legacy drawing was
        //open and carried as data ever since.
        public LegacyLegs Legs = new NoLegsStated();
        //WHAT CHANGES ON THIS VERTEX: the pipe arriving and the pipe leaving.
        //Null on a vertex that changes nothing, and then the pipe running
        //through it is both. It is the CHANGE that is kept here, never the
        //straight it takes - that is asked, once, below.
        //
        //ONE FIELD, so that half a change cannot be written. The two ends were
        //two nullables that every site happened to set together; nothing made
        //them, and the reach (`TakenAt`, which reads a missing end as "the pipe
        //runs through") and the contact rule (`EndOfStraight`, which reads it as
        //"nothing stands here") would have answered the same vertex
        //differently. A pair that must agree is one value.
        //
        //WHILE `PlaceBoundaries` RUNS IT HOLDS TWO CONFIDENCES. An F corner is
        //marked before any boundary is placed, so that a straight measured
        //against it already sees the change - that marking is a FORECAST, and
        //the corner may yet lose its boundary. Every vertex behind the cursor
        //holds the LAID answer, rewritten from what survived the drops. The two
        //converge when the call returns, and only the laid reading leaves it.
        public IdentityChange? Change;
        public bool Turns => Kind is not (VertexKind.End or VertexKind.Straight);
        //WHETHER THIS VERTEX'S TURN IS STILL TO COME OUT OF THE STRAIGHT. A
        //fillet's arc is already in D0/D1, so the straight measured against it
        //stops where the arc does and the vertex takes nothing more; an elbow,
        //an F corner and an unsized bend are not yet in the route, so what
        //stands on them is still to be reserved. A route fact, not a reach:
        //how much they take is NDH's answer.
        public bool TurnNotYetInRoute =>
            Kind is VertexKind.Elbow or VertexKind.FCorner or VertexKind.Bend;
        //WHETHER A PART MAKES THE TURN, or the pipe itself is bent. Read off
        //the legacy block that made the corner, and nothing more: which part,
        //and how far it reaches, is NDH's answer.
        public bool TurnedByAPart => Kind is VertexKind.Elbow or VertexKind.FCorner;
        //AN ELASTIC BEND STILL WAITING FOR ITS RADIUS. Nothing can say what it
        //takes of a leg until it has one, which is why two of them share their
        //leg evenly instead of reading a reach off each other.
        //
        //It asks RadiusDecided and not the KIND, because a bend stays a Bend
        //after it is sized: SizeBends walks ascending, so by the time a bend is
        //sized its BACK neighbour already has a real setback written in - and
        //reading the kind threw that setback away and took the half-leg share
        //anyway, which is a caller inventing a length beside one it had already
        //computed.
        public bool RadiusStillUnknown => Kind == VertexKind.Bend && !RadiusDecided;
        //WHETHER THIS BEND'S RADIUS HAS BEEN SETTLED, either by being sized or
        //by being left sharp for want of leg room. Both are answers; what is
        //unknown is a bend the sizing walk has not reached yet.
        public bool RadiusDecided;

        /// <summary>
        /// THE ONE QUESTION, put for this vertex: everything standing on it at
        /// once - the change it carries and the turn of
        /// <paramref name="turnDegrees"/> it makes - answered by the new
        /// pipeline. <paramref name="pipe"/> is the pipe running through a
        /// vertex that changes nothing.
        /// <para>
        /// Asking the change and the turn separately is what this replaced, and
        /// it could answer neither of the two vertices that carry both: an F-rør
        /// corner was asked as an elbow on unchanging pipe, so the F's own reach
        /// - the whole point of an F-rør, which IS the corner - was dropped, and
        /// an elbow's leg was counted in its place.
        /// </para>
        /// </summary>
        public PartStraight TakenAt(
            INdhPartStraight straight, LegacyIdentitySpan pipe, double turnDegrees) =>
            straight.At(Change?.From ?? pipe, Change?.To ?? pipe, turnDegrees, TurnedByAPart);
    }

    public static NdhRoute Build(
        LegacyPipelineTrace trace, INdhPartStraight straight, IReadOnlyList<NdhJunctionSeat> junctions)
    {
        Polyline centreline = trace.Centreline;
        if (trace.Spans.Count == 0) throw new ArgumentException("No identity spans.", nameof(trace));

        NdhRoute route = new NdhRoute();
        List<Seg> segs = SimplifyLines(Segments(centreline));
        if (segs.Count == 0) throw new ArgumentException("Degenerate centreline.", nameof(trace));

        List<RouteVertex> vs = Vertices(segs, trace.Corners);
        for (int i = 1; i < vs.Count - 1; i++)
            if (TurnAt(vs, i) >= MaxTurn)
                throw new InvalidOperationException(
                    $"The route folds back on itself at {vs[i].D0:F2} m.");

        //BEFORE the angles are snapped, so that an elbow whose leg this moves is
        //put back on its exact angle by the snap that follows.
        FlattenStrayKinks(vs, route.Adjustments);
        SnapFittingAngles(vs, route.Adjustments);
        List<LegacyIdentitySpan> identities = CleanSpans(trace.Spans, route.Adjustments);
        //A CANDIDATE change, on open pipe, before it has a vertex to stand on:
        //no turn, because where it will stand is what is being worked out. Once
        //it has a vertex, that vertex asks for itself.
        List<PartStraight> parts = identities
            .Select((s, i) => i == 0
                ? new PartStraight()
                : straight.At(identities[i - 1], s, 0.0, false))
            .ToList();
        List<Seat> seats = Seats(junctions, centreline);
        FitArcs(vs, centreline, identities, parts, seats, route.Adjustments);
        FitFillets(vs, route.Adjustments);
        FilletZones(vs, centreline);
        SplitArcsAtJunctions(vs, seats, centreline, route.Adjustments);
        FitFillets(vs, route.Adjustments);
        FilletZones(vs, centreline);
        List<(RouteVertex V, LegacyIdentitySpan Span)> bounds = PlaceBoundaries(
            vs, identities, parts, straight, centreline, seats, route.Adjustments);
        //A boundary kept right at a fillet's tangent point can leave the leg
        //between them a hair short of the fillet's setback.
        FitFillets(vs, route.Adjustments);
        ClearJunctions(vs, bounds, straight, seats, route.Adjustments);
        SizeBends(vs, bounds, straight, seats, route.Adjustments);

        foreach (RouteVertex v in vs)
        {
            route.Vertices.Add(new NdhRouteVertex(v.P.X, v.P.Y, v.Radius));
            //INDEX-ALIGNED WITH Vertices, and so with the causes the build hands
            //back: the i-th vertex the importer asks for is the i-th id it is
            //given, and this says which legacy block that vertex answers for.
            route.VertexCauseHandles.Add(v.CauseHandle);
            route.VertexLegs.Add(v.Legs);
        }
        foreach ((RouteVertex v, LegacyIdentitySpan s) in bounds)
            route.Boundaries.Add(new NdhIdentityBoundary(vs.IndexOf(v), s.System, s.Type, s.Dn));

        return route;
    }

    #region Segments
    private static List<Seg> Segments(Polyline pl)
    {
        List<Seg> segs = new List<Seg>();
        for (int i = 0; i < pl.NumberOfVertices - 1; i++)
        {
            double d0 = pl.GetDistanceAtParameter(i);
            double d1 = pl.GetDistanceAtParameter(i + 1);
            if (d1 - d0 < MinSegmentLength) continue;

            Point2d a = pl.GetPoint2dAt(i);
            Point2d b = pl.GetPoint2dAt(i + 1);
            double bulge = pl.GetBulgeAt(i);

            if (Math.Abs(bulge) * a.GetDistanceTo(b) / 2.0 < MaxStraightSagitta)
            {
                segs.Add(LineSeg(a, d0, b, d1));
                continue;
            }

            double sweep = 4.0 * Math.Atan(bulge);
            CircularArc2d arc = pl.GetArcSegment2dAt(i);
            int pieces = (int)Math.Ceiling(Math.Abs(sweep) / MaxArcSweep);
            for (int k = 0; k < pieces; k++)
            {
                double f0 = (double)k / pieces, f1 = (double)(k + 1) / pieces;
                Point2d pa = k == 0 ? a : Rotate(a, arc.Center, sweep * f0);
                Point2d pb = k == pieces - 1 ? b : Rotate(a, arc.Center, sweep * f1);
                segs.Add(new Seg
                {
                    Kind = Kind.Arc,
                    A = pa,
                    B = pb,
                    R = arc.Radius,
                    D0 = d0 + (d1 - d0) * f0,
                    D1 = d0 + (d1 - d0) * f1,
                    T0 = ArcTangent(pa, arc.Center, sweep),
                    T1 = ArcTangent(pb, arc.Center, sweep),
                });
            }
        }
        return segs;
    }

    private static Seg LineSeg(Point2d a, double d0, Point2d b, double d1)
    {
        Vector2d t = (b - a).GetNormal();
        return new Seg { Kind = Kind.Line, A = a, B = b, D0 = d0, D1 = d1, T0 = t, T1 = t };
    }

    /// <summary>
    /// Drops every line vertex the route can do without while staying within
    /// <see cref="MaxLineDeviation"/> of the trace (Douglas-Peucker over each run
    /// of consecutive lines). What remains are real kinks, however small.
    /// </summary>
    private static List<Seg> SimplifyLines(List<Seg> segs)
    {
        List<Seg> result = new List<Seg>();
        int i = 0;
        while (i < segs.Count)
        {
            if (segs[i].Kind != Kind.Line)
            {
                result.Add(segs[i++]);
                continue;
            }

            int j = i;
            while (j + 1 < segs.Count && segs[j + 1].Kind == Kind.Line) j++;

            List<(Point2d P, double D)> pts = new() { (segs[i].A, segs[i].D0) };
            for (int k = i; k <= j; k++) pts.Add((segs[k].B, segs[k].D1));

            bool[] keep = new bool[pts.Count];
            keep[0] = keep[pts.Count - 1] = true;
            Stack<(int, int)> todo = new Stack<(int, int)>();
            todo.Push((0, pts.Count - 1));
            while (todo.Count > 0)
            {
                (int a, int b) = todo.Pop();
                if (b - a < 2) continue;
                LineSegment2d chord = new LineSegment2d(pts[a].P, pts[b].P);
                int far = -1;
                double farDist = MaxLineDeviation;
                for (int k = a + 1; k < b; k++)
                {
                    double dist = chord.GetDistanceTo(pts[k].P);
                    if (dist > farDist) { farDist = dist; far = k; }
                }
                if (far < 0) continue;
                keep[far] = true;
                todo.Push((a, far));
                todo.Push((far, b));
            }

            int last = 0;
            for (int k = 1; k < pts.Count; k++)
            {
                if (!keep[k]) continue;
                result.Add(LineSeg(pts[last].P, pts[last].D, pts[k].P, pts[k].D));
                last = k;
            }
            i = j + 1;
        }
        return result;
    }

    private static Point2d Rotate(Point2d p, Point2d c, double ang)
    {
        double cos = Math.Cos(ang), sin = Math.Sin(ang);
        double x = p.X - c.X, y = p.Y - c.Y;
        return new Point2d(c.X + x * cos - y * sin, c.Y + x * sin + y * cos);
    }

    /// <summary>Unit direction of travel at <paramref name="p"/> on the arc.</summary>
    private static Vector2d ArcTangent(Point2d p, Point2d c, double sweep)
    {
        Vector2d r = (p - c).GetNormal();
        //CCW travel is the radius turned left, CW turned right.
        return sweep > 0.0 ? new Vector2d(-r.Y, r.X) : new Vector2d(r.Y, -r.X);
    }
    #endregion

    #region Vertices
    private static List<RouteVertex> Vertices(List<Seg> segs, IReadOnlyList<LegacyCorner> corners)
    {
        List<RouteVertex> vs = new List<RouteVertex>
        {
            new RouteVertex { P = segs[0].A, Kind = VertexKind.End, D0 = segs[0].D0, D1 = segs[0].D0 },
        };

        for (int i = 0; i < segs.Count; i++)
        {
            Seg s = segs[i];

            if (s.Kind == Kind.Arc)
            {
                //Tangent lines on either side: a straight neighbour's own line
                //when there is one, otherwise the arc's end tangent - averaged
                //with an arc neighbour's so a compound join shares one tangent.
                (Point2d p0, Vector2d t0) = TangentLine(segs, i, true);
                (Point2d p1, Vector2d t1) = TangentLine(segs, i, false);

                if (!TryIntersect(p0, t0, p1, t1, out Point2d pi) &&
                    !TryIntersect(s.A, s.T0, s.B, s.T1, out pi))
                    throw new InvalidOperationException(
                        $"Arc at {s.D0:F2}-{s.D1:F2} m has no tangent intersection.");

                vs.Add(new RouteVertex
                { P = pi, Kind = VertexKind.Fillet, Radius = s.R, D0 = s.D0, D1 = s.D1 });
            }

            if (i == segs.Count - 1) break;

            //Every kink between two straights is a corner - the simplification
            //left only real ones. Against an arc only a real corner is: a
            //slightly off arc join is re-filleted by building the arc's PI on
            //the neighbouring straight (see TangentLine).
            Seg n = segs[i + 1];
            double turn = Math.Abs(Turn(s.T1, n.T0));
            bool lineToLine = s.Kind == Kind.Line && n.Kind == Kind.Line;
            if (turn > (lineToLine ? StraightTurn : ArcJoinTolerance))
            {
                (VertexKind kind, double nominal, string cause, LegacyLegs legs) =
                    CornerKind(s.B, turn, corners);
                vs.Add(new RouteVertex
                {
                    P = s.B, Kind = kind, NominalTurn = nominal, CauseHandle = cause, Legs = legs,
                    D0 = s.D1, D1 = s.D1,
                });
            }
        }

        Seg last = segs[segs.Count - 1];
        vs.Add(new RouteVertex { P = last.B, Kind = VertexKind.End, D0 = last.D1, D1 = last.D1 });

        //Collapse coincident vertices (a corner on an end, an arc ending at
        //the start); an end survives, else the turning one.
        for (int i = vs.Count - 1; i > 0; i--)
        {
            if (vs[i].P.GetDistanceTo(vs[i - 1].P) > MinSegmentLength) continue;
            int drop = vs[i].Kind == VertexKind.End ? i - 1 : i;
            if (drop == 0) drop = 1;
            if (vs.Count > 2) vs.RemoveAt(drop);
        }

        return vs;
    }

    /// <summary>
    /// What makes the corner at <paramref name="p"/>, the turn its elbow is
    /// made for, and WHICH BLOCK it was - the last so that the component this
    /// vertex ends up carrying can be named afterwards. A corner no block
    /// explains answers with an empty handle, not with a nearest guess.
    /// </summary>
    private static (VertexKind Kind, double NominalTurn, string CauseHandle, LegacyLegs Legs) CornerKind(
        Point2d p, double turn, IReadOnlyList<LegacyCorner> corners)
    {
        if (turn < MinFittingTurn) return (VertexKind.Bend, double.NaN, "", new NoLegsStated());
        foreach (LegacyCorner c in corners)
        {
            double d = c.Position.GetDistanceTo(p);
            if (c.Kind == LegacyCornerKind.Elbow && d <= ElbowReach)
                return (VertexKind.Elbow, c.NominalTurn, c.Handle, c.Legs);
            if (c.Kind == LegacyCornerKind.FModel && d <= FModelReach)
                return (VertexKind.FCorner, c.NominalTurn, c.Handle, c.Legs);
        }
        return (VertexKind.Bend, double.NaN, "", new NoLegsStated());
    }

    /// <summary>
    /// Takes out a kink NOTHING CAN ROUND: a corner too shallow to carry any
    /// fitting, standing against a part whose leg claims the stretch up to it,
    /// which the route can run straight through without leaving the trace.
    /// <para>
    /// WHY IT CANNOT BE LEFT. A kink no arc has room for is given no radius,
    /// and a radius of nought is how this route says ELBOW - so NDH stands a
    /// fitting on a corner the drafter never meant to draw, and on a shallow
    /// corner there is usually no such fitting to stand ("made at 90 degrees
    /// only; this corner turns 1.2"). The kink is the defect; straightening it
    /// is what the drawing meant.
    /// </para>
    /// <para>
    /// WHY ONLY AGAINST A PART. A shallow kink out on open pipe is ROUNDED,
    /// and rounding is the faithful answer - the import translates and does not
    /// redraw, so it moves no geometry it does not have to. What starves a bend
    /// is a neighbour that RESERVES its stretch: an elbow's leg ends where the
    /// kink stands, the bend's share of that stretch is nothing, and no arc
    /// fits in nothing. Measured on the legacy drawing 2026-09-22: pipeline 008
    /// wobbles at EVERY vertex over 300 m, 0.09 to 2.75 degrees, and every one
    /// of those kinks is rounded without complaint EXCEPT the two standing
    /// against a 90 degree elbow.
    /// </para>
    /// <para>
    /// The part's actual leg is not read here and cannot be - a reach is asked
    /// per identity, and the identity at a vertex is not settled until the
    /// boundaries are laid. So this is decided on the part STANDING there
    /// rather than on the leg it will turn out to want, and a kink whose
    /// neighbour turns out to have left it room is straightened anyway. That
    /// costs at most <see cref="MaxTraceDeviation"/> of trace, and it is said
    /// out loud in the report.
    /// </para>
    /// <para>
    /// THE BOUND IS <see cref="MaxTraceDeviation"/>, which is already how far
    /// the route may stray wherever it declines to reproduce a drawn corner
    /// exactly: <c>SizeBends</c> caps a bend's radius by that same figure so
    /// its arc does not bulge further than that from the kink. Rounding keeps
    /// the promise by curving, this keeps it by running straight - one promise,
    /// two ways, not a second number. <see cref="MaxLineDeviation"/> is a
    /// different question: it bounds the BULK simplification of a polyline run,
    /// which cascades, and is tighter for that reason.
    /// </para>
    /// <para>
    /// The vertex is dropped, not nudged, so nothing is left behind turning
    /// nought degrees; both neighbours keep their positions, and the elbow one
    /// of them is gets put back on its exact angle by
    /// <see cref="SnapFittingAngles"/>, which runs next.
    /// </para>
    /// </summary>
    private static void FlattenStrayKinks(List<RouteVertex> vs, List<string> notes)
    {
        //A KINK TOO SHALLOW TO BE A FITTING. `CornerKind` already draws this
        //line - under MinFittingTurn it does not even look for a block - so a
        //vertex that is a Bend AND turns less than that is one the drawing
        //never put a part on.
        bool Shallow(int k) =>
            k > 0 && k < vs.Count - 1
            && vs[k].Kind == VertexKind.Bend
            && TurnAt(vs, k) < MinFittingTurn;

        //A NEIGHBOUR THAT RESERVES ITS STRETCH: a vertex a PART turns, which is
        //what `TurnedByAPart` is the one test for. An end is NOT one - it
        //carries a cap only where a cap is authored, and this import authors
        //none - and a change is not one either, because what a change reserves
        //depends on the identity either side of it and that is not settled
        //until the boundaries are laid.
        bool Reserves(int k) => vs[k].TurnedByAPart;

        //A FILLET IS AN ARC THE DRAWING DREW, carrying a radius already fitted
        //to the tangents its neighbours make. Drop a vertex beside one and that
        //radius belongs to a tangent that no longer exists, so a kink is
        //straightened only where straightening is all it is.
        bool Drawn(int k) => vs[k].Kind == VertexKind.Fillet;

        for (int i = vs.Count - 2; i > 0; i--)
        {
            if (!Shallow(i)) continue;
            if (!Reserves(i - 1) && !Reserves(i + 1)) continue;
            if (Drawn(i - 1) || Drawn(i + 1)) continue;

            Point2d a = vs[i - 1].P, b = vs[i + 1].P;
            Vector2d ab = b - a;
            if (ab.Length < MinSegmentLength) continue;
            double off = new LineSegment2d(a, b).GetDistanceTo(vs[i].P);
            if (off > MaxTraceDeviation) continue;

            notes.Add($"kink at {vs[i].D0:F2} m taken out, {off:F3} m from the trace: " +
                      $"it turns {ToDeg(TurnAt(vs, i)):F1}\u00b0, too little for any fitting, " +
                      $"and the {vs[i - 1].Kind} beside it leaves no room to bend");
            vs.RemoveAt(i);
        }
    }

    /// <summary>
    /// Makes every corner a fixed-angle legacy elbow or an F-model stands on turn
    /// exactly the part's angle. Only a corner already within <see cref="MaxAngleSnap"/> is
    /// snapped; one further off is not that part, and the pipeline reports it.
    ///
    /// An elbow leg (see <see cref="ElbowLegLength"/>) keeps its length, so
    /// parts that touch in the legacy still touch. The snapped corners, and a
    /// pipeline end on an elbow leg, move; every other vertex stays.
    ///
    /// These constraints rarely pin the geometry down: two elbows joined by an
    /// elbow leg can slide together along their outer legs and still turn
    /// exactly. So the vertices go to the NEAREST place where every constraint
    /// holds - the least total squared move - found by Gauss-Newton projection:
    /// each step solves the constraints, linearised where the vertices are now,
    /// for the smallest move from where they started.
    /// </summary>
    private static void SnapFittingAngles(List<RouteVertex> vs, List<string> notes)
    {
        int last = vs.Count - 1;
        List<int> snap = Enumerable.Range(1, Math.Max(0, last - 1))
            .Where(i => !double.IsNaN(vs[i].NominalTurn) &&
                Math.Abs(TurnAt(vs, i) - vs[i].NominalTurn) <= MaxAngleSnap)
            .ToList();
        if (snap.Count == 0) return;
        HashSet<int> snapped = new HashSet<int>(snap);

        //Elbow legs: between two snapped corners, or from one to a pipeline end.
        List<(int A, int B, double Length)> legs = new List<(int, int, double)>();
        for (int k = 0; k < last; k++)
        {
            bool held = (snapped.Contains(k) && snapped.Contains(k + 1)) ||
                (k == 0 && snapped.Contains(1)) || (k + 1 == last && snapped.Contains(last - 1));
            double length = vs[k].P.GetDistanceTo(vs[k + 1].P);
            if (held && length <= ElbowLegLength) legs.Add((k, k + 1, length));
        }

        //Each moving vertex owns two columns (x, y) of the constraint Jacobian.
        int[] col = Enumerable.Repeat(-1, vs.Count).ToArray();
        int n = 0;
        foreach (int i in snap.Concat(legs.SelectMany(l => new[] { l.A, l.B })).Distinct().OrderBy(i => i))
        {
            col[i] = n;
            n += 2;
        }
        int m = snap.Count + legs.Count;

        Point2d[] from = vs.Select(v => v.P).ToArray();
        double worst;
        for (int iteration = 0; ; iteration++)
        {
            Matrix<double> jacobian = Matrix<double>.Build.Dense(m, n);
            Vector<double> residual = Vector<double>.Build.Dense(m);
            int row = 0;
            foreach (int i in snap)
            {
                //A leg w's direction changes by w's perpendicular over |w|^2.
                Vector2d u = vs[i].P - vs[i - 1].P, v = vs[i + 1].P - vs[i].P;
                double turn = Turn(u, v);
                double side = Math.Sign(turn);
                Vector2d du = Perpendicular(u) / u.DotProduct(u) * side;
                Vector2d dv = Perpendicular(v) / v.DotProduct(v) * side;
                residual[row] = side * turn - vs[i].NominalTurn;
                AddGradient(jacobian, row, col[i - 1], du);
                AddGradient(jacobian, row, col[i], (du + dv).Negate());
                AddGradient(jacobian, row, col[i + 1], dv);
                row++;
            }
            foreach ((int a, int b, double length) in legs)
            {
                Vector2d ab = vs[b].P - vs[a].P;
                residual[row] = ab.Length - length;
                AddGradient(jacobian, row, col[a], ab.GetNormal().Negate());
                AddGradient(jacobian, row, col[b], ab.GetNormal());
                row++;
            }

            worst = residual.AbsoluteMaximum();
            if (worst < SnappedTurnTolerance || iteration == MaxSnapIterations) break;

            //Keeping the move from the start in the Jacobian's row space is
            //what makes the result the nearest, not just any, that holds.
            Vector<double> moved = Vector<double>.Build.Dense(n);
            for (int i = 0; i <= last; i++)
            {
                if (col[i] < 0) continue;
                moved[col[i]] = vs[i].P.X - from[i].X;
                moved[col[i] + 1] = vs[i].P.Y - from[i].Y;
            }
            Matrix<double> transposed = jacobian.Transpose();
            Vector<double> step = transposed *
                ((jacobian * transposed).PseudoInverse() * (jacobian * moved - residual));
            for (int i = 0; i <= last; i++)
                if (col[i] >= 0)
                    vs[i].P = new Point2d(from[i].X + step[col[i]], from[i].Y + step[col[i] + 1]);
        }
        if (worst >= SnappedTurnTolerance)
            notes.Add($"elbow corners settled only to within {worst:G2} of their angles and legs");

        for (int i = 0; i <= last; i++)
        {
            double moved = from[i].GetDistanceTo(vs[i].P);
            if (moved <= VertexSnap) continue;
            notes.Add(vs[i].Kind == VertexKind.End
                ? $"pipeline end moved {moved:F3} m to keep its legacy elbow at its exact angle"
                : $"corner at {vs[i].D0:F2} m moved {moved:F3} m to turn exactly " +
                    $"{vs[i].NominalTurn * 180.0 / Math.PI:F0}° like its legacy " +
                    (vs[i].Kind == VertexKind.FCorner ? "F-rør" : "elbow"));
        }
    }

    private static Vector2d Perpendicular(Vector2d w) => new Vector2d(-w.Y, w.X);

    /// <summary>Adds one vertex's gradient to a Jacobian row; a fixed vertex has no columns.</summary>
    private static void AddGradient(Matrix<double> jacobian, int row, int column, Vector2d gradient)
    {
        if (column < 0) return;
        jacobian[row, column] += gradient.X;
        jacobian[row, column + 1] += gradient.Y;
    }

    /// <summary>
    /// The tangent line entering (<paramref name="entering"/>) or leaving arc
    /// <paramref name="i"/>.
    /// </summary>
    private static (Point2d P, Vector2d T) TangentLine(List<Seg> segs, int i, bool entering)
    {
        Seg s = segs[i];
        int j = entering ? i - 1 : i + 1;
        Point2d at = entering ? s.A : s.B;
        Vector2d own = entering ? s.T0 : s.T1;

        if (j < 0 || j >= segs.Count) return (at, own);

        //A real corner against the arc: the arc keeps its own tangent.
        Seg n = segs[j];
        Vector2d other = entering ? n.T1 : n.T0;
        if (Math.Abs(entering ? Turn(other, own) : Turn(own, other)) > ArcJoinTolerance)
            return (at, own);

        if (n.Kind == Kind.Line) return (n.A, n.T0);

        //Compound or reverse arcs: share one tangent at the joint.
        Vector2d avg = own + other;
        return (at, avg.Length < 1e-12 ? own : avg.GetNormal());
    }

    /// <summary>
    /// Shrinks legacy arcs whose setbacks overrun a leg: the PIs of fitted
    /// compound arcs stand on averaged tangents, and the arcs' own radii can
    /// then ask a little more than the leg between them holds.
    /// </summary>
    private static void FitFillets(List<RouteVertex> vs, List<string> notes)
    {
        double[] given = vs.Select(v => v.Radius).ToArray();
        for (int pass = 0; pass < 100; pass++)
        {
            bool shrunk = false;
            for (int k = 0; k < vs.Count - 1; k++)
            {
                double leg = vs[k].P.GetDistanceTo(vs[k + 1].P);
                double need = FilletSetback(vs, k) + FilletSetback(vs, k + 1);
                if (need <= leg - LegSlack) continue;

                double f = Math.Max(0.0, leg - 2.0 * LegSlack) / need;
                if (vs[k].Kind == VertexKind.Fillet) vs[k].Radius *= f;
                if (vs[k + 1].Kind == VertexKind.Fillet) vs[k + 1].Radius *= f;
                shrunk = true;
            }
            if (!shrunk) break;
        }

        for (int i = 0; i < vs.Count; i++)
            if (vs[i].Radius < given[i] * 0.999)
                notes.Add($"arc R={given[i]:F2} at {vs[i].D0:F2} m reduced to " +
                    $"R={vs[i].Radius:F2} to fit between its neighbours");
    }

    /// <summary>
    /// Sets each fillet's D0/D1 to where the route's own arc starts and ends:
    /// a re-filleted arc does not start exactly where the legacy arc did, and
    /// a boundary must stay off the arc that is actually built.
    /// </summary>
    private static void FilletZones(List<RouteVertex> vs, Polyline cl)
    {
        for (int i = 1; i < vs.Count - 1; i++)
        {
            if (vs[i].Kind != VertexKind.Fillet) continue;
            double sb = FilletSetback(vs, i);
            Point2d t0 = vs[i].P - (vs[i].P - vs[i - 1].P).GetNormal() * sb;
            Point2d t1 = vs[i].P + (vs[i + 1].P - vs[i].P).GetNormal() * sb;
            double d0 = DistAt(cl, t0), d1 = DistAt(cl, t1);
            vs[i].D0 = Math.Min(d0, d1);
            vs[i].D1 = Math.Max(d0, d1);
        }
    }

    private static double DistAt(Polyline cl, Point2d p) =>
        cl.GetDistAtPoint(cl.GetClosestPointTo(new Point3d(p.X, p.Y, 0.0), false));

    private static double FilletSetback(List<RouteVertex> vs, int i) =>
        vs[i].Kind == VertexKind.Fillet && i > 0 && i < vs.Count - 1
            ? vs[i].Radius * Math.Tan(TurnAt(vs, i) / 2.0)
            : 0.0;

    /// <summary>
    /// Gives every elastic bend the largest radius whose arc stays within
    /// <see cref="MaxTraceDeviation"/> of its kink and whose setback fits what
    /// its two legs have left once what stands at their far ends is taken out.
    /// <para>
    /// WHAT STANDS THERE IS READ FROM THE THING ITSELF (NDH's reach-and-radius.md
    /// Law R1): a fillet or an already-sized bend takes its own setback, a
    /// boundary takes the straight its change's parts take, a corner takes the
    /// legs of the part making it - asked of NDH - and an end takes nothing,
    /// because nothing is authored there. Two UNSIZED bends
    /// share their leg evenly, which is not a reach and never was: neither
    /// radius exists yet. A flat half-metre once stood for every end and half a
    /// leg for every corner.
    /// </para>
    /// </summary>
    private static void SizeBends(
        List<RouteVertex> vs, IReadOnlyList<(RouteVertex V, LegacyIdentitySpan Span)> bounds,
        INdhPartStraight straight, IReadOnlyList<Seat> seats, List<string> notes)
    {
        //THE PIPE STANDING ON EACH VERTEX, READ FROM THE BOUNDARIES AS LAID.
        //`PlaceBoundaries` has already run and it MOVES boundaries - off a
        //corner, out of a junction seat, onto an F-rør - without writing the new
        //distance back onto the span. So the spans' own StartDist is the
        //legacy DRAFT, and a vertex between a boundary's drafted and laid
        //positions is on a different pipe than the draft says. `bounds` is the
        //one place that knows where each identity actually begins - it is what
        //the importer itself is handed - so this asks that, and the question is
        //asked once.
        //
        //`bounds` is never empty and `bounds[0]` is always the opening identity:
        //`Build` refuses a trace with no spans, `CleanSpans` cannot empty one,
        //and `PlaceBoundaries` seeds its list with the pipeline start and only
        //ever replaces that entry's span, never removes the entry.
        LegacyIdentitySpan[] pipeOn = PipesOn(vs, bounds);

        //What each vertex takes of the leg on each side of it. A bend is
        //written in as it is settled, and the walk is ascending, so the bend
        //after it reads a real setback (RadiusDecided) rather than a share.
        double[] back = new double[vs.Count];
        double[] forward = new double[vs.Count];
        for (int i = 0; i < vs.Count; i++)
        {
            //ONE ASK, for the change this vertex carries AND the corner a part
            //makes on it together. The turn put here is only the one a PART
            //makes: a fillet's arc is already in D0/D1 and gets its setback
            //below, and a bend has no radius yet and gets its own further down.
            LegacyIdentitySpan pipe = pipeOn[i];
            double turnDegrees = vs[i].TurnedByAPart ? ToDeg(TurnAt(vs, i)) : 0.0;
            //An end vertex asks this too and is answered: it carries no cap
            //(one is AUTHORED, and this import authors none), so it comes back
            //with whatever CHANGE stands on it and nothing more. That is NDH's
            //answer, not a number this file keeps.
            PartStraight taken = vs[i].TakenAt(straight, pipe, turnDegrees);
            back[i] = taken.Back;
            forward[i] = taken.Forward;
            //A FILLET'S ARC SETS BACK ITS OWN TANGENT POINT. `FilletSetback`
            //answers nought for every other kind and for the two ends, so it is
            //asked of every vertex rather than asked twice - once out here and
            //once inside itself.
            double sb = FilletSetback(vs, i);
            back[i] += sb;
            forward[i] += sb;
        }

        for (int i = 1; i < vs.Count - 1; i++)
        {
            RouteVertex v = vs[i];
            if (v.Kind != VertexKind.Bend) continue;

            double avail = Math.Min(
                Math.Min(
                    Share(vs[i - 1], forward[i - 1], v.P.GetDistanceTo(vs[i - 1].P)),
                    Share(vs[i + 1], back[i + 1], v.P.GetDistanceTo(vs[i + 1].P))),
                JunctionRoom(v.D0, seats));
            if (avail <= MinSegmentLength)
            {
                notes.Add($"bend at {v.D0:F2} m has no leg room, left sharp");
                //NOT SETTLED, on purpose. This bend gets no radius and never
                //will, and a radius of nought is how this route says ELBOW
                //(NdhRouteVertex) - so NDH will stand a part here whose legs
                //this file never asked for and never reserved. Marking it
                //decided would hand the next bend along the whole rest of the
                //leg, right up to where that part will stand.
                //
                //The neighbour therefore keeps sharing: "neither radius exists
                //yet" is still literally true of a bend nobody could fit an arc
                //to, and a share is what this file has always done when it
                //cannot say what will stand somewhere.
                //
                //ASKING IT AS AN ELBOW would be the Law R1 answer and is NOT
                //done here, because the reach door would REFUSE the ones that
                //matter: a shallow corner in AluPex has no elbow Produkt
                //("made at 90 degrees only; this corner turns 1.2"), and a
                //refusal here loses the whole pipeline instead of one arc. That
                //is a design question - which part NDH stands on a sharp
                //vertex - and not a reservation this file can make.
                continue;
            }

            //An arc of radius R bulges R(1/cos(turn/2) - 1) inside its kink.
            double turn = TurnAt(vs, i);
            v.Radius = Math.Min(
                avail / Math.Tan(turn / 2.0),
                MaxTraceDeviation / (1.0 / Math.Cos(turn / 2.0) - 1.0));
            double sb = v.Radius * Math.Tan(turn / 2.0);
            back[i] += sb;
            forward[i] += sb;
            v.D0 -= sb;
            v.D1 += sb;
            v.RadiusDecided = true;
        }

        //nTaken: what the neighbour takes of this leg, toward this bend.
        static double Share(RouteVertex n, double nTaken, double leg) =>
            n.RadiusStillUnknown ? leg / 2.0 : Math.Max(0.0, leg - nTaken - LegSlack);
    }

    #region Junctions
    /// <summary>
    /// A junction's seat on the route, as centreline distances: its branch
    /// point, and the stretch [<paramref name="Lo"/>, <paramref name="Hi"/>] the
    /// connection standing there occupies.
    /// </summary>
    private readonly record struct Seat(double Site, double Lo, double Hi);

    /// <summary>
    /// Each junction's seat on the route: the stretch NDH's connection will
    /// occupy, read from the connection (<see cref="INdhJunctionStraight"/>) and
    /// laid about the branch point.
    /// <para>
    /// NOT THE LEGACY PART'S PORTS. The legacy block is not what will stand
    /// there - NDH's junction is, at NDH's own length - so the ports the old
    /// part was drawn with say nothing about the stretch the new one needs.
    /// Measured 2026-09-22: with the seat reduced to the legacy ports alone the
    /// run's remarks went from 12 to 32, which is how large the answer this now
    /// asks for is.
    /// </para>
    /// </summary>
    private static List<Seat> Seats(IReadOnlyList<NdhJunctionSeat> junctions, Polyline cl)
    {
        List<Seat> seats = new List<Seat>();
        foreach (NdhJunctionSeat j in junctions)
        {
            double site = DistAt(cl, j.Site);
            seats.Add(new Seat(site, site - j.Straight.Back, site + j.Straight.Forward));
        }
        return seats;
    }

    /// <summary>
    /// Opens a straight through every branch junction that sits inside a LEGACY
    /// arc, by splitting that arc into two arcs of the same radius with the
    /// chord across the junction's seat between them. NDH cannot weld a
    /// connection into a curve (issue #12), so without this the junction is
    /// refused PortOnArc and the branch is left hanging - three of them on the
    /// reference drawing (038 on 016 at R=250 m, 012 on 013 at R=25 m, 048 on
    /// 047 on an elastic bend ClearJunctions could not move).
    ///
    /// The chord is the only thing that changes: both arcs keep the legacy
    /// radius, the route's tangents either side are the legacy ones, and the
    /// straight stays within <see cref="MaxTraceDeviation"/> of the arc it
    /// replaces (a 1.4 m seat on a 25 m radius strays 10 mm). Where it would
    /// stray further, or where the arc has no room for an arc on both sides of
    /// the seat, the arc is left as the legacy drawing drew it and NDH refuses
    /// the junction loudly, exactly as it does today.
    /// </summary>
    private static void SplitArcsAtJunctions(
        List<RouteVertex> vs, IReadOnlyList<Seat> seats, Polyline cl, List<string> notes)
    {
        foreach ((double site, double lo, double hi) in seats)
        {
            //The branch point, which is where NDH will look for the port. A
            //seat merely REACHING into an arc is no reason to open one: the
            //port is then on the straight beside it and NDH takes it.
            int i = -1;
            for (int k = 1; k < vs.Count - 1; k++)
                if (vs[k].Kind == VertexKind.Fillet && vs[k].D0 < site && vs[k].D1 > site) { i = k; break; }
            if (i < 0) continue;

            RouteVertex v = vs[i];
            double zone = v.D1 - v.D0;
            if (zone <= MinSegmentLength) continue;

            //The straight MUST carry the whole seat: it is what the connection
            //standing there occupies, as NDH answers it, so there is no smaller
            //stretch it could make do with. An ARC is left on both sides: the
            //chord cannot start where the leg before it arrives, or the route
            //would kink there instead of curving.
            double margin = Math.Min(0.2, ArcSplitKeep / zone);
            double f0 = Math.Clamp((lo - v.D0) / zone, margin, 1.0 - margin);
            double f1 = Math.Clamp((hi - v.D0) / zone, margin, 1.0 - margin);
            if (f1 - f0 < 1e-9 ||
                v.D0 + f0 * zone > lo + VertexSnap ||
                v.D0 + f1 * zone < hi - VertexSnap)
            {
                notes.Add($"the branch junction at {lo:F2}-{hi:F2} m stands in the legacy arc at " +
                    $"{v.D0:F2}-{v.D1:F2} m and no straight can be opened in it");
                continue;
            }

            Vector2d u = (v.P - vs[i - 1].P).GetNormal();
            Vector2d w = (vs[i + 1].P - v.P).GetNormal();
            double sweep = Turn(u, w);
            if (Math.Abs(sweep) < StraightTurn) continue;

            //Straying from the arc by the sagitta of the chord it replaces.
            double stray = v.Radius * (1.0 - Math.Cos(Math.Abs(sweep) * (f1 - f0) / 2.0));
            if (stray > MaxTraceDeviation)
            {
                notes.Add($"the branch junction at {lo:F2}-{hi:F2} m stands in the legacy arc at " +
                    $"{v.D0:F2}-{v.D1:F2} m; a straight through it would stray {stray:F3} m from the trace");
                continue;
            }

            double setback = v.Radius * Math.Tan(Math.Abs(sweep) / 2.0);
            Point2d t0 = v.P - u * setback;
            //The centre is the arc's own: square to the arriving tangent, on the side it turns to.
            Vector2d n = new Vector2d(-u.Y, u.X) * Math.Sign(sweep);
            Point2d c = t0 + n * v.Radius;
            Point2d a = Rotate(t0, c, sweep * f0);
            Point2d b = Rotate(t0, c, sweep * f1);
            Vector2d chord = (b - a).GetNormal();

            if (!TryIntersect(t0, u, a, chord, out Point2d pi0)) continue;
            Point2d t1 = v.P + w * setback;
            if (!TryIntersect(a, chord, t1, w, out Point2d pi1)) continue;

            vs[i] = new RouteVertex
            { P = pi0, Kind = VertexKind.Fillet, Radius = v.Radius, D0 = v.D0, D1 = v.D0 };
            vs.Insert(i + 1, new RouteVertex
            { P = pi1, Kind = VertexKind.Fillet, Radius = v.Radius, D0 = v.D1, D1 = v.D1 });
            notes.Add($"the legacy arc at {v.D0:F2}-{v.D1:F2} m split into two arcs of R={v.Radius:F2} " +
                $"with a straight across the branch junction at {lo:F2}-{hi:F2} m " +
                $"({stray:F3} m from the trace)");
        }
    }

    /// <summary>
    /// How far an elastic bend at distance <paramref name="d"/> may reach before
    /// it runs into a junction's seat. A seat the bend stands inside does not
    /// limit it: <see cref="ClearJunctions"/> could not move that bend, and the
    /// route keeps the legacy drafting there rather than a sharp kink no part
    /// can make; NDH then refuses the junction, which the drafter sees marked.
    /// </summary>
    private static double JunctionRoom(double d, IReadOnlyList<Seat> seats)
    {
        double room = double.PositiveInfinity;
        foreach ((double _, double lo, double hi) in seats)
        {
            if (hi <= d) room = Math.Min(room, d - hi);
            else if (lo >= d) room = Math.Min(room, lo - d);
        }
        return room;
    }

    /// <summary>
    /// Moves every elastic bend that stands inside a junction's seat out of it,
    /// through the seat's nearer edge and on by THE BEND'S OWN SETBACK - the
    /// tangent length of the tightest bend that pipe allows, asked through
    /// <see cref="INdhPartStraight"/> - so its arc ends where the seat begins,
    /// along the straight the junction stands on: that straight is kept, the
    /// leg on the other side swings to the moved corner. Such a bend is a
    /// drafting kink where a pipe was joined to the part askew (live run
    /// 2026-09-19, F2: 024's tee on 036 had a 0.87 degree kink at its own port,
    /// and every tee NDH refused as PortOnArc had an elastic bend's arc sized
    /// straight through it).
    ///
    /// The move is made only where it costs nothing else: the other neighbour
    /// may not be a part of fixed angle or a change's straight vertex (its turn
    /// would change), the other leg must be at least twice the move, and the
    /// moved corner stays within half of <see cref="MaxTraceDeviation"/> of the
    /// trace (the arc SizeBends puts there takes the other half). The moved
    /// bend may not land in ANY junction's seat - two junctions close together
    /// would otherwise trade one bend between them (review of #319, I8): the
    /// nearer edge is tried first, then the farther one. Otherwise the bend
    /// stays, the report says why, and NDH refuses the junction loudly as it
    /// would have anyway - never a silent move into a worse place.
    /// </summary>
    private static void ClearJunctions(
        List<RouteVertex> vs, IReadOnlyList<(RouteVertex V, LegacyIdentitySpan Span)> bounds,
        INdhPartStraight straight, IReadOnlyList<Seat> seats, List<string> notes)
    {
        foreach ((double _, double lo, double hi) in seats)
        {
            string seat = $"{lo:F2}-{hi:F2} m";
            for (int i = 1; i < vs.Count - 1; i++)
            {
                RouteVertex v = vs[i];
                if (v.Kind != VertexKind.Bend || v.D0 <= lo || v.D0 >= hi) continue;

                //ASKED AT THE CORNER AS IT STANDS. The move swings the other
                //leg, so the moved corner turns a little differently; the
                //kinks this clears are fractions of a degree, and the arc
                //SizeBends later fits there is sized on the corner as moved.
                LegacyIdentitySpan pipe = PipesOn(vs, bounds)[i];
                double setback = straight.At(pipe, pipe, ToDeg(TurnAt(vs, i)), false).Back;
                bool nearerIsBack = v.D0 - lo <= hi - v.D0;
                BendMove near = MoveOutOfSeat(vs, i, lo, hi, setback, nearerIsBack, seats);
                BendMove move = near.Why == null
                    ? near
                    : MoveOutOfSeat(vs, i, lo, hi, setback, !nearerIsBack, seats);
                if (move.Why != null)
                {
                    //A bend that cannot be moved out can still be taken out:
                    //the kink is drafting noise (047's was 1.6 degrees), and
                    //the trace it strays from is the drafter's own hand at the
                    //very place the legacy part stood. Straight is what the
                    //junction needs, and the route must still hold the trace.
                    double stray = SegmentDistance(v.P, vs[i - 1].P, vs[i + 1].P);
                    if (stray <= MaxTraceDeviation)
                    {
                        notes.Add($"bend at {v.D0:F2} m could not be moved out of the branch junction at " +
                            $"{seat} ({near.Why}); its {ToDeg(TurnAt(vs, i)):F2}° kink is taken out instead, " +
                            $"{stray:F3} m from the trace, so the main runs straight through it");
                        vs.RemoveAt(i);
                        //The kink the corner held does not vanish: a change's
                        //straight vertex beside it was collinear with the leg
                        //that has just swung, and would now turn by the kink -
                        //a second fitting on a boundary, which NDH refuses
                        //(live run 2026-09-20: 047 lost its whole pipeline and
                        //its three branches to TwoFittingsAtOneVertex). It is
                        //slid back onto the line of its neighbours, which is
                        //what a straight vertex is allowed to do.
                        ReStraighten(vs, i - 1);
                        ReStraighten(vs, i);
                        i--;
                        continue;
                    }
                    notes.Add($"bend at {v.D0:F2} m stands in the branch junction at {seat} and stays: " +
                        $"{near.Why}; out the other side, {move.Why}");
                    continue;
                }

                notes.Add($"bend at {v.D0:F2} m moved {move.Distance:F2} m out of the branch junction at {seat}, " +
                    "so the main runs straight through it");
                v.P = move.To;
                v.D0 = v.D1 = move.ToD;
            }
        }
    }

    /// <summary>
    /// The pipe standing on each vertex, index-aligned with
    /// <paramref name="vs"/> AS IT IS NOW: read from where each identity was
    /// actually laid (<paramref name="bounds"/>), never from the spans' drafted
    /// distances. Asked afresh by whoever needs it, because vertices are still
    /// being removed while junctions are cleared.
    /// </summary>
    private static LegacyIdentitySpan[] PipesOn(
        List<RouteVertex> vs, IReadOnlyList<(RouteVertex V, LegacyIdentitySpan Span)> bounds)
    {
        Dictionary<RouteVertex, LegacyIdentitySpan> beginsHere = new();
        foreach ((RouteVertex bv, LegacyIdentitySpan bs) in bounds) beginsHere[bv] = bs;
        LegacyIdentitySpan[] pipeOn = new LegacyIdentitySpan[vs.Count];
        LegacyIdentitySpan carried = bounds[0].Span;
        for (int i = 0; i < vs.Count; i++)
        {
            if (beginsHere.TryGetValue(vs[i], out LegacyIdentitySpan begins)) carried = begins;
            pipeOn[i] = carried;
        }
        return pipeOn;
    }

    /// <summary>
    /// Puts the change's straight vertex at <paramref name="j"/> back on the
    /// line of its neighbours, so it still carries no turn. Does nothing to any
    /// other kind of vertex: a corner is meant to turn.
    /// </summary>
    private static void ReStraighten(List<RouteVertex> vs, int j)
    {
        if (j <= 0 || j >= vs.Count - 1 || vs[j].Kind != VertexKind.Straight) return;
        Point2d a = vs[j - 1].P, b = vs[j + 1].P;
        Vector2d ab = b - a;
        double len = ab.Length;
        if (len < MinSegmentLength) return;
        double t = ((vs[j].P - a).DotProduct(ab)) / (len * len);
        vs[j].P = a + ab * Math.Clamp(t, 0.0, 1.0);
    }

    /// <summary>A bend moved out of a seat: where to, or why it may not go (then the rest is meaningless).</summary>
    private readonly record struct BendMove(Point2d To, double ToD, double Distance, string? Why);

    /// <summary>
    /// Bend <paramref name="i"/> moved out of the seat [lo, hi] through its back
    /// (toward the route's start) or front edge and <paramref name="setback"/>
    /// on - its own arc's tangent length - along the straight on the junction's
    /// side of it.
    /// </summary>
    private static BendMove MoveOutOfSeat(
        List<RouteVertex> vs, int i, double lo, double hi, double setback, bool back,
        IReadOnlyList<Seat> seats)
    {
        RouteVertex v = vs[i];
        //The neighbour on the junction's side keeps its leg's line; the other
        //one's leg swings.
        RouteVertex keep = back ? vs[i + 1] : vs[i - 1];
        RouteVertex other = back ? vs[i - 1] : vs[i + 1];
        double s = (back ? v.D0 - lo : hi - v.D0) + setback;
        Point2d moved = v.P + (v.P - keep.P).GetNormal() * s;
        double movedD = back ? v.D0 - s : v.D0 + s;

        string? why =
            other.Kind == VertexKind.Straight || !double.IsNaN(other.NominalTurn)
                ? "its other neighbour may not turn"
            : s > v.P.GetDistanceTo(other.P) / 2.0 ? "its other leg is too short"
            //The kept straight runs on over the old corner, so the route
            //strays furthest at the moved corner, off the trace's other leg.
            : SegmentDistance(moved, v.P, other.P) > MaxTraceDeviation / 2.0
                ? "the route would stray too far from the trace"
            : seats.Any(x => movedD > x.Lo && movedD < x.Hi)
                ? "moved out, it would stand in another branch junction"
            : null;
        return new BendMove(moved, movedD, s, why);
    }
    #endregion

    /// <summary>Absolute turn of the route at interior vertex <paramref name="i"/>.</summary>
    private static double TurnAt(List<RouteVertex> vs, int i) =>
        Math.Abs(Turn((vs[i].P - vs[i - 1].P).GetNormal(), (vs[i + 1].P - vs[i].P).GetNormal()));

    private static bool TryIntersect(
        Point2d p0, Vector2d t0, Point2d p1, Vector2d t1, out Point2d x)
    {
        x = default;
        double det = t0.X * t1.Y - t0.Y * t1.X;
        if (Math.Abs(det) < 1e-12) return false;
        double rx = p1.X - p0.X, ry = p1.Y - p0.Y;
        double s = (rx * t1.Y - ry * t1.X) / det;
        x = new Point2d(p0.X + s * t0.X, p0.Y + s * t0.Y);
        return true;
    }

    private static double Turn(Vector2d a, Vector2d b) =>
        Math.Atan2(a.X * b.Y - a.Y * b.X, a.X * b.X + a.Y * b.Y);
    #endregion

    #region Boundaries
    /// <summary>
    /// The identity spans the pipeline is built with: neighbours of one
    /// identity merged, and every span holding no legacy pipe and no part of its
    /// own dropped - it is only the length of the parts around it. The pipe after it then starts
    /// where it started, and its change stands where the dropped span's did: at
    /// the first part met, which the new pipeline puts on the change's vertex
    /// and lays the rest of the chain downstream of.
    /// </summary>
    private static List<LegacyIdentitySpan> CleanSpans(
        IReadOnlyList<LegacyIdentitySpan> spans, List<string> notes)
    {
        List<LegacyIdentitySpan> result = Merge(spans);
        for (int i = result.FindIndex(s => !s.HoldsPipeOrPart); i >= 0 && result.Count > 1;
             i = result.FindIndex(s => !s.HoldsPipeOrPart))
        {
            LegacyIdentitySpan s = result[i];
            result.RemoveAt(i);
            if (i == 0)
            {
                result[0] = result[0] with { StartDist = s.StartDist, ChangeDist = s.StartDist };
                notes.Add($"{s.EndDist - s.StartDist:F2} m of {Describe(s)} at the pipeline start holds no pipe " +
                    "or part of its own, dropped");
            }
            else if (i < result.Count)
            {
                result[i] = result[i] with { StartDist = s.StartDist, ChangeDist = s.ChangeDist };
                notes.Add($"{s.EndDist - s.StartDist:F2} m of {Describe(s)} at {s.StartDist:F2} m holds no pipe " +
                    $"or part of its own: the change to {Describe(result[i])} stands at {s.ChangeDist:F2} m");
            }
            else
            {
                result[i - 1] = result[i - 1] with { EndDist = s.EndDist };
                notes.Add($"{s.EndDist - s.StartDist:F2} m of {Describe(s)} at the pipeline end holds no pipe " +
                    "or part of its own, dropped");
            }
            result = Merge(result);
        }
        return result;
    }

    private static List<LegacyIdentitySpan> Merge(IReadOnlyList<LegacyIdentitySpan> spans)
    {
        List<LegacyIdentitySpan> merged = new List<LegacyIdentitySpan>();
        foreach (LegacyIdentitySpan s in spans)
        {
            if (merged.Count > 0 && SameIdentity(merged[merged.Count - 1], s))
                merged[merged.Count - 1] = merged[merged.Count - 1] with
                { EndDist = s.EndDist, HoldsPipeOrPart = merged[merged.Count - 1].HoldsPipeOrPart || s.HoldsPipeOrPart };
            else merged.Add(s);
        }
        return merged;
    }

    /// <summary>
    /// A vertex per identity change, in route order: on the F-rør corner the
    /// change is served by, else on the straight at the centre of its legacy
    /// part, else moved clear of the legacy arc or corner it falls on. A change
    /// moved onto or past the one before it leaves that one no length; that one
    /// is dropped.
    /// </summary>
    private static List<(RouteVertex, LegacyIdentitySpan)> PlaceBoundaries(
        List<RouteVertex> vs,
        List<LegacyIdentitySpan> spans,
        List<PartStraight> parts,
        INdhPartStraight straight,
        Polyline centreline,
        IReadOnlyList<Seat> seats,
        List<string> notes)
    {
        //EVERY F CORNER IS MARKED BEFORE ANY CHANGE IS PLACED. A corner an F-rør
        //makes carries its change, and what that vertex takes is ONE answer
        //covering both - so a straight measured against it must already see the
        //change, even while the boundary being placed is still an earlier one.
        //THE CORNER ITSELF IS KEPT, NEVER ITS INDEX. `VertexOnStraight` INSERTS
        //into `vs` as boundaries are placed, so an index taken before the loop
        //names a different vertex afterwards - every insertion ahead of a corner
        //shifts it. An F corner is `FCorner` and the two removal paths take only
        //`Straight` vertices, so the object itself is stable for the whole call
        //while its position is not.
        RouteVertex?[] fCorners = new RouteVertex?[spans.Count];
        for (int w = 1; w < spans.Count; w++)
        {
            int at = FCornerFor(vs, spans[w].ChangeDist, spans[w - 1], spans[w]);
            if (at < 0) continue;
            fCorners[w] = vs[at];
            vs[at].Change = new IdentityChange(spans[w - 1], spans[w]);
        }

        List<(double D, RouteVertex V, LegacyIdentitySpan Span)> placed = new() { (0.0, vs[0], spans[0]) };
        for (int w = 1; w < spans.Count; w++)
        {
            LegacyIdentitySpan prev = spans[w - 1];
            LegacyIdentitySpan s = spans[w];
            string what = $"change to {Describe(s)} at {s.ChangeDist:F2} m";

            RouteVertex? v;
            double d;
            RouteVertex? fc = fCorners[w];
            if (fc != null)
            {
                v = fc;
                d = v.D0;
                if (Math.Abs(d - s.ChangeDist) > VertexSnap)
                    notes.Add($"{what} put on the F-rør corner at {d:F2} m");
            }
            else
            {
                d = StraightDistance(vs, s.ChangeDist, prev, s, parts[w], straight, seats, out string? why);
                if (why != null) notes.Add($"{what} {why}, moved to {d:F2} m");
                v = VertexOnStraight(vs, d, centreline);
                if (v == null)
                {
                    notes.Add($"{what} has no straight to stand on, dropped");
                    continue;
                }
            }

            if (d <= placed[0].D + VertexSnap)
            {
                //THE PIPELINE SIMPLY BEGINS AS `s`. Nothing is laid at a
                //change that starts the pipeline - the opening identity is
                //simply `s` - so whatever marking the vertex arrived with comes
                //off it, whichever vertex it is. The phantom this refactor
                //exists to kill is `SizeBends` taking a transition chain's
                //reach out of the first leg, and `EndOfStraight` holding its
                //minimum pipe, for parts nobody lays.
                //
                //It is done HERE and not in the sweep at the end of the loop,
                //because the sweep clears only what is not laid: this vertex
                //may well be, `vs[0]` always is, and neither would be touched.
                notes.Add($"{what} starts the pipeline");
                v.Change = null;
                placed[0] = (placed[0].D, placed[0].V, s);
                continue;
            }
            while (placed.Count > 1 && d <= placed[placed.Count - 1].D + VertexSnap)
            {
                (double pd, RouteVertex pv, LegacyIdentitySpan ps) = placed[placed.Count - 1];
                notes.Add($"{Describe(ps)} from {pd:F2} m has no length left, dropped");
                placed.RemoveAt(placed.Count - 1);
                if (pv.Kind == VertexKind.Straight && pv != v) vs.Remove(pv);
            }

            //AND THE MARKING NAMES WHAT SURVIVED. The drop loop above can remove
            //the very span this vertex was marked as changing FROM - two changes
            //landing on one vertex is exactly how that happens, and then `pv ==
            //v` deliberately keeps the vertex while its predecessor's entry
            //goes. Left alone, the vertex would ask NDH for a B->C transition
            //while the importer lays A->C: a reach read for parts that are not
            //there, which is the thing Law R1 exists to stop. Whatever `placed`
            //ends on IS the identity arriving here.
            //
            //THE POSITION, THOUGH, WAS ALREADY CHOSEN AGAINST THE DRAFTED
            //PREDECESSOR, wherever it was MEASURED onto a straight - an F corner
            //is not measured, it is the corner's own station. On that path
            //`parts[w]` and the `prev` handed to `StraightDistance` are the
            //draft's, and the drop cannot be known before `d` is, since the drop
            //test reads `d`. So with spans A, B, C where B collapses onto C, C
            //sits where the B->C chain wanted it while A->C is laid.
            //A single-pass limitation, not a second reading - both readings go
            //through the one door - and recorded as such in
            //`reach-and-radius.md <deferred>`.
            v.Change = new IdentityChange(placed[placed.Count - 1].Span, s);
            placed.Add((d, v, s));
        }

        //A drop can leave two neighbours with one identity.
        for (int i = placed.Count - 1; i > 0; i--)
        {
            if (!SameIdentity(placed[i - 1].Span, placed[i].Span)) continue;
            if (placed[i].V.Kind == VertexKind.Straight) vs.Remove(placed[i].V);
            placed.RemoveAt(i);
        }

        //AND THE PRE-MARKING IS UNDONE WHERE THE BOUNDARY DID NOT LAND. Every F
        //corner was marked up front so a straight measured against it would see
        //the change, but this loop can then drop that boundary three ways: it
        //starts the pipeline, it is pushed onto the one before it, or it is
        //deduplicated away. A Straight vertex is removed from the route when
        //that happens; an F corner is not - it is a real corner of the route -
        //so it would keep a marking for a change nobody lays, and both SizeBends
        //and EndOfStraight would then reserve that change's parts on it.
        HashSet<RouteVertex> laid = new(placed.Select(p => p.V));
        foreach (RouteVertex? fc in fCorners)
        {
            if (fc == null || laid.Contains(fc)) continue;
            fc.Change = null;
        }

        return placed.Select(p => (p.V, p.Span)).ToList();
    }

    /// <summary>
    /// The F-rør corner serving a Twin&lt;-&gt;Enkelt change at distance
    /// <paramref name="d"/>: a sharp corner by an F-model, within reach, turning
    /// exactly a right angle (snapped there by <see cref="SnapFittingAngles"/>),
    /// with nothing but the construction changing. -1 when there is none: an F
    /// corner drafted too far off square to snap has no F-rør, and the change
    /// moves off it like off any corner.
    /// </summary>
    private static int FCornerFor(
        List<RouteVertex> vs, double d, LegacyIdentitySpan prev, LegacyIdentitySpan next)
    {
        if (prev.System != next.System || prev.Dn != next.Dn) return -1;
        if (IsTwin(prev.Type) == IsTwin(next.Type)) return -1;

        for (int i = 1; i < vs.Count - 1; i++)
        {
            if (vs[i].Kind != VertexKind.FCorner || Math.Abs(vs[i].D0 - d) > FModelReach) continue;
            if (Math.Abs(TurnAt(vs, i) - Math.PI / 2.0) <= FCornerSquare) return i;
        }
        return -1;
    }

    /// <summary>
    /// Where on a straight a change at distance <paramref name="d"/> goes: its
    /// own position, unless that is inside a legacy arc or on a corner (moved
    /// onto a neighbouring straight, see <see cref="MoveClear"/>), or its
    /// <paramref name="parts"/> would reach past its straight - into a fillet, an
    /// elbow's leg, or into the setback of an elastic bend. Parts
    /// meeting an elbow touch it or leave a pipe the drawing can weld, never a
    /// sliver: a change drafted closer than that is put in contact. A straight
    /// too short for the parts keeps the change in its middle, and the new
    /// pipeline says so on the change. <paramref name="why"/> says why it moved;
    /// null if not.
    /// </summary>
    private static double StraightDistance(
        List<RouteVertex> vs, double d, LegacyIdentitySpan prev, LegacyIdentitySpan next,
        PartStraight parts, INdhPartStraight straight,
        IReadOnlyList<Seat> seats, out string? why)
    {
        why = null;
        int hit = vs.FindIndex(v => v.Turns && (v.Kind == VertexKind.Fillet
            ? d > v.D0 + VertexSnap && d < v.D1 - VertexSnap
            : Math.Abs(d - v.D0) <= VertexSnap));

        int leg;
        double target;
        if (hit >= 0)
        {
            (leg, target) = MoveClear(vs, hit, prev, next);
            why = vs[hit].Kind == VertexKind.Fillet ? "falls inside an arc" : "falls on a corner";
        }
        else
        {
            leg = Math.Max(0, Math.Min(vs.Count - 2, vs.FindLastIndex(v => v.D1 <= d + VertexSnap)));
            target = d;
        }

        StraightEnd from = EndOfStraight(vs, leg, prev, straight, elbow => elbow.Forward);
        StraightEnd to = EndOfStraight(vs, leg + 1, next, straight, elbow => elbow.Back);
        double lo = vs[leg].D1 + from.Taken + parts.Back, hi = vs[leg + 1].D0 - to.Taken - parts.Forward;
        bool tooShort = lo > hi;
        if (tooShort) lo = hi = (vs[leg].D1 + vs[leg + 1].D0) / 2.0;

        double clamped = Math.Max(lo, Math.Min(hi, target));
        if (why == null && Math.Abs(clamped - target) > VertexSnap)
        {
            //THE WHOLE SUM, not just one term of it. "Too little straight for
            //its parts (0.05 m back, 3.05 m on)" never said how much straight
            //there WAS, so a reader could not tell a change that misses by a
            //centimetre from one that misses by three metres - and the note ends
            //by asking the drafter to make room, which is not an instruction
            //until it says how much (live run 2026-09-22, pipeline 016).
            double span = vs[leg + 1].D0 - vs[leg].D1;
            double need = parts.Back + parts.Forward + from.Taken + to.Taken;
            why = $"has too little straight for its parts: the straight from " +
                  $"{vs[leg].D1:F2} to {vs[leg + 1].D0:F2} m is {span:F2} m and the parts " +
                  $"need {need:F2} m ({parts.Back:F2} back + {parts.Forward:F2} on, plus " +
                  $"{from.Taken:F2} and {to.Taken:F2} for what stands at either end)" +
                  (tooShort ? $", so it stands in the middle at {clamped:F2} m and NDH says so"
                            : "");
        }

        (double cleared, bool clear) = ClearOfSeats(clamped, parts, seats, lo, hi);
        if (why == null && !clear)
            why = "stands in a branch junction with no room on its side";
        else if (why == null && Math.Abs(cleared - clamped) > VertexSnap)
            why = "stands in a branch junction";
        clamped = cleared;

        double touching = clamped - lo < from.ContactWithin ? lo
            : hi - clamped < to.ContactWithin ? hi
            : clamped;
        if (why == null && Math.Abs(touching - clamped) > VertexSnap)
            why = "would leave too short a pipe to what stands at that vertex, " +
                  "put in contact with it";
        return touching;
    }

    /// <summary>
    /// A change moved out of every branch junction's seat it stands in. The
    /// junction's own fitting owns that stretch of the main, so a reducer or a
    /// materialeskift left inside it lands in the fitting's interior and NDH
    /// files a FootprintOverlap on the new pipeline (live run 2026-09-20: eight
    /// of the seventeen issues were exactly this, every one of them an AluPex
    /// Preskobling T-stykke with its reducer a few millimetres away).
    ///
    /// The change keeps the SIDE of the junction the legacy drawing drew it on.
    /// Moving it past the junction would change the pipe the junction itself is
    /// made in - a different Produkt at a different size - which is designing,
    /// not translating. Where its own side has no room, the change stays where
    /// it is and NDH says so loudly, exactly as it does today.
    /// <para>
    /// THE SEAT IS HELD EXACTLY, so its edge is judged by NDH's footprint law
    /// and not by drafting noise: a part always stands in a seat, so the change
    /// either TOUCHES it or leaves at least the minimum pipe. A change nearer
    /// than that - overlapping by a fraction of a millimetre, or leaving one -
    /// is put in contact. Live run 2026-09-22: once the tee held its seat, every
    /// AluPex Preskobling T-stykke drawn touching its reducer came back 0.5 mm
    /// inside it or 1 mm short of it, because this test forgave a millimetre
    /// that NDH does not.
    /// </para>
    /// Answers the station, and whether it is clear of every seat: a change
    /// left where it was drawn for want of room is NOT, and the caller says so.
    /// </summary>
    private static (double D, bool Clear) ClearOfSeats(
        double d, PartStraight parts, IReadOnlyList<Seat> seats, double lo, double hi)
    {
        //Seats can overlap; each move is re-tested against all of them, and the
        //pass count bounds the walk. A walk that finds no lawful place - off
        //its straight, or shuttled between two seats too close to hold it -
        //leaves the change where it was drawn, as the summary says.
        double drawn = d;
        for (int pass = 0; pass <= seats.Count; pass++)
        {
            int hit = -1;
            double moved = d;
            for (int i = 0; i < seats.Count; i++)
            {
                bool before = d <= seats[i].Site;
                double gap = before ? seats[i].Lo - (d + parts.Forward) : (d - parts.Back) - seats[i].Hi;
                if (gap >= parts.MinimumPipe) continue;
                double contact = before ? seats[i].Lo - parts.Forward : seats[i].Hi + parts.Back;
                //Already touching, exactly as this very move put it.
                if (contact == d) continue;
                hit = i;
                moved = contact;
                break;
            }
            if (hit < 0) return (d, true);
            if (moved < lo - VertexSnap || moved > hi + VertexSnap) return (drawn, false);
            d = moved;
        }
        return (drawn, false);
    }

    /// <summary>
    /// What the vertex at one end of a change's straight takes of it
    /// (<see cref="StraightEnd.Taken"/>), and how near the change's parts may come
    /// before they must touch it instead (<see cref="StraightEnd.ContactWithin"/>).
    /// </summary>
    private readonly record struct StraightEnd(double Taken, double ContactWithin);

    /// <summary>
    /// The end of a straight at vertex <paramref name="i"/>, for the
    /// <paramref name="pipe"/> running on it: what stands at that vertex,
    /// reaching toward the straight (<paramref name="legToward"/>), as NDH
    /// measures it.
    /// </summary>
    private static StraightEnd EndOfStraight(
        List<RouteVertex> vs, int i, LegacyIdentitySpan pipe, INdhPartStraight straight,
        Func<PartStraight, double> legToward)
    {
        //A vertex whose turn is already in the route has already ended the
        //straight where its own geometry does, and takes nothing MORE out of it
        //FOR ITS TURN - but it may still carry a CHANGE, and that it does take.
        //So the turn is dropped and the vertex is still asked, rather than the
        //whole question being skipped: everything is ONE question, and the
        //answer is NDH's - an elbow's leg where a part makes the turn, the
        //tangent setback of the tightest bend that pipe allows where the pipe
        //makes it, and the change standing there either way.
        //
        //This used to switch on the vertex kind and hand back a flat metre for
        //a bend - twenty-four times the truth on a shallow kink in flexible
        //pipe, and what refused pipeline 016.
        //
        //AN END VERTEX TAKES NOTHING, and that is NDH's answer and not a number
        //this file keeps. An end cap is AUTHORED on the pipeline
        //(`capAtStart` / `capAtEnd`), this import authors none, and the planner
        //lays a cap only where one was authored - so nothing stands at these
        //two vertices. The 0.5 m `EndRoom` that used to be reserved here was an
        //invention; asking every run end for its cap anyway was the opposite
        //invention, and refused every AluPex run in the drawing, because
        //'Endebund' is a steel Produkt.
        double turn = vs[i].TurnNotYetInRoute ? ToDeg(TurnAt(vs, i)) : 0.0;
        PartStraight taken = vs[i].TakenAt(straight, pipe, turn);
        //THE MINIMUM PIPE IS A DISTANCE TO A PART, so it is a contact rule only
        //where a PART stands. NDH answers `MinimumPipe` unconditionally - it is
        //the drawing's setting, not a fact about this vertex - so reading it at
        //every vertex would pull a change into contact with an occupant that is
        //not there.
        //
        //A REACH IS NOT THE TEST, and this is the one place the difference
        //shows: an elastically bent pipe reaches (its tangent setback is real)
        //but stands no part there - no weld, nothing to leave a spool of pipe
        //against - so a bend must not snap a change into contact. The test is
        //the two facts the vertex already states about itself: a part turns it,
        //or a change stands on it.
        //
        //`TurnedByAPart` IS a kind test, and so is `TurnNotYetInRoute`, which
        //this method reads at the top of its body. Both are named classifications
        //DECLARED ONCE on the vertex and read wherever the answer is wanted -
        //`TurnedByAPart` at three sites, each putting something different into
        //the ask or into a test - and neither is a per-kind behaviour table,
        //which is what `switches-are-a-smell` forbids. The smell is a switch
        //that picks what to DO; a predicate that names one property of a vertex
        //is how the kind stops being read anywhere else.
        //AND A CHANGE FROM AN IDENTITY TO ITSELF IS NOT ONE. While boundaries
        //are still being placed a vertex can hold A -> A for a moment - a
        //boundary was dropped onto it and the pipe either side is the same -
        //and the dedup pass only clears that at the end of the call. Nothing
        //stands on such a vertex; the reach already comes back nought, and the
        //contact rule must agree with the reach.
        bool aPartStands = vs[i].TurnedByAPart || vs[i].Change is { Stands: true };
        return new StraightEnd(legToward(taken), aPartStands ? taken.MinimumPipe : 0.0);
    }

    /// <summary>
    /// The leg and distance a change falling on the fitting of vertex
    /// <paramref name="i"/> moves to: the twin side of a Twin&lt;-&gt;Enkelt
    /// change, otherwise the longer neighbouring straight; at its end by the
    /// fitting, from where the change's parts push it along the straight
    /// (<see cref="StraightDistance"/>).
    /// </summary>
    private static (int Leg, double D) MoveClear(
        List<RouteVertex> vs, int i, LegacyIdentitySpan prev, LegacyIdentitySpan next)
    {
        double backLo = vs[i - 1].D1, backHi = vs[i].D0;
        double foreLo = vs[i].D1, foreHi = vs[i + 1].D0;

        bool back = IsTwin(prev.Type) != IsTwin(next.Type)
            ? IsTwin(prev.Type)
            : backHi - backLo > foreHi - foreLo;

        return back ? (i - 1, backHi) : (i, foreLo);
    }

    /// <summary>
    /// The vertex a boundary at straight distance <paramref name="d"/> sits on:
    /// an existing straight-through vertex within <see cref="VertexSnap"/>, or a
    /// new straight-through vertex projected onto the leg so it adds no turn.
    /// Null when <paramref name="d"/> is at the route's far end.
    /// </summary>
    private static RouteVertex? VertexOnStraight(List<RouteVertex> vs, double d, Polyline cl)
    {
        foreach (RouteVertex v in vs)
            if (!v.Turns && Math.Abs(v.D0 - d) <= VertexSnap)
                return v == vs[vs.Count - 1] ? null : v;

        //The leg between the vertices whose geometry brackets d.
        int k = vs.FindLastIndex(x => x.D1 <= d + VertexSnap);
        if (k < 0) k = 0;
        if (k >= vs.Count - 1) return null;
        Point2d a = vs[k].P, b = vs[k + 1].P;

        Point3d on = cl.GetPointAtDist(Math.Max(0.0, Math.Min(d, cl.Length)));
        Vector2d ab = b - a;
        double t = ab.LengthSqrd < 1e-18
            ? 0.0
            : ((on.X - a.X) * ab.X + (on.Y - a.Y) * ab.Y) / ab.LengthSqrd;
        t = Math.Max(0.0, Math.Min(1.0, t));

        RouteVertex nv = new RouteVertex
        { P = a + ab * t, Kind = VertexKind.Straight, D0 = d, D1 = d };
        vs.Insert(k + 1, nv);
        return nv;
    }

    private static bool IsTwin(PipeTypeEnum type) => type == PipeTypeEnum.Twin;

    private static bool SameIdentity(LegacyIdentitySpan a, LegacyIdentitySpan b) => a.Identity == b.Identity;

    private static string Describe(LegacyIdentitySpan s) => $"{s.System} {s.Type} DN{s.Dn}";
    #endregion

    private static double ToRad(double deg) => deg * Math.PI / 180.0;

    private static double ToDeg(double rad) => rad * 180.0 / Math.PI;
}
