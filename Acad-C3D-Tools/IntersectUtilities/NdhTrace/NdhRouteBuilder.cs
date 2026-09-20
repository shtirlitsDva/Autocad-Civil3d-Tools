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

/// <summary>Asks the new pipeline how much straight its parts take.</summary>
internal interface INdhPartStraight
{
    /// <summary>The parts of a change from <paramref name="before"/> to <paramref name="after"/>.</summary>
    PartStraight OfChange(LegacyIdentitySpan before, LegacyIdentitySpan after);

    /// <summary>The elbow <paramref name="pipe"/> takes on a corner turning <paramref name="turnDegrees"/>.</summary>
    PartStraight OfElbow(LegacyIdentitySpan pipe, double turnDegrees);
}

/// <summary>
/// Where a legacy branch sits on the pipeline being routed: its place on the
/// centreline and where its part(s) meet the main (their main-run ports).
/// </summary>
internal sealed record NdhJunctionSeat(Point2d Site, IReadOnlyList<Point2d> MainPorts);

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
    private const double ElbowReach = 1.5;
    private const double FModelReach = 3.0;
    //The smallest standard elbow is 15 degrees; a kink much smaller than
    //that next to an elbow is not the elbow's.
    private static readonly double MinFittingTurn = ToRad(5.0);
    //The route must not fold back on itself: the pipeline cannot model it.
    private static readonly double MaxTurn = ToRad(179.0);
    //A boundary this close to a vertex is put on that vertex.
    private const double VertexSnap = 1e-3;
    //The parts of a change stay at least this far from the corner of an
    //elastic bend, so the bend - sized after the change is placed - keeps an
    //arc of its own instead of a sharp corner.
    private const double BendMargin = 1.0;
    //Every bend keeps this much straight between itself and a pipeline end.
    private const double EndRoom = 0.5;
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
    private const double ElbowLegLength = 5.0;
    //An arc split at a junction keeps at least this much arc on each side of
    //the straight: less is not an arc, it is drafting noise.
    private const double ArcSplitKeep = 0.15;
    //The straight opened through a junction reaches this far past the legacy
    //part's own span, so NDH's junction - not drawn to the legacy block's
    //length - has straight of its own either side of the port.
    private const double JunctionPortStraight = 0.25;
    //A junction's straight reaches this far past its legacy part's main-run
    //ports on either side, so the new pipeline's part - not drawn to the
    //legacy block's length - keeps a straight too, and the bends sized
    //against it keep an arc of their own.
    private const double JunctionClearance = 0.5;
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
        //The straight the parts of the change on this vertex take; none on a
        //vertex that changes nothing.
        public PartStraight Parts;
        public bool Turns => Kind is not (VertexKind.End or VertexKind.Straight);
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

        SnapFittingAngles(vs, route.Adjustments);
        List<LegacyIdentitySpan> identities = CleanSpans(trace.Spans, route.Adjustments);
        List<PartStraight> parts = identities
            .Select((s, i) => i == 0 ? new PartStraight() : straight.OfChange(identities[i - 1], s))
            .ToList();
        List<(double Lo, double Hi)> seats = Seats(junctions, centreline);
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
        ClearJunctions(vs, seats, route.Adjustments);
        SizeBends(vs, seats, route.Adjustments);

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
    /// <see cref="MaxTraceDeviation"/> of its kink and whose setback stays within
    /// its share of both legs: what a fillet's setback leaves of a leg, the
    /// leg less the straight a change's parts take toward a boundary, less
    /// <see cref="EndRoom"/> toward an end, half the leg toward any other corner.
    /// </summary>
    private static void SizeBends(
        List<RouteVertex> vs, IReadOnlyList<(double Lo, double Hi)> seats, List<string> notes)
    {
        double[] setback = new double[vs.Count];
        for (int i = 1; i < vs.Count - 1; i++)
            setback[i] = FilletSetback(vs, i);

        for (int i = 1; i < vs.Count - 1; i++)
        {
            RouteVertex v = vs[i];
            if (v.Kind != VertexKind.Bend) continue;

            double avail = Math.Min(
                Math.Min(
                    Share(vs[i - 1], setback[i - 1], v.P.GetDistanceTo(vs[i - 1].P), vs[i - 1].Parts.Forward),
                    Share(vs[i + 1], setback[i + 1], v.P.GetDistanceTo(vs[i + 1].P), vs[i + 1].Parts.Back)),
                JunctionRoom(v.D0, seats));
            if (avail <= MinSegmentLength)
            {
                notes.Add($"bend at {v.D0:F2} m has no leg room, left sharp");
                continue;
            }

            //An arc of radius R bulges R(1/cos(turn/2) - 1) inside its kink.
            double turn = TurnAt(vs, i);
            v.Radius = Math.Min(
                avail / Math.Tan(turn / 2.0),
                MaxTraceDeviation / (1.0 / Math.Cos(turn / 2.0) - 1.0));
            double sb = v.Radius * Math.Tan(turn / 2.0);
            setback[i] = sb;
            v.D0 -= sb;
            v.D1 += sb;
        }

        //partsToward: how far the parts of a change on n reach toward the bend.
        static double Share(RouteVertex n, double nSetback, double leg, double partsToward) => n.Kind switch
        {
            VertexKind.End => Math.Max(0.0, leg - EndRoom),
            VertexKind.Straight => Math.Max(0.0, leg - partsToward - LegSlack),
            VertexKind.Fillet => Math.Max(0.0, leg - nSetback - LegSlack),
            _ => leg / 2.0,
        };
    }

    #region Junctions
    /// <summary>
    /// Each junction's seat on the route, as centreline distances: from its
    /// part's main-run ports and its site, widened by <see cref="JunctionClearance"/>.
    /// </summary>
    private static List<(double Lo, double Hi)> Seats(IReadOnlyList<NdhJunctionSeat> junctions, Polyline cl)
    {
        List<(double Lo, double Hi)> seats = new List<(double Lo, double Hi)>();
        foreach (NdhJunctionSeat j in junctions)
        {
            List<double> ds = j.MainPorts.Append(j.Site).Select(p => DistAt(cl, p)).ToList();
            seats.Add((ds.Min() - JunctionClearance, ds.Max() + JunctionClearance));
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
        List<RouteVertex> vs, IReadOnlyList<(double Lo, double Hi)> seats, Polyline cl, List<string> notes)
    {
        foreach ((double lo, double hi) in seats)
        {
            //The legacy part's own site, which is where NDH will look for the
            //port. A seat merely REACHING into an arc is no reason to open one:
            //the port is then on the straight beside it and NDH takes it.
            double site = (lo + hi) / 2.0;
            int i = -1;
            for (int k = 1; k < vs.Count - 1; k++)
                if (vs[k].Kind == VertexKind.Fillet && vs[k].D0 < site && vs[k].D1 > site) { i = k; break; }
            if (i < 0) continue;

            RouteVertex v = vs[i];
            double zone = v.D1 - v.D0;
            if (zone <= MinSegmentLength) continue;

            //The straight WANTS the legacy part's whole span plus room either
            //side; it MUST at least carry the site and that room, wherever the
            //squared port lands on it. An ARC is left on both sides: the chord
            //cannot start where the leg before it arrives, or the route would
            //kink there instead of curving.
            double wantLo = lo + JunctionClearance - JunctionPortStraight;
            double wantHi = hi - JunctionClearance + JunctionPortStraight;
            double mustLo = site - JunctionPortStraight, mustHi = site + JunctionPortStraight;
            double margin = Math.Min(0.2, ArcSplitKeep / zone);
            double f0 = Math.Clamp((wantLo - v.D0) / zone, margin, 1.0 - margin);
            double f1 = Math.Clamp((wantHi - v.D0) / zone, margin, 1.0 - margin);
            if (f1 - f0 < 1e-9 ||
                v.D0 + f0 * zone > mustLo + VertexSnap ||
                v.D0 + f1 * zone < mustHi - VertexSnap)
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
    private static double JunctionRoom(double d, IReadOnlyList<(double Lo, double Hi)> seats)
    {
        double room = double.PositiveInfinity;
        foreach ((double lo, double hi) in seats)
        {
            if (hi <= d) room = Math.Min(room, d - hi);
            else if (lo >= d) room = Math.Min(room, lo - d);
        }
        return room;
    }

    /// <summary>
    /// Moves every elastic bend that stands inside a junction's seat out of it,
    /// through the seat's nearer edge and <see cref="JunctionClearance"/> on,
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
        List<RouteVertex> vs, IReadOnlyList<(double Lo, double Hi)> seats, List<string> notes)
    {
        foreach ((double lo, double hi) in seats)
        {
            string seat = $"{lo + JunctionClearance:F2}-{hi - JunctionClearance:F2} m";
            for (int i = 1; i < vs.Count - 1; i++)
            {
                RouteVertex v = vs[i];
                if (v.Kind != VertexKind.Bend || v.D0 <= lo || v.D0 >= hi) continue;

                bool nearerIsBack = v.D0 - lo <= hi - v.D0;
                BendMove near = MoveOutOfSeat(vs, i, lo, hi, nearerIsBack, seats);
                BendMove move = near.Why == null ? near : MoveOutOfSeat(vs, i, lo, hi, !nearerIsBack, seats);
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
    /// (toward the route's start) or front edge and <see cref="JunctionClearance"/>
    /// on, along the straight on the junction's side of it.
    /// </summary>
    private static BendMove MoveOutOfSeat(
        List<RouteVertex> vs, int i, double lo, double hi, bool back, IReadOnlyList<(double Lo, double Hi)> seats)
    {
        RouteVertex v = vs[i];
        //The neighbour on the junction's side keeps its leg's line; the other
        //one's leg swings.
        RouteVertex keep = back ? vs[i + 1] : vs[i - 1];
        RouteVertex other = back ? vs[i - 1] : vs[i + 1];
        double s = (back ? v.D0 - lo : hi - v.D0) + JunctionClearance;
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
        IReadOnlyList<(double Lo, double Hi)> seats,
        List<string> notes)
    {
        List<(double D, RouteVertex V, LegacyIdentitySpan Span)> placed = new() { (0.0, vs[0], spans[0]) };
        for (int w = 1; w < spans.Count; w++)
        {
            LegacyIdentitySpan prev = spans[w - 1];
            LegacyIdentitySpan s = spans[w];
            string what = $"change to {Describe(s)} at {s.ChangeDist:F2} m";

            RouteVertex? v;
            double d;
            int fc = FCornerFor(vs, s.ChangeDist, prev, s);
            if (fc >= 0)
            {
                v = vs[fc];
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
                v.Parts = parts[w];
            }

            if (d <= placed[0].D + VertexSnap)
            {
                notes.Add($"{what} starts the pipeline");
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
            placed.Add((d, v, s));
        }

        //A drop can leave two neighbours with one identity.
        for (int i = placed.Count - 1; i > 0; i--)
        {
            if (!SameIdentity(placed[i - 1].Span, placed[i].Span)) continue;
            if (placed[i].V.Kind == VertexKind.Straight) vs.Remove(placed[i].V);
            placed.RemoveAt(i);
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
    /// elbow's leg, or within <see cref="BendMargin"/> of an elastic bend. Parts
    /// meeting an elbow touch it or leave a pipe the drawing can weld, never a
    /// sliver: a change drafted closer than that is put in contact. A straight
    /// too short for the parts keeps the change in its middle, and the new
    /// pipeline says so on the change. <paramref name="why"/> says why it moved;
    /// null if not.
    /// </summary>
    private static double StraightDistance(
        List<RouteVertex> vs, double d, LegacyIdentitySpan prev, LegacyIdentitySpan next,
        PartStraight parts, INdhPartStraight straight,
        IReadOnlyList<(double Lo, double Hi)> seats, out string? why)
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
        if (lo > hi) lo = hi = (vs[leg].D1 + vs[leg + 1].D0) / 2.0;

        double clamped = Math.Max(lo, Math.Min(hi, target));
        if (why == null && Math.Abs(clamped - target) > VertexSnap)
            why = $"has too little straight for its parts ({parts.Back:F2} m back, {parts.Forward:F2} m on)";

        double cleared = ClearOfSeats(clamped, parts, seats, lo, hi);
        if (why == null && Math.Abs(cleared - clamped) > VertexSnap)
            why = "stands in a branch junction";
        clamped = cleared;

        double touching = clamped - lo < from.ContactWithin ? lo
            : hi - clamped < to.ContactWithin ? hi
            : clamped;
        if (why == null && Math.Abs(touching - clamped) > VertexSnap)
            why = "would leave too short a pipe to its elbow, put in contact with it";
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
    /// </summary>
    private static double ClearOfSeats(
        double d, PartStraight parts, IReadOnlyList<(double Lo, double Hi)> seats, double lo, double hi)
    {
        //Seats can overlap; each move is re-tested against all of them, and the
        //pass count bounds the walk.
        for (int pass = 0; pass <= seats.Count; pass++)
        {
            int hit = -1;
            for (int i = 0; i < seats.Count; i++)
                if (d + parts.Forward > seats[i].Lo + VertexSnap &&
                    d - parts.Back < seats[i].Hi - VertexSnap) { hit = i; break; }
            if (hit < 0) return d;

            double moved = d <= (seats[hit].Lo + seats[hit].Hi) / 2.0
                ? seats[hit].Lo - parts.Forward
                : seats[hit].Hi + parts.Back;
            if (moved < lo - VertexSnap || moved > hi + VertexSnap) return d;
            d = moved;
        }
        return d;
    }

    /// <summary>
    /// What the vertex at one end of a change's straight takes of it
    /// (<see cref="StraightEnd.Taken"/>), and how near the change's parts may come
    /// before they must touch it instead (<see cref="StraightEnd.ContactWithin"/>).
    /// </summary>
    private readonly record struct StraightEnd(double Taken, double ContactWithin);

    /// <summary>
    /// The end of a straight at vertex <paramref name="i"/>, for the
    /// <paramref name="pipe"/> running on it: an elbow's leg toward the straight
    /// (<paramref name="legToward"/>), a bend's <see cref="BendMargin"/>, and
    /// nothing at an end, a fillet or another change.
    /// </summary>
    private static StraightEnd EndOfStraight(
        List<RouteVertex> vs, int i, LegacyIdentitySpan pipe, INdhPartStraight straight,
        Func<PartStraight, double> legToward) => vs[i].Kind switch
    {
        VertexKind.Elbow => ElbowEnd(straight.OfElbow(pipe, ToDeg(TurnAt(vs, i))), legToward),
        VertexKind.Bend => new StraightEnd(BendMargin, 0.0),
        _ => new StraightEnd(0.0, 0.0),
    };

    private static StraightEnd ElbowEnd(PartStraight elbow, Func<PartStraight, double> legToward) =>
        new StraightEnd(legToward(elbow), elbow.MinimumPipe);

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
