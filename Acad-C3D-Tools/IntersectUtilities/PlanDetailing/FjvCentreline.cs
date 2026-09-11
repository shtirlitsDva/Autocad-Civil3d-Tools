using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.DataManager.CsvData;
using IntersectUtilities.UtilsCommon.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

using static IntersectUtilities.PipeScheduleV2.PipeScheduleV2;
using static IntersectUtilities.UtilsCommon.Utils;

namespace IntersectUtilities.PlanDetailing;

/// <summary>
/// The outcome of <see cref="FjvCentreline.Build"/>. Every polyline is in memory
/// and not database resident; the caller owns and must add or dispose them.
/// </summary>
internal sealed class FjvCentrelineResult
{
    /// <summary>Chained runs that are not a Centreline themselves.</summary>
    public List<Polyline> RunLines { get; } = new List<Polyline>();
    public List<Polyline> Centrelines { get; } = new List<Polyline>();
    public int EdgeCount { get; set; }
    public int TwinRunCount { get; set; }
    public int FremRunCount { get; set; }
    public int ReturRunCount { get; set; }
    public int JoinCount { get; set; }
    public List<string> UnpairedRuns { get; } = new List<string>();
    public List<string> UntypedRuns { get; } = new List<string>();
    public List<string> MixedRuns { get; } = new List<string>();
    public List<string> UnjoinedTransitions { get; } = new List<string>();
    public List<string> FallbackBlocks { get; } = new List<string>();
    public List<string> UnresolvedBlocks { get; } = new List<string>();
}

/// <summary>
/// Builds the Centreline of every FJV pipeline, twin and bonded alike.
///
/// Pipes and the components on them are chained into runs, one per strand.
/// Connectivity comes from coincident pipe ends and MuffeIntern ports; the path
/// through a component is taken from the component's OWN centreline geometry,
/// so a curved fitting keeps its real arc.
///
/// A twin run IS its Centreline. A bonded pipeline's Centreline is the trimmed
/// bisector of its frem and retur runs. A transition (Y-rør, F-model, H-rør) is
/// the Composition boundary between the two: its inner geometry never carries a
/// run across, and the twin and bonded Centrelines are joined over it at the
/// intersection of their end tangents, so one polyline runs through.
/// </summary>
internal static class FjvCentreline
{
    //Connection tolerance. Measured on FJV-fremtid_2.26.1_fixture.dwg:
    //80% of pipe ends sit exactly on their MuffeIntern port and 96.5% within 1 um,
    //but the worst legitimate connection is ~14 mm off. Two ports on the same
    //component are never closer than ~100 mm, so 10 mm connects without merging.
    private const double Tol = 0.01;
    //Continuation threshold at nodes with 3+ edges: two edges are treated as
    //the same run only when they pass essentially straight through (bend
    //under ~37 degrees). Junction fittings route their main line straight
    //(a tee's through-edges are collinear); a sharper bend at a 3+ node
    //means a different logical run - e.g. the two single-pipe stubs of a
    //twin-transition fork, which a looser threshold would join into a
    //hairpin that then cannot pair with anything.
    private const double ContinuationDot = -0.8;
    //A frem point is only paired with a retur point closer than this. The real
    //centre-to-centre spacing is DN dependent (~0.5-1.5 m); this is only a sanity
    //cutoff so a run is never paired with an unrelated run somewhere else.
    private const double MaxPairSeparation = 5.0;
    //Arc-length spacing of the stations at which the bisector is traced. The
    //trace is refitted afterwards, so the step only bounds how much geometry
    //can hide between two stations - not the accuracy of the fitted result.
    private const double BisectorStep = 0.25;
    //Max deviation of the fitted line/arc centreline from the traced bisector.
    private const double BisectorFitTol = 0.001;
    //A centreline piece supported by only a couple of samples is a junction
    //wisp, not a corridor - two station steps is the shortest real piece.
    private const double MinPieceLength = 2.0 * BisectorStep;
    //How far a Centreline end may be extended to meet the other side of a
    //transition. The fixture's Y-rør and F-model joins reach 0.3-1.2 m; the
    //bound only keeps an unrelated end, or a corridor that broke off well
    //before the transition, from being joined.
    private const double MaxTransitionReach = 3.0;
    //End tangents closer to parallel than this are joined by a chord, not a miter.
    private static readonly double JoinParallelSin = Math.Sin(5.0 * Math.PI / 180.0);

    private static readonly HashSet<string> TransitionTypes =
        new HashSet<string> { "Y-Model", "F-Model", "H-Model" };

    public static FjvCentrelineResult Build(
        IReadOnlyCollection<Polyline> pipes,
        IReadOnlyCollection<BlockReference> components,
        FjvDynamicComponents fjv,
        Transaction tx)
    {
        FjvCentrelineResult result = new FjvCentrelineResult();
        List<Polyline> temps = new List<Polyline>();

        try
        {
            Graph graph = new Graph();

            #region Pipe edges
            foreach (Polyline pl in pipes)
            {
                PipeTypeEnum type = GetPipeType(pl);
                if (type is not (PipeTypeEnum.Twin or PipeTypeEnum.Frem or PipeTypeEnum.Retur))
                    throw new ArgumentException(
                        $"Pipe {pl.Handle} on layer {pl.Layer} is {type}, " +
                        "expected Twin, Frem or Retur.", nameof(pipes));

                if (pl.Length < Tol) continue;

                Polyline copy = (Polyline)pl.Clone();
                temps.Add(copy);
                graph.AddEdge(copy, type, pl.Handle.ToString());
            }
            #endregion

            #region Component edges from their internal centreline geometry
            List<Transition> transitions = new List<Transition>();

            foreach (BlockReference br in components)
            {
                List<Point3d> ports = br.GetAllEndPoints().ToList();

                if (TransitionTypes.Contains(ComponentSchedule.ReadComponentType(br, fjv)))
                {
                    //The Composition boundary: only its ports enter the graph, so
                    //no run is carried from twin into bonded through it.
                    transitions.Add(new Transition(br, ports.Select(graph.Node).ToList()));
                    continue;
                }

                //Endebund and friends: a single port just terminates a chain.
                if (ports.Count < 2) continue;

                #region Collect the block's internal curves in WCS
                BlockTableRecord btr = br.BlockTableRecord.Go<BlockTableRecord>(tx);
                List<Polyline> inner = new List<Polyline>();
                foreach (ObjectId oid in btr)
                {
                    Entity nested = oid.Go<Entity>(tx);
                    Polyline conv = CurveToPolyline(nested, br.BlockTransform);
                    if (conv == null) continue;
                    if (conv.Length < Tol) { conv.Dispose(); continue; }
                    inner.Add(conv);
                }
                #endregion

                #region Planarize: node set, then split curves at interior nodes
                List<Point3d> locPts = new List<Point3d>();
                Dictionary<(long, long), List<int>> locGrid = new();

                foreach (Point3d p in ports) GetOrAddNode(p, locPts, locGrid, Tol);
                foreach (Polyline c in inner)
                {
                    GetOrAddNode(c.StartPoint, locPts, locGrid, Tol);
                    GetOrAddNode(c.EndPoint, locPts, locGrid, Tol);
                }

                List<Polyline> segs = new List<Polyline>();
                foreach (Polyline c in inner)
                {
                    List<double> pars = new List<double>();
                    foreach (Point3d p in locPts)
                    {
                        Point3d cp = c.GetClosestPointTo(p, false);
                        if (cp.DistanceHorizontalTo(p) > Tol) continue;
                        double par = c.GetParameterAtPoint(cp);
                        if (par <= 1e-6 || par >= c.EndParam - 1e-6) continue;
                        pars.Add(par);
                    }

                    if (pars.Count == 0) { segs.Add(c); continue; }

                    pars.Sort();
                    DBObjectCollection split = c.GetSplitCurves(
                        new DoubleCollection(pars.ToArray()));
                    foreach (DBObject dbo in split)
                        if (dbo is Polyline sp) segs.Add(sp);
                    c.Dispose();
                }
                #endregion

                #region Shortest path from the first port to every other port
                List<(int A, int B, double W)> locEdges = new();
                List<Polyline> locGeom = new List<Polyline>();
                foreach (Polyline s in segs)
                {
                    int a = GetOrAddNode(s.StartPoint, locPts, locGrid, Tol);
                    int b = GetOrAddNode(s.EndPoint, locPts, locGrid, Tol);
                    if (a == b) continue; //closed symbol geometry, never a path
                    locEdges.Add((a, b, s.Length));
                    locGeom.Add(s);
                }

                HashSet<int> used = new HashSet<int>();
                bool anyPathFailed = false;
                int fromNode = GetOrAddNode(ports[0], locPts, locGrid, Tol);

                for (int i = 1; i < ports.Count; i++)
                {
                    int toNode = GetOrAddNode(ports[i], locPts, locGrid, Tol);
                    List<int> path = ShortestPathEdges(
                        fromNode, toNode, locPts.Count, locEdges);

                    if (path == null) { anyPathFailed = true; continue; }
                    foreach (int e in path) used.Add(e);
                }
                #endregion

                #region Emit the block's contribution
                string source = br.Handle.ToString();
                HashSet<Polyline> keep = new HashSet<Polyline>();
                foreach (int e in used)
                {
                    Polyline g = locGeom[e];
                    keep.Add(g);
                    temps.Add(g);
                    graph.AddEdge(g, PipeTypeEnum.Ukendt, source);
                }

                //Decoration and symbol geometry never lies on a port-to-port path.
                foreach (Polyline s in segs) if (!keep.Contains(s)) s.Dispose();

                if (anyPathFailed)
                {
                    //Fallback: chord through the insertion point. Exact for most
                    //two-port fittings, approximate for curved ones - reported.
                    result.FallbackBlocks.Add($"{br.RealName()} {br.Handle}");

                    for (int i = 1; i < ports.Count; i++)
                    {
                        Polyline chord = new Polyline();
                        chord.AddVertexAt(0, ports[0].To2d(), 0.0, 0.0, 0.0);
                        if (br.Position.DistanceHorizontalTo(ports[0]) > Tol &&
                            br.Position.DistanceHorizontalTo(ports[i]) > Tol)
                            chord.AddVertexAt(
                                chord.NumberOfVertices, br.Position.To2d(), 0.0, 0.0, 0.0);
                        chord.AddVertexAt(
                            chord.NumberOfVertices, ports[i].To2d(), 0.0, 0.0, 0.0);

                        if (chord.Length < Tol) { chord.Dispose(); continue; }

                        temps.Add(chord);
                        graph.AddEdge(chord, PipeTypeEnum.Ukendt, source);
                    }
                }

                if (used.Count == 0 && !anyPathFailed)
                    result.UnresolvedBlocks.Add($"{br.RealName()} {br.Handle}");
                #endregion
            }
            #endregion

            #region Drop connected parts that contain no pipe
            int[] parent = new int[graph.NodePts.Count];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;

            for (int e = 0; e < graph.Geom.Count; e++)
            {
                int ra = FindRoot(graph.A[e], parent);
                int rb = FindRoot(graph.B[e], parent);
                if (ra != rb) parent[ra] = rb;
            }

            HashSet<int> pipeRoots = new HashSet<int>();
            for (int e = 0; e < graph.Geom.Count; e++)
                if (graph.Type[e] != PipeTypeEnum.Ukendt)
                    pipeRoots.Add(FindRoot(graph.A[e], parent));

            List<int> liveEdges = new List<int>();
            for (int e = 0; e < graph.Geom.Count; e++)
                if (pipeRoots.Contains(FindRoot(graph.A[e], parent))) liveEdges.Add(e);

            result.EdgeCount = liveEdges.Count;
            #endregion

            #region Pair half-edges at every node
            //Half-edge he = edgeIndex * 2 + end (0 = A-end, 1 = B-end).
            Dictionary<int, List<int>> nodeHalfEdges = new Dictionary<int, List<int>>();
            foreach (int e in liveEdges)
            {
                if (!nodeHalfEdges.TryGetValue(graph.A[e], out var la))
                { la = new List<int>(); nodeHalfEdges[graph.A[e]] = la; }
                la.Add(e * 2);

                if (!nodeHalfEdges.TryGetValue(graph.B[e], out var lb))
                { lb = new List<int>(); nodeHalfEdges[graph.B[e]] = lb; }
                lb.Add(e * 2 + 1);
            }

            Dictionary<int, int> pairOf = new Dictionary<int, int>();
            foreach (var kvp in nodeHalfEdges)
            {
                List<int> hes = kvp.Value;
                if (hes.Count < 2) continue;

                if (hes.Count == 2)
                {
                    //A plain joint - always continue, even around a 90 degree elbow.
                    pairOf[hes[0]] = hes[1];
                    pairOf[hes[1]] = hes[0];
                    continue;
                }

                //Tee, Y or F-model: continue along the straightest pair, the rest branch off.
                List<(double Dot, int H1, int H2)> cands = new();
                for (int i = 0; i < hes.Count; i++)
                    for (int j = i + 1; j < hes.Count; j++)
                        cands.Add((
                            OutgoingDir(hes[i], graph.Geom).DotProduct(
                                OutgoingDir(hes[j], graph.Geom)),
                            hes[i], hes[j]));

                foreach (var c in cands.OrderBy(x => x.Dot))
                {
                    if (c.Dot > ContinuationDot) break;
                    if (pairOf.ContainsKey(c.H1) || pairOf.ContainsKey(c.H2)) continue;
                    pairOf[c.H1] = c.H2;
                    pairOf[c.H2] = c.H1;
                }
            }
            #endregion

            #region Trace chains into typed runs
            HashSet<int> visited = new HashSet<int>();
            List<List<int>> chains = new List<List<int>>();

            //Open chains first: start at every half-edge that has no continuation.
            foreach (int e in liveEdges)
            {
                for (int end = 0; end < 2; end++)
                {
                    int he = e * 2 + end;
                    if (pairOf.ContainsKey(he)) continue;
                    if (visited.Contains(e)) continue;
                    chains.Add(TraceChain(he, pairOf, visited));
                }
            }

            //Whatever is left is a closed loop.
            foreach (int e in liveEdges)
            {
                if (visited.Contains(e)) continue;
                chains.Add(TraceChain(e * 2, pairOf, visited));
            }

            List<Run> twinRuns = new List<Run>();
            List<Run> fremRuns = new List<Run>();
            List<Polyline> returRuns = new List<Polyline>();

            foreach (List<int> chain in chains)
            {
                if (chain.Count == 0) continue;

                Polyline outPl = ChainToPolyline(chain, graph);
                if (outPl.NumberOfVertices < 2 || outPl.Length < Tol)
                { outPl.Dispose(); continue; }

                int firstHe = chain[0];
                int lastHe = chain[chain.Count - 1];
                Run run = new Run(
                    outPl,
                    firstHe % 2 == 0 ? graph.A[firstHe / 2] : graph.B[firstHe / 2],
                    lastHe % 2 == 0 ? graph.B[lastHe / 2] : graph.A[lastHe / 2],
                    string.Join(", ", chain.Select(he => graph.Source[he / 2]).Distinct()));

                //A run is typed by the pipes it is built from; component edges
                //carry no type of their own.
                List<PipeTypeEnum> types = chain
                    .Select(he => graph.Type[he / 2])
                    .Where(t => t != PipeTypeEnum.Ukendt)
                    .Distinct().ToList();

                if (types.Count != 1)
                {
                    (types.Count == 0 ? result.UntypedRuns : result.MixedRuns)
                        .Add(run.Sources);
                    result.RunLines.Add(outPl);
                    continue;
                }

                switch (types[0])
                {
                    case PipeTypeEnum.Twin:
                        twinRuns.Add(run);
                        break;
                    case PipeTypeEnum.Frem:
                        fremRuns.Add(run);
                        result.RunLines.Add(outPl);
                        break;
                    case PipeTypeEnum.Retur:
                        returRuns.Add(outPl);
                        result.RunLines.Add(outPl);
                        break;
                }
            }

            result.TwinRunCount = twinRuns.Count;
            result.FremRunCount = fremRuns.Count;
            result.ReturRunCount = returRuns.Count;
            #endregion

            #region Centreline pieces and the nodes their ends stand on
            //The centreline is the locus of points equidistant from the frem and the
            //retur runs: every centreline point lies on the normal of frem at its
            //frem foot point, on the normal of retur at its retur foot point, and at
            //equal distance from both curves (the trimmed bisector, Elber & Kim,
            //Computer-Aided Design 30(14) 1998). Only the "corridor sheet" of the
            //bisector is wanted, so a foot-point pair is accepted only where the two
            //tangents are near-parallel - this rejects the sheets that bisect a run
            //against its perpendicular branch at a tee. The bisector of two line/arc
            //chains is algebraic but has no exact line/arc representation, so it is
            //traced numerically station by station (the distance solve is monotone,
            //so bisection is guaranteed) and refitted with lines and arcs to
            //sub-millimetre tolerance.
            List<Polyline> pieces = new List<Polyline>();
            //Graph node -> the piece ends that stand on it, per side of a transition.
            Dictionary<int, List<(int Piece, int End)>> twinEndsAt = new();
            Dictionary<int, List<(int Piece, int End)>> bondedEndsAt = new();

            foreach (Run t in twinRuns)
            {
                int idx = pieces.Count;
                pieces.Add(t.Pl);
                AddEndAt(twinEndsAt, t.StartNode, (idx, 0));
                AddEndAt(twinEndsAt, t.EndNode, (idx, 1));
            }

            List<RunGeom> returIdx = returRuns.Select(BuildRunGeom).ToList();

            foreach (Run f in fremRuns)
            {
                List<List<Point3d>> traced = TraceBisector(
                    f.Pl, returIdx, MaxPairSeparation, BisectorStep);

                int first = pieces.Count;
                foreach (List<Point3d> piece in traced)
                {
                    Polyline mid = FitPolyline(piece, BisectorFitTol);
                    if (mid == null) continue;
                    if (mid.NumberOfVertices < 2 || mid.Length < MinPieceLength)
                    { mid.Dispose(); continue; }
                    pieces.Add(mid);
                }

                if (pieces.Count == first) { result.UnpairedRuns.Add(f.Sources); continue; }

                //The trace runs from the frem start to the frem end, so the first
                //piece begins at the start node's side and the last ends at the
                //end node's side.
                AddEndAt(bondedEndsAt, f.StartNode, (first, 0));
                AddEndAt(bondedEndsAt, f.EndNode, (pieces.Count - 1, 1));
            }
            #endregion

            #region Join twin and bonded Centrelines across every transition
            Dictionary<(int Piece, int End), (int Piece, int End, Point3d? Corner)> link =
                new();

            foreach (Transition tr in transitions)
            {
                List<(int Piece, int End)> twinEnds = tr.PortNodes
                    .SelectMany(n => twinEndsAt.TryGetValue(n, out var l) ? l : new())
                    .Distinct().ToList();
                List<(int Piece, int End)> bondedEnds = tr.PortNodes
                    .SelectMany(n => bondedEndsAt.TryGetValue(n, out var l) ? l : new())
                    .Distinct().ToList();

                string id = $"{tr.Br.RealName()} {tr.Br.Handle}";
                if (twinEnds.Count != 1 || bondedEnds.Count != 1)
                {
                    result.UnjoinedTransitions.Add(
                        $"{id}: {twinEnds.Count} twin and {bondedEnds.Count} bonded " +
                        "Centreline end(s) at its ports, expected 1 and 1");
                    continue;
                }

                (int Piece, int End) tw = twinEnds[0];
                (int Piece, int End) bo = bondedEnds[0];
                (Point3d a, Vector3d ta) = EndFrame(pieces[tw.Piece], tw.End);
                (Point3d b, Vector3d tb) = EndFrame(pieces[bo.Piece], bo.End);

                if (!TryJoin(a, ta, b, tb, out Point3d? corner))
                {
                    result.UnjoinedTransitions.Add(
                        $"{id}: the twin and bonded Centreline ends do not meet " +
                        $"within {MaxTransitionReach} m");
                    continue;
                }

                link[tw] = (bo.Piece, bo.End, corner);
                link[bo] = (tw.Piece, tw.End, corner);
                result.JoinCount++;
            }

            result.Centrelines.AddRange(MergeLinkedPieces(pieces, link, temps));
            #endregion

            return result;
        }
        catch
        {
            foreach (Polyline p in result.RunLines.Concat(result.Centrelines)) p.Dispose();
            throw;
        }
        finally
        {
            foreach (Polyline p in temps) p.Dispose();
        }
    }

    /// <summary>
    /// The chain graph: nodes are welded within <see cref="Tol"/>, each edge is
    /// one pipe or one port-to-port path segment of a component.
    /// </summary>
    private sealed class Graph
    {
        public readonly List<Point3d> NodePts = new List<Point3d>();
        private readonly Dictionary<(long, long), List<int>> grid = new();
        public readonly List<Polyline> Geom = new List<Polyline>();
        public readonly List<int> A = new List<int>();
        public readonly List<int> B = new List<int>();
        //Ukendt for component edges.
        public readonly List<PipeTypeEnum> Type = new List<PipeTypeEnum>();
        //Handle of the pipe or block an edge came from, for reports.
        public readonly List<string> Source = new List<string>();

        public int Node(Point3d p) => GetOrAddNode(p, NodePts, grid, Tol);

        public void AddEdge(Polyline g, PipeTypeEnum type, string source)
        {
            Geom.Add(g);
            A.Add(Node(g.StartPoint));
            B.Add(Node(g.EndPoint));
            Type.Add(type);
            Source.Add(source);
        }
    }

    private sealed record Transition(BlockReference Br, List<int> PortNodes);

    private sealed record Run(Polyline Pl, int StartNode, int EndNode, string Sources);

    private static void AddEndAt(
        Dictionary<int, List<(int Piece, int End)>> map, int node, (int Piece, int End) end)
    {
        if (!map.TryGetValue(node, out var l)) { l = new(); map[node] = l; }
        l.Add(end);
    }

    /// <summary>
    /// Concatenates the half-edges of a chain into one polyline, each edge
    /// walked from the end it was entered at.
    /// </summary>
    private static Polyline ChainToPolyline(List<int> chain, Graph graph)
    {
        Polyline outPl = new Polyline();
        Point3d lastPt = Point3d.Origin;

        foreach (int he in chain)
        {
            Polyline g = graph.Geom[he / 2];
            bool forward = he % 2 == 0;

            int n = g.NumberOfVertices;
            for (int i = 0; i < n - 1; i++)
            {
                int vi = forward ? i : n - 1 - i;
                //Reversing a polyline flips which vertex owns the bulge
                //and its sign; take it from the vertex we are leaving.
                double bulge = forward
                    ? g.GetBulgeAt(vi)
                    : -g.GetBulgeAt(vi - 1);
                outPl.AddVertexAt(
                    outPl.NumberOfVertices, g.GetPoint2dAt(vi), bulge, 0.0, 0.0);
            }
            lastPt = forward ? g.EndPoint : g.StartPoint;
        }

        outPl.AddVertexAt(outPl.NumberOfVertices, lastPt.To2d(), 0.0, 0.0, 0.0);
        return outPl;
    }

    /// <summary>
    /// The end point of <paramref name="pl"/> at <paramref name="end"/>
    /// (0 = start, 1 = end) and the unit direction that continues past it.
    /// </summary>
    private static (Point3d P, Vector3d Dir) EndFrame(Polyline pl, int end)
    {
        return end == 1
            ? (pl.EndPoint, pl.GetFirstDerivative(pl.EndParam).GetNormal())
            : (pl.StartPoint, pl.GetFirstDerivative(pl.StartParam).Negate().GetNormal());
    }

    /// <summary>
    /// Joins two Centreline ends that face each other across a transition: the
    /// miter intersection of their continuing tangents when they cross (the F's
    /// corner), a plain chord when they are near-collinear (straight through a
    /// Y). Fails when neither lies ahead of both ends within
    /// <see cref="MaxTransitionReach"/>. <paramref name="corner"/> is null when no
    /// vertex is needed between the two ends.
    /// </summary>
    private static bool TryJoin(
        Point3d a, Vector3d ta, Point3d b, Vector3d tb, out Point3d? corner)
    {
        corner = null;
        double rx = b.X - a.X, ry = b.Y - a.Y;
        double det = ta.X * tb.Y - ta.Y * tb.X;

        if (Math.Abs(det) > JoinParallelSin)
        {
            //a + s*ta = b + u*tb
            double s = (rx * tb.Y - ry * tb.X) / det;
            double u = (rx * ta.Y - ry * ta.X) / det;
            if (s < -Tol || u < -Tol) return false;
            if (s > MaxTransitionReach || u > MaxTransitionReach) return false;
            if (s > Tol && u > Tol)
                corner = new Point3d(a.X + s * ta.X, a.Y + s * ta.Y, 0.0);
            return true;
        }

        //Near-parallel: the ends must face each other and the chord must run
        //along them, not step sideways to some neighbouring line.
        if (ta.DotProduct(tb) > 0.0) return false;
        double along = rx * ta.X + ry * ta.Y;
        double lateral = Math.Abs(rx * ta.Y - ry * ta.X);
        if (along < -Tol || along > MaxTransitionReach) return false;
        return lateral <= Math.Max(Tol, along * JoinParallelSin);
    }

    /// <summary>
    /// Walks every chain of linked pieces into one polyline. Pieces without a
    /// link are returned as they are; merged sources go to
    /// <paramref name="temps"/> for disposal.
    /// </summary>
    private static List<Polyline> MergeLinkedPieces(
        List<Polyline> pieces,
        Dictionary<(int Piece, int End), (int Piece, int End, Point3d? Corner)> link,
        List<Polyline> temps)
    {
        List<Polyline> output = new List<Polyline>();
        bool[] done = new bool[pieces.Count];

        //Open paths first, from a piece end that has no link; then closed rings.
        for (int pass = 0; pass < 2; pass++)
        {
            for (int i = 0; i < pieces.Count; i++)
            {
                if (done[i]) continue;

                bool startLinked = link.ContainsKey((i, 0));
                bool endLinked = link.ContainsKey((i, 1));
                if (!startLinked && !endLinked)
                {
                    output.Add(pieces[i]);
                    done[i] = true;
                    continue;
                }
                if (pass == 0 && startLinked && endLinked) continue;

                Polyline merged = new Polyline();
                int cur = i;
                int entry = startLinked ? 1 : 0;
                while (true)
                {
                    done[cur] = true;
                    temps.Add(pieces[cur]);
                    AppendPolyline(merged, pieces[cur], entry == 1);

                    if (!link.TryGetValue((cur, 1 - entry), out var next)) break;
                    if (done[next.Piece]) break;
                    if (next.Corner is Point3d c) AddVertex(merged, c.To2d(), 0.0);
                    cur = next.Piece;
                    entry = next.End;
                }
                output.Add(merged);
            }
        }

        return output;
    }

    /// <summary>
    /// Appends all vertices of <paramref name="src"/> to <paramref name="target"/>,
    /// reversed when asked. The last vertex gets no bulge: whatever follows is
    /// joined by a straight segment.
    /// </summary>
    private static void AppendPolyline(Polyline target, Polyline src, bool reversed)
    {
        int n = src.NumberOfVertices;
        for (int i = 0; i < n; i++)
        {
            int vi = reversed ? n - 1 - i : i;
            double bulge = i == n - 1
                ? 0.0
                : reversed ? -src.GetBulgeAt(vi - 1) : src.GetBulgeAt(vi);
            AddVertex(target, src.GetPoint2dAt(vi), bulge);
        }
    }

    /// <summary>
    /// Adds a vertex, or - when it coincides with the last one - only hands the
    /// last vertex the bulge of the segment that now starts there.
    /// </summary>
    private static void AddVertex(Polyline target, Point2d p, double bulge)
    {
        int n = target.NumberOfVertices;
        if (n > 0 && target.GetPoint2dAt(n - 1).GetDistanceTo(p) < 1e-9)
        {
            target.SetBulgeAt(n - 1, bulge);
            return;
        }
        target.AddVertexAt(n, p, bulge, 0.0, 0.0);
    }

    /// <summary>
    /// Returns the index of the node at <paramref name="p"/>, creating it when no
    /// existing node lies within <paramref name="tol"/>. Uses a hash grid sized to
    /// the tolerance, so lookup stays constant time as the node count grows.
    /// </summary>
    private static int GetOrAddNode(
        Point3d p,
        List<Point3d> nodePts,
        Dictionary<(long, long), List<int>> grid,
        double tol)
    {
        long cx = (long)Math.Floor(p.X / tol);
        long cy = (long)Math.Floor(p.Y / tol);

        for (long dx = -1; dx <= 1; dx++)
            for (long dy = -1; dy <= 1; dy++)
                if (grid.TryGetValue((cx + dx, cy + dy), out var bucket))
                    foreach (int idx in bucket)
                        if (nodePts[idx].DistanceHorizontalTo(p) <= tol) return idx;

        int newIdx = nodePts.Count;
        nodePts.Add(p);
        if (!grid.TryGetValue((cx, cy), out var own))
        { own = new List<int>(); grid[(cx, cy)] = own; }
        own.Add(newIdx);
        return newIdx;
    }

    /// <summary>
    /// Converts a Line, Arc or Polyline to an in-memory Polyline in WCS.
    /// Returns null for anything else (text, hatches, nested blocks).
    /// The caller owns and must dispose the result.
    /// </summary>
    private static Polyline CurveToPolyline(Entity ent, Matrix3d xform)
    {
        switch (ent)
        {
            case Line ln:
                {
                    using Line c = (Line)ln.Clone();
                    c.TransformBy(xform);
                    Polyline pl = new Polyline(2);
                    pl.AddVertexAt(0, c.StartPoint.To2d(), 0.0, 0.0, 0.0);
                    pl.AddVertexAt(1, c.EndPoint.To2d(), 0.0, 0.0, 0.0);
                    return pl;
                }
            case Arc ar:
                {
                    using Arc c = (Arc)ar.Clone();
                    c.TransformBy(xform);
                    //An arc always sweeps CCW about its own normal; a mirrored
                    //instance flips the normal, and with it the bulge sign.
                    double bulge = Math.Tan(c.TotalAngle / 4.0);
                    if (c.Normal.Z < 0.0) bulge = -bulge;
                    Polyline pl = new Polyline(2);
                    pl.AddVertexAt(0, c.StartPoint.To2d(), bulge, 0.0, 0.0);
                    pl.AddVertexAt(1, c.EndPoint.To2d(), 0.0, 0.0, 0.0);
                    return pl;
                }
            case Polyline plo:
                {
                    Polyline c = (Polyline)plo.Clone();
                    c.TransformBy(xform);
                    return c;
                }
            default:
                return null;
        }
    }

    /// <summary>
    /// Dijkstra over a small undirected edge list. Returns the edge indices of the
    /// shortest path, or null when the two nodes are not connected.
    /// </summary>
    private static List<int> ShortestPathEdges(
        int fromNode, int toNode, int nodeCount, List<(int A, int B, double W)> edges)
    {
        if (fromNode == toNode) return new List<int>();

        double[] dist = new double[nodeCount];
        int[] prevEdge = new int[nodeCount];
        bool[] done = new bool[nodeCount];
        for (int i = 0; i < nodeCount; i++)
        { dist[i] = double.MaxValue; prevEdge[i] = -1; }
        dist[fromNode] = 0.0;

        while (true)
        {
            int u = -1;
            double best = double.MaxValue;
            for (int i = 0; i < nodeCount; i++)
                if (!done[i] && dist[i] < best) { best = dist[i]; u = i; }

            if (u == -1 || u == toNode) break;
            done[u] = true;

            for (int e = 0; e < edges.Count; e++)
            {
                int v = -1;
                if (edges[e].A == u) v = edges[e].B;
                else if (edges[e].B == u) v = edges[e].A;
                if (v == -1 || done[v]) continue;

                double nd = dist[u] + edges[e].W;
                if (nd < dist[v]) { dist[v] = nd; prevEdge[v] = e; }
            }
        }

        if (dist[toNode] == double.MaxValue) return null;

        List<int> path = new List<int>();
        int cur = toNode;
        while (cur != fromNode)
        {
            int e = prevEdge[cur];
            if (e == -1) return null;
            path.Add(e);
            cur = edges[e].A == cur ? edges[e].B : edges[e].A;
        }
        path.Reverse();
        return path;
    }

    /// <summary>
    /// Unit direction pointing away from the node that half-edge
    /// <paramref name="he"/> is attached to.
    /// </summary>
    private static Vector3d OutgoingDir(int he, List<Polyline> edgeGeom)
    {
        Polyline g = edgeGeom[he / 2];
        Vector3d v = he % 2 == 0
            ? g.GetFirstDerivative(g.StartPoint)
            : g.GetFirstDerivative(g.EndPoint).Negate();
        return v.Length < Autodesk.AutoCAD.Geometry.Tolerance.Global.EqualPoint
            ? v
            : v.GetNormal();
    }

    /// <summary>
    /// Walks the paired half-edges forward from <paramref name="startHe"/> and
    /// returns the chain as half-edge indices, each entered at its listed end.
    /// </summary>
    private static List<int> TraceChain(
        int startHe, Dictionary<int, int> pairOf, HashSet<int> visited)
    {
        List<int> chain = new List<int>();
        int cur = startHe;

        while (true)
        {
            int e = cur / 2;
            if (visited.Contains(e)) break;
            visited.Add(e);
            chain.Add(cur);

            int exitHe = e * 2 + (1 - cur % 2);
            if (!pairOf.TryGetValue(exitHe, out int next)) break;
            if (visited.Contains(next / 2)) break;
            cur = next;
        }

        return chain;
    }

    /// <summary>Union-find root with path compression.</summary>
    private static int FindRoot(int i, int[] parent)
    {
        while (parent[i] != i)
        {
            parent[i] = parent[parent[i]];
            i = parent[i];
        }
        return i;
    }

    //Corridor-sheet condition: a foot-point pair is only accepted when the chord
    //from the query point to the retur foot deviates less than 35 degrees from
    //the station normal. 35 degrees passes the tangent mismatch drafting slop
    //and curvature produce, and rejects 45-degree-and-up branch sheets at tees.
    private const double ChordSkewSin = 0.57357643635104609;

    /// <summary>
    /// Traces the corridor sheet of the trimmed bisector between
    /// <paramref name="frem"/> and the retur runs. Stations are placed every
    /// <paramref name="step"/> of arc length plus at every vertex; a vertex
    /// with a real turn gets one station per one-sided tangent and the pair is
    /// joined by a miter intersection, the way offset corners join. At each
    /// station the equidistant radius r solves dist(fp + r*n, retur) = r, which
    /// is a guaranteed bisection root because g(r) = dist - r is monotone
    /// nonincreasing (the distance field has unit gradient). Stations without a
    /// valid correspondence, or whose foot moves to another retur run, split the
    /// trace into separate pieces.
    /// </summary>
    private static List<List<Point3d>> TraceBisector(
        Polyline frem,
        List<RunGeom> returRuns,
        double maxSep,
        double step)
    {
        //A vertex turning less than this is treated as smooth.
        const double minMiterTurn = 1e-3;

        double totalLen = frem.Length;

        #region Station distances: regular steps plus every vertex station
        List<double> dists = new List<double>();
        for (double d = 0.0; d < totalLen; d += step) dists.Add(d);
        dists.Add(totalLen);
        for (int i = 1; i < frem.NumberOfVertices - 1; i++)
            dists.Add(frem.GetDistanceAtParameter(i));
        dists.Sort();
        #endregion

        #region Stations: foot point, ray tangent, and the corner kind
        //T orients the station's ray (normal = rot90(T)) and its sheet test.
        //A vertex with a real turn gets TWO stations, one per one-sided
        //tangent (Kind 1 entering, 2 leaving) - the pair is joined by a miter
        //intersection when both solve, the way offset corners join. The
        //equidistant set itself bulges outward at a sharp double corner (the
        //diagonal is equidistant to the two corner POINTS at more than the
        //half-gap), which is not how a pipe pair's axis corners.
        List<(Point3d P, Vector3d T, int Kind)> stations =
            new List<(Point3d, Vector3d, int)>();
        double prevD = double.MinValue;
        foreach (double d in dists)
        {
            if (d - prevD < 1e-9) continue;
            prevD = d;

            double par = frem.GetParameterAtDistance(Math.Min(d, totalLen));
            Point3d fp = frem.GetPointAtParameter(par);

            bool interiorVertex =
                Math.Abs(par - Math.Round(par)) < 1e-6 &&
                par > 0.5 && par < frem.EndParam - 0.5;

            if (!interiorVertex)
            {
                Vector3d t = frem.GetFirstDerivative(par);
                if (t.Length < 1e-12) continue;
                stations.Add((fp, t.GetNormal(), 0));
                continue;
            }

            Vector3d tin = frem.GetFirstDerivative(
                Math.Max(par - 1e-6, 0.0)).GetNormal();
            Vector3d tout = frem.GetFirstDerivative(
                Math.Min(par + 1e-6, frem.EndParam)).GetNormal();
            double ang = Math.Atan2(
                tin.X * tout.Y - tin.Y * tout.X, tin.DotProduct(tout));
            if (Math.Abs(ang) < minMiterTurn)
            {
                stations.Add((fp, tin, 0));
                continue;
            }
            stations.Add((fp, tin, 1));
            stations.Add((fp, tout, 2));
        }
        #endregion

        #region Solve the equidistant radius at every station
        List<List<Point3d>> pieces = new List<List<Point3d>>();
        List<Point3d> cur = new List<Point3d>();
        //Consecutive bisector samples further apart than this belong to
        //different sheets - split rather than bridge.
        double jumpLimit = 5.0 * step;

        bool pendingMiter = false;
        int curRun = -1;
        Point3d miterP = Point3d.Origin;
        Vector3d miterT = Vector3d.XAxis;

        foreach ((Point3d fp, Vector3d T, int kind) in stations)
        {
            double d0 = ValidDist(fp, T, 0.0, returRuns, maxSep, out Point3d q0, out int q0Run);
            bool ok = d0 <= maxSep;
            Point3d bis = Point3d.Origin;

            if (ok)
            {
                //Normal pointing to the side the retur foot is on.
                double side = T.X * (q0.Y - fp.Y) - T.Y * (q0.X - fp.X);
                Vector3d n = new Vector3d(-T.Y, T.X, 0.0);
                if (side < 0.0) n = n.Negate();

                double lo = 0.0, hi = d0;
                double gHi = ValidDist(
                    fp + n * hi, T, 0.0, returRuns, maxSep, out _, out _) - hi;
                if (gHi > 0.0)
                {
                    hi = maxSep;
                    gHi = ValidDist(
                        fp + n * hi, T, 0.0, returRuns, maxSep, out _, out _) - hi;
                }

                if (gHi > 0.0) ok = false;
                else
                {
                    for (int it = 0; it < 24; it++)
                    {
                        double mid = (lo + hi) / 2.0;
                        double g = ValidDist(
                            fp + n * mid, T, 0.0, returRuns, maxSep, out _, out _) - mid;
                        if (g > 0.0) lo = mid; else hi = mid;
                    }
                    double r = (lo + hi) / 2.0;
                    bis = fp + n * r;
                    //The sheet filter can make the distance field jump; accept
                    //only a true root, not a discontinuity bisection homed on.
                    double res = ValidDist(
                        bis, T, 0.0, returRuns, maxSep, out _, out _) - r;
                    if (Math.Abs(res) > 1e-4) ok = false;
                }
            }

            if (!ok)
            {
                pendingMiter = false;
                if (cur.Count > 1) pieces.Add(cur);
                cur = new List<Point3d>();
                continue;
            }

            //A foot on another retur run belongs to another pair. At a run end
            //the partner's foot is an endpoint, which is never a foot, so the
            //nearest valid foot left can sit on a neighbouring pipeline's
            //retur: split rather than bridge. The stitch below rejoins a real
            //handover between two retur runs of one pair.
            if (cur.Count > 0 &&
                (q0Run != curRun ||
                 cur[cur.Count - 1].DistanceHorizontalTo(bis) > jumpLimit))
            {
                pendingMiter = false;
                if (cur.Count > 1) pieces.Add(cur);
                cur = new List<Point3d>();
            }

            curRun = q0Run;

            //Join the two one-sided corner points through their miter: the
            //intersection of the incoming mid-line (through the Kind-1 point
            //along its tangent) with the outgoing one, when it lies between
            //them and within a sane miter length.
            if (kind == 2 && pendingMiter)
            {
                //Lines miterP + t1*miterT and bis + s*T; the miter is valid
                //when it lies ahead of the incoming point (t1 > 0), behind
                //the outgoing one (s < 0), and within a sane miter length.
                double det = miterT.X * T.Y - miterT.Y * T.X;
                if (Math.Abs(det) > 1e-9)
                {
                    double rx = bis.X - miterP.X, ry = bis.Y - miterP.Y;
                    double t1 = (rx * T.Y - ry * T.X) / det;
                    double s = (rx * miterT.Y - ry * miterT.X) / det;
                    double span = 2.0 * miterP.DistanceHorizontalTo(bis);
                    if (t1 > 1e-9 && s < -1e-9 && t1 < span && -s < span)
                        cur.Add(new Point3d(
                            miterP.X + t1 * miterT.X,
                            miterP.Y + t1 * miterT.Y, 0.0));
                }
            }
            pendingMiter = kind == 1;
            if (pendingMiter) { miterP = bis; miterT = T; }

            cur.Add(bis);
        }
        if (cur.Count > 1) pieces.Add(cur);
        #endregion

        #region Drop wisps, then stitch pieces across small disruptions
        //Near a component joint or a spot where the pair swaps sides (the two
        //runs cross), correspondence degenerates for a metre or two and the
        //trace splits, sometimes leaving a stray far-sheet wisp between the
        //good pieces. Wisps go; consecutive pieces of this run separated by
        //less than half the pairing cutoff are rejoined - through the miter
        //intersection of their end tangents when the ends form a corner, as a
        //plain chord otherwise.
        double stitchLimit = maxSep / 2.0;
        double wispLimit = 2.0 * step;

        List<List<Point3d>> stitched = new List<List<Point3d>>();
        foreach (List<Point3d> piece in pieces)
        {
            if (piece[0].DistanceHorizontalTo(piece[piece.Count - 1]) < wispLimit)
                continue;
            if (stitched.Count == 0) { stitched.Add(piece); continue; }

            List<Point3d> prev = stitched[stitched.Count - 1];
            if (prev[prev.Count - 1].DistanceHorizontalTo(piece[0]) > stitchLimit)
            { stitched.Add(piece); continue; }

            Vector3d ta = (prev[prev.Count - 1] - prev[prev.Count - 2]).GetNormal();
            Vector3d tb = (piece[1] - piece[0]).GetNormal();

            //In a crossing region the two pieces overlap spatially; joining
            //them raw folds the polyline back on itself. Trim what lies
            //behind the seam on either side first.
            while (piece.Count > 2 &&
                (piece[0] - prev[prev.Count - 1]).DotProduct(ta) <= 1e-9)
                piece.RemoveAt(0);
            while (prev.Count > 2 &&
                (piece[0] - prev[prev.Count - 1]).DotProduct(tb) <= 1e-9)
                prev.RemoveAt(prev.Count - 1);

            Point3d aEnd = prev[prev.Count - 1];
            Point3d bStart = piece[0];
            double gap = aEnd.DistanceHorizontalTo(bStart);
            if (gap > stitchLimit) { stitched.Add(piece); continue; }

            //Recompute the seam tangents from the TRIMMED ends: a stray
            //far-sheet sample at a raw piece boundary would otherwise
            //corrupt them and veto a legitimate miter.
            ta = (prev[prev.Count - 1] - prev[prev.Count - 2]).GetNormal();
            tb = (piece[1] - piece[0]).GetNormal();

            //Miter only when the intersection stays near the seam - a miter
            //longer than twice the gap means near-parallel end tangents, and
            //a chord joins those better than a spike.
            double det = ta.X * tb.Y - ta.Y * tb.X;
            if (Math.Abs(det) > 1e-3)
            {
                double rx = bStart.X - aEnd.X, ry = bStart.Y - aEnd.Y;
                double t1 = (rx * tb.Y - ry * tb.X) / det;
                double s = (rx * ta.Y - ry * ta.X) / det;
                if (t1 > 1e-9 && s < -1e-9 &&
                    t1 < 2.0 * gap && -s < 2.0 * gap)
                    prev.Add(new Point3d(
                        aEnd.X + t1 * ta.X, aEnd.Y + t1 * ta.Y, 0.0));
            }
            prev.AddRange(piece);
        }
        #endregion

        #region Remove sub-resolution backtracks
        //Where the runs converge (twin-to-single reducers) or a sheet
        //transition crowds stations, consecutive samples can jitter side to
        //side. The trace advances one step per station, so a reversal of
        //more than 120 degrees whose legs are both shorter than the station
        //spacing cannot represent real geometry - it is sampling noise. The
        //120-degree bound keeps legitimate miter corners: a right-angle
        //elbow's miter turns exactly 90 degrees on legs of about the
        //half-gap, which can be shorter than the station spacing.
        double noiseLeg = 1.5 * step;
        foreach (List<Point3d> piece in stitched)
        {
            bool removed = true;
            while (removed && piece.Count > 2)
            {
                removed = false;
                for (int k = 1; k < piece.Count - 1; k++)
                {
                    Vector3d v1 = piece[k] - piece[k - 1];
                    Vector3d v2 = piece[k + 1] - piece[k];
                    if (v1.DotProduct(v2) < -0.5 * v1.Length * v2.Length &&
                        v1.Length < noiseLeg && v2.Length < noiseLeg)
                    { piece.RemoveAt(k); removed = true; break; }
                }
            }
        }
        #endregion

        return stitched;
    }

    /// <summary>
    /// Line/arc segments and interior vertices of one run, unpacked to plain
    /// doubles so the distance query in the bisector trace runs without any
    /// AutoCAD API calls or allocations.
    /// </summary>
    private sealed class RunGeom
    {
        public double MinX, MinY, MaxX, MaxY;
        //Per segment: type 0 = line (A + t*U, t in [0, Len]),
        //type 1 = arc (centre C, radius R, start angle Sa, signed sweep).
        public int[] SegType;
        public double[] Ax, Ay, Ux, Uy, Len;
        public double[] Cx, Cy, R, Sa, Sweep;
        //Interior vertices with their one-sided unit tangents and the signed
        //turn angle - the corner candidates.
        public double[] Vx, Vy, VinX, VinY, Turn;
    }

    /// <summary>Unpacks a run polyline into a <see cref="RunGeom"/>.</summary>
    private static RunGeom BuildRunGeom(Polyline r)
    {
        int nSeg = r.NumberOfVertices - 1;
        RunGeom g = new RunGeom
        {
            SegType = new int[nSeg],
            Ax = new double[nSeg], Ay = new double[nSeg],
            Ux = new double[nSeg], Uy = new double[nSeg], Len = new double[nSeg],
            Cx = new double[nSeg], Cy = new double[nSeg], R = new double[nSeg],
            Sa = new double[nSeg], Sweep = new double[nSeg],
        };

        Extents3d ext = r.GeometricExtents;
        g.MinX = ext.MinPoint.X; g.MinY = ext.MinPoint.Y;
        g.MaxX = ext.MaxPoint.X; g.MaxY = ext.MaxPoint.Y;

        for (int i = 0; i < nSeg; i++)
        {
            Point2d a = r.GetPoint2dAt(i);
            Point2d b = r.GetPoint2dAt(i + 1);
            double bulge = r.GetBulgeAt(i);
            double chord = a.GetDistanceTo(b);

            if (Math.Abs(bulge) < 1e-12 || chord < 1e-12)
            {
                g.SegType[i] = 0;
                g.Ax[i] = a.X; g.Ay[i] = a.Y;
                g.Len[i] = chord;
                if (chord > 1e-12)
                { g.Ux[i] = (b.X - a.X) / chord; g.Uy[i] = (b.Y - a.Y) / chord; }
                continue;
            }

            //Centre from the bulge: a positive bulge sweeps CCW, which puts
            //the centre on the LEFT of the chord at r*cos(theta/2) from the
            //chord midpoint (negative for a major arc, flipping the side).
            g.SegType[i] = 1;
            double theta = 4.0 * Math.Atan(bulge);
            double radius = chord / (2.0 * Math.Sin(Math.Abs(theta) / 2.0));
            double nx = -(b.Y - a.Y) / chord, ny = (b.X - a.X) / chord;
            double off = Math.Sign(bulge) * radius * Math.Cos(theta / 2.0);
            double cx = (a.X + b.X) / 2.0 + nx * off;
            double cy = (a.Y + b.Y) / 2.0 + ny * off;
            g.Cx[i] = cx; g.Cy[i] = cy; g.R[i] = radius;
            g.Sa[i] = Math.Atan2(a.Y - cy, a.X - cx);
            g.Sweep[i] = theta;
        }

        #region Interior vertices with one-sided tangents
        int nV = Math.Max(0, r.NumberOfVertices - 2);
        g.Vx = new double[nV]; g.Vy = new double[nV];
        g.VinX = new double[nV]; g.VinY = new double[nV];
        g.Turn = new double[nV];
        for (int j = 0; j < nV; j++)
        {
            Point2d v = r.GetPoint2dAt(j + 1);
            g.Vx[j] = v.X; g.Vy[j] = v.Y;
            Vector3d tin = r.GetFirstDerivative(j + 1 - 1e-6);
            Vector3d tout = r.GetFirstDerivative(j + 1 + 1e-6);
            if (tin.Length < 1e-12 || tout.Length < 1e-12) continue;
            tin = tin.GetNormal(); tout = tout.GetNormal();
            g.VinX[j] = tin.X; g.VinY[j] = tin.Y;
            g.Turn[j] = Math.Atan2(
                tin.X * tout.Y - tin.Y * tout.X, tin.DotProduct(tout));
        }
        #endregion

        return g;
    }

    /// <summary>
    /// Distance from <paramref name="p"/> to the nearest retur foot point that
    /// is a valid corridor correspondence. EVERY local foot candidate on every
    /// run is considered - each segment's perpendicular (or radial) interior
    /// foot and each interior vertex - because the globally closest point may
    /// be an invalid sheet while a slightly farther foot is the valid one. A
    /// smooth foot is valid when its chord is near-perpendicular to the
    /// station tangent; a vertex foot when its chord lies in the vertex's
    /// normal cone widened by the same slack. Run endpoints are never feet -
    /// correspondence simply ends there. Returns MaxValue when no valid foot
    /// exists within <paramref name="cutoff"/>. <paramref name="footRun"/> is
    /// the index of the run the foot lies on, -1 when there is none.
    /// </summary>
    private static double ValidDist(
        Point3d p,
        Vector3d coneTin,
        double coneTurn,
        List<RunGeom> returRuns,
        double cutoff,
        out Point3d foot,
        out int footRun)
    {
        const double eps = 1e-6;
        double slack = Math.Asin(ChordSkewSin);
        double best = double.MaxValue;
        foot = Point3d.Origin;
        footRun = -1;

        for (int ri = 0; ri < returRuns.Count; ri++)
        {
            RunGeom g = returRuns[ri];
            double lim = Math.Min(best, cutoff);
            double bx = Math.Max(Math.Max(g.MinX - p.X, p.X - g.MaxX), 0.0);
            double by = Math.Max(Math.Max(g.MinY - p.Y, p.Y - g.MaxY), 0.0);
            if (bx * bx + by * by >= lim * lim) continue;

            #region Segment interior feet
            for (int i = 0; i < g.SegType.Length; i++)
            {
                double fx, fy, d;
                if (g.SegType[i] == 0)
                {
                    if (g.Len[i] < 1e-12) continue;
                    double t = (p.X - g.Ax[i]) * g.Ux[i] + (p.Y - g.Ay[i]) * g.Uy[i];
                    if (t <= eps || t >= g.Len[i] - eps) continue;
                    fx = g.Ax[i] + t * g.Ux[i];
                    fy = g.Ay[i] + t * g.Uy[i];
                }
                else
                {
                    double vx = p.X - g.Cx[i], vy = p.Y - g.Cy[i];
                    double vLen = Math.Sqrt(vx * vx + vy * vy);
                    if (vLen < 1e-9) continue;
                    double ang = Math.Atan2(vy, vx);
                    double sw = g.Sweep[i];
                    double angEps = eps / g.R[i];
                    double along = sw > 0.0
                        ? NormalizeCcw(ang - g.Sa[i])
                        : NormalizeCcw(g.Sa[i] - ang);
                    if (along <= angEps || along >= Math.Abs(sw) - angEps) continue;
                    fx = g.Cx[i] + g.R[i] * vx / vLen;
                    fy = g.Cy[i] + g.R[i] * vy / vLen;
                }

                double ddx = fx - p.X, ddy = fy - p.Y;
                d = Math.Sqrt(ddx * ddx + ddy * ddy);
                if (d >= best || d > cutoff || d < 1e-9) continue;

                //The chord must lie in the STATION's cone: normal +- slack at
                //a smooth station, the corner's normal cone +- slack at one.
                if (!ChordInCone(
                    ddx / d, ddy / d, coneTin, coneTurn, slack)) continue;

                best = d;
                foot = new Point3d(fx, fy, 0.0);
                footRun = ri;
            }
            #endregion

            #region Interior vertex feet
            for (int j = 0; j < g.Vx.Length; j++)
            {
                double wx = p.X - g.Vx[j], wy = p.Y - g.Vy[j];
                double d = Math.Sqrt(wx * wx + wy * wy);
                if (d >= best || d > cutoff || d < 1e-9) continue;
                wx /= d; wy /= d;

                //Both cones must accept the chord: the station's (chord from
                //station side toward retur) and the retur corner's own normal
                //cone (chord from the corner toward the station side).
                if (!ChordInCone(-wx, -wy, coneTin, coneTurn, slack)) continue;
                if (!ChordInCone(
                    wx, wy,
                    new Vector3d(g.VinX[j], g.VinY[j], 0.0), g.Turn[j], slack))
                    continue;

                best = d;
                foot = new Point3d(g.Vx[j], g.Vy[j], 0.0);
                footRun = ri;
            }
            #endregion
        }
        return best;
    }

    /// <summary>
    /// True when the unit chord (cx, cy) lies within the normal cone spanned
    /// by rotating the normal of <paramref name="coneTin"/> through
    /// <paramref name="coneTurn"/>, widened by <paramref name="slack"/> on
    /// both edges. Either side of the curve is accepted - the cone is mirror
    /// symmetric. A zero turn degenerates to normal +- slack, the smooth case.
    /// </summary>
    private static bool ChordInCone(
        double cx, double cy, Vector3d coneTin, double coneTurn, double slack)
    {
        double sideSign = coneTin.X * cy - coneTin.Y * cx >= 0.0 ? 1.0 : -1.0;
        double nInX = -coneTin.Y * sideSign, nInY = coneTin.X * sideSign;
        double delta = Math.Atan2(nInX * cy - nInY * cx, nInX * cx + nInY * cy);
        return delta >= Math.Min(0.0, coneTurn) - slack &&
               delta <= Math.Max(0.0, coneTurn) + slack;
    }

    /// <summary>
    /// Fits one polyline of line and arc segments through the ordered samples,
    /// keeping every sample within <paramref name="tol"/> of its segment. At
    /// every start index the LONGEST primitive wins: the line reach and the arc
    /// reach are searched independently and the arc is taken only when it
    /// swallows clearly more samples - a line-first greedy would chop a
    /// large-radius arc into within-tolerance chords, and an arc-first one
    /// would round every sharp miter corner. Returns null for degenerate
    /// input. The caller owns the result.
    /// </summary>
    private static Polyline FitPolyline(List<Point3d> pts, double tol)
    {
        //Collapse near-duplicates: at a sheet transition two consecutive
        //stations can land within millimetres of each other with a tiny
        //backtrack between them, which would force an unmergeable kink.
        List<Point3d> p = new List<Point3d>();
        foreach (Point3d q in pts)
            if (p.Count == 0 || p[p.Count - 1].DistanceHorizontalTo(q) > 0.01)
                p.Add(q);
        if (p.Count < 2) return null;

        Polyline outPl = new Polyline();
        int i = 0;
        while (i < p.Count - 1)
        {
            int lineReach = MaxReach(p, i, tol, false, out _);
            int arcReach = MaxReach(p, i, tol, true, out double arcBulge);

            //An arc must prove itself: through any 3 points there is a circle,
            //so demand at least two interior samples AND a clear win over the
            //line before rounding anything.
            bool useArc = arcReach >= lineReach + 2 && arcReach - i >= 3;
            int reach = useArc ? arcReach : lineReach;
            double bulge = useArc ? arcBulge : 0.0;

            outPl.AddVertexAt(outPl.NumberOfVertices, p[i].To2d(), bulge, 0.0, 0.0);
            i = reach;
        }
        outPl.AddVertexAt(
            outPl.NumberOfVertices, p[p.Count - 1].To2d(), 0.0, 0.0, 0.0);
        return outPl;
    }

    /// <summary>
    /// Farthest index j &gt; i such that samples i..j fit a single primitive of
    /// the requested kind within <paramref name="tol"/> (exponential probe,
    /// then binary refine). Outputs the bulge of the winning fit.
    /// </summary>
    private static int MaxReach(
        List<Point3d> p, int i, double tol, bool arc, out double bulge)
    {
        int reach = i + 1;
        bulge = 0.0;

        int probe = 1;
        while (reach + probe <= p.Count - 1 &&
               PrimitiveFits(p, i, reach + probe, tol, arc, out double b1))
        { reach += probe; bulge = b1; probe *= 2; }

        int lo = reach, hi = Math.Min(reach + probe, p.Count - 1);
        while (hi - lo > 1)
        {
            int mid = (lo + hi) / 2;
            if (PrimitiveFits(p, i, mid, tol, arc, out double b2))
            { lo = mid; bulge = b2; }
            else hi = mid;
        }
        return lo;
    }

    /// <summary>
    /// True when samples i..j (inclusive) fit a single line (arc = false) or a
    /// single circular arc (arc = true) from p[i] to p[j] within
    /// <paramref name="tol"/>; outputs the bulge.
    /// </summary>
    private static bool PrimitiveFits(
        List<Point3d> p, int i, int j, double tol, bool arc, out double bulge)
    {
        bulge = 0.0;
        Point3d a = p[i], b = p[j];
        double abLen = a.DistanceHorizontalTo(b);
        if (abLen < 1e-9) return false;
        if (j - i == 1) return true;

        if (!arc)
        {
            double ux = (b.X - a.X) / abLen, uy = (b.Y - a.Y) / abLen;
            for (int k = i + 1; k < j; k++)
            {
                double vx = p[k].X - a.X, vy = p[k].Y - a.Y;
                double along = vx * ux + vy * uy;
                double off = Math.Abs(vx * uy - vy * ux);
                if (off > tol || along < -tol || along > abLen + tol) return false;
            }
            return true;
        }

        #region Arc through p[i], the middle sample, p[j]
        //Work relative to a: the circumcenter formula on absolute UTM-sized
        //coordinates cancels catastrophically for short spans (meters of
        //center error), while local coordinates keep full precision.
        Point3d m = p[(i + j) / 2];
        double mx = m.X - a.X, my = m.Y - a.Y;
        double bx = b.X - a.X, by = b.Y - a.Y;
        double det = 2.0 * (mx * by - my * bx);
        if (Math.Abs(det) < 1e-12) return false;
        double mm = mx * mx + my * my;
        double bb = bx * bx + by * by;
        double cx = a.X + (mm * by - bb * my) / det;
        double cy = a.Y + (bb * mx - mm * bx) / det;
        double radius = Math.Sqrt((a.X - cx) * (a.X - cx) + (a.Y - cy) * (a.Y - cy));

        double angA = Math.Atan2(a.Y - cy, a.X - cx);
        double angB = Math.Atan2(b.Y - cy, b.X - cx);
        double angM = Math.Atan2(m.Y - cy, m.X - cx);
        double sweepCcw = NormalizeCcw(angB - angA);
        bool ccw = NormalizeCcw(angM - angA) < sweepCcw;
        double sweep = ccw ? sweepCcw : sweepCcw - 2.0 * Math.PI;
        //The tracer never emits near-half-circle pieces; a fit that claims one
        //is a degenerate circumcircle, not the bisector.
        if (Math.Abs(sweep) > Math.PI * 1.5) return false;

        //Every sample on the circle, advancing monotonically along the sweep -
        //otherwise the circumcircle happens to pass through three points of a
        //shape that is not one arc.
        double prevT = 0.0;
        for (int k = i + 1; k < j; k++)
        {
            double rr = Math.Sqrt(
                (p[k].X - cx) * (p[k].X - cx) + (p[k].Y - cy) * (p[k].Y - cy));
            if (Math.Abs(rr - radius) > tol) return false;
            double ak = Math.Atan2(p[k].Y - cy, p[k].X - cx);
            double t = ccw
                ? NormalizeCcw(ak - angA) / Math.Abs(sweep)
                : NormalizeCcw(angA - ak) / Math.Abs(sweep);
            if (t < prevT - 1e-9 || t > 1.0 + 1e-9) return false;
            prevT = t;
        }

        bulge = Math.Tan(sweep / 4.0);
        return true;
        #endregion
    }

    /// <summary>Angle wrapped to [0, 2*pi).</summary>
    private static double NormalizeCcw(double ang)
    {
        while (ang < 0.0) ang += 2.0 * Math.PI;
        while (ang >= 2.0 * Math.PI) ang -= 2.0 * Math.PI;
        return ang;
    }
}
