using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using IntersectUtilities;
using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.Enums;

using NTRExport.Enums;

using System.Globalization;

using static IntersectUtilities.UtilsCommon.Utils;

namespace NTRExport.TopologyModel
{
    internal class TopologyBuilder
    {
        private readonly IReadOnlyList<Polyline> _pipes;
        private readonly IReadOnlyList<BlockReference> _fittings;

        private TopologyBuilder(IEnumerable<Polyline> pipes, IEnumerable<BlockReference> fittings)
        {
            _pipes = pipes as IReadOnlyList<Polyline> ?? pipes.ToList();
            _fittings = fittings as IReadOnlyList<BlockReference> ?? fittings.ToList();
        }

        public static Topology Build(IEnumerable<Polyline> pipes, IEnumerable<BlockReference> fittings)
            => new TopologyBuilder(pipes, fittings).BuildInternal();

        private Topology BuildInternal()
        {
            var g = new Topology();
            var nodeIndex = new List<TNode>();
            var mainPortNodes = new List<TNode>();

            static bool TryProjectOntoSegment(Point3d p, Point3d a, Point3d b, out double t, out Point3d foot)
            {
                var a2 = a.To2d();
                var b2 = b.To2d();
                var p2 = p.To2d();
                var ab = b2 - a2;
                var abLen2 = ab.DotProduct(ab);
                if (abLen2 < 1e-12)
                {
                    t = 0.0;
                    foot = a;
                    return false;
                }
                var ap = p2 - a2;
                t = ap.DotProduct(ab) / abLen2;
                var foot2 = new Point2d(a2.X + ab.X * t, a2.Y + ab.Y * t);
                foot = foot2.To3d();
                // Distance from P to segment line in XY
                var dist = p2.GetDistanceTo(foot2);
                return dist <= CadTolerance.Node;
            }

            static double OrientedAngle(Point2d center, Point2d from, Point2d to)
            {
                var vf = from - center;
                var vt = to - center;
                var dot = vf.DotProduct(vt);
                var cross = vf.X * vt.Y - vf.Y * vt.X;
                return Math.Atan2(cross, dot);
            }

            static Point3d TangentPointFor(Point2d center, Point2d s, Point2d e)
            {
                var rs = s - center;
                var re = e - center;
                var ts = new Vector2d(-rs.Y, rs.X);
                var te = new Vector2d(-re.Y, re.X);
                var denom = ts.X * te.Y - ts.Y * te.X;
                if (Math.Abs(denom) < 1e-9)
                {
                    return default;
                }
                var es = e - s;
                var l = (es.X * te.Y - es.Y * te.X) / denom;
                var inter = s + ts.MultiplyBy(l);
                return inter.To3d();
            }

            TNode NodeAt(Point3d p)
            {
                var n = nodeIndex.FirstOrDefault(x => x.Pos.DistanceTo(p) < CadTolerance.Node);
                if (n != null) return n;
                n = new TNode { Pos = p };
                g.Nodes.Add(n); nodeIndex.Add(n);
                return n;
            }

            foreach (var fitting in _fittings)
            {
                var ports = MuffeInternReader.ReadPorts(fitting);
                var tf = CreateFitting(fitting);
                foreach (var cadPort in ports)
                {
                    var node = NodeAt(cadPort.Position);
                    var port = new TPort(cadPort.Role, node, tf);
                    tf.AddPort(port);
                    if (cadPort.Role == PortRole.Main)
                        mainPortNodes.Add(node);
                }
                g.Elements.Add(tf);
            }

            foreach (var pl in _pipes)
            {
                var segs = GetSegments(pl).ToList();
                if (segs.Count == 0)
                {
                    prdDbg($"Encountered polyline with no segments: {pl.Handle}");
                    continue;
                }

                foreach (var s in segs)
                {
                    if (s is LineSegment2d ls)
                    {
                        var a = NodeAt(ls.StartPoint.To3d());
                        var b = NodeAt(ls.EndPoint.To3d());
                        var tp = new TPipe(
                            pl.Handle,
                            s,
                            self => new TPort(PortRole.Neutral, a, self),
                            self => new TPort(PortRole.Neutral, b, self));
                        g.Elements.Add(tp);
                    }
                    else if (s is CircularArc2d arc)
                    {
                        var a = NodeAt(s.StartPoint.To3d());
                        var b = NodeAt(s.EndPoint.To3d());
                        var c = arc.Center;
                        var total = OrientedAngle(c, arc.StartPoint, arc.EndPoint);

                        // Gather interior split nodes on this arc
                        var arcSplits = new List<(double param, TNode node, Point2d foot)>();
                        if (Math.Abs(total) > 1e-12)
                        {
                            foreach (var mn in mainPortNodes)
                            {
                                var p2 = mn.Pos.To2d();
                                var foot = arc.GetClosestPointTo(p2);
                                var dist = foot.Point.GetDistanceTo(p2);
                                if (dist > CadTolerance.Node) continue;
                                // Exclude endpoints
                                if (foot.Point.GetDistanceTo(arc.StartPoint) <= CadTolerance.Node) continue;
                                if (foot.Point.GetDistanceTo(arc.EndPoint) <= CadTolerance.Node) continue;

                                var part = OrientedAngle(c, arc.StartPoint, foot.Point);
                                // Inside arc interval in oriented sense
                                if (total > 0.0)
                                {
                                    if (part <= 1e-12 || part >= total - 1e-12) continue;
                                }
                                else
                                {
                                    if (part >= -1e-12 || part <= total + 1e-12) continue;
                                }

                                // Deduplicate by reference or very close param
                                if (!arcSplits.Any(x => ReferenceEquals(x.node, mn) || Math.Abs(x.param - part) < 1e-6))
                                {
                                    arcSplits.Add((part, mn, foot.Point));
                                }
                            }
                        }

                        if (arcSplits.Count == 0)
                        {
                            var elbow = new ElbowFormstykke(
                                pl.Handle,
                                NTRExport.Utils.Utils.GetTangentPoint(arc),
                                PipelineElementType.Kedelrørsbøjning);
                            elbow.AddPort(new TPort(PortRole.Neutral, a, elbow));
                            elbow.AddPort(new TPort(PortRole.Neutral, b, elbow));
                            g.Elements.Add(elbow);
                        }
                        else
                        {
                            // Sort splits along oriented arc
                            var ordered = total > 0.0
                                ? arcSplits.OrderBy(x => x.param).ToList()
                                : arcSplits.OrderByDescending(x => x.param).ToList();

                            // Build chain of nodes and corresponding feet
                            var chainNodes = new List<TNode>();
                            var chainFeet = new List<Point2d>();
                            chainNodes.Add(a);
                            chainFeet.Add(arc.StartPoint);
                            foreach (var spt in ordered)
                            {
                                // avoid duplicates
                                if (!ReferenceEquals(chainNodes.Last(), spt.node))
                                {
                                    chainNodes.Add(spt.node);
                                    chainFeet.Add(spt.foot);
                                }
                            }
                            if (!ReferenceEquals(chainNodes.Last(), b))
                            {
                                chainNodes.Add(b);
                                chainFeet.Add(arc.EndPoint);
                            }

                            // Emit sub-elbows for each sub-arc between adjacent nodes
                            for (int j = 0; j < chainNodes.Count - 1; j++)
                            {
                                var n0 = chainNodes[j];
                                var n1 = chainNodes[j + 1];
                                // skip degenerate
                                if (n0.Pos.DistanceTo(n1.Pos) < 1e-9) continue;

                                var s2 = chainFeet[j];
                                var e2 = chainFeet[j + 1];
                                var tpnt = TangentPointFor(c, s2, e2);

                                var sub = new ElbowFormstykke(
                                    pl.Handle,
                                    tpnt == default ? NTRExport.Utils.Utils.GetTangentPoint(arc) : tpnt,
                                    PipelineElementType.Kedelrørsbøjning);
                                sub.AddPort(new TPort(PortRole.Neutral, n0, sub));
                                sub.AddPort(new TPort(PortRole.Neutral, n1, sub));
                                g.Elements.Add(sub);
                            }
                        }
                    }
                }
            }

            // Generic mid-segment stitching:
            // If a fitting has a Main port that lies on a pipe interior (not at ends),
            // split that pipe at the port's node so the branch connects into the same network.
            {
                // Collect split points per pipe as (t along A->B, node)
                var splits = new Dictionary<TPipe, List<(double t, TNode node)>>();
                var pipesSnapshot = g.Pipes.ToList(); // avoid mutating during enumeration

                foreach (var fit in g.Fittings)
                {
                    foreach (var port in fit.Ports.Where(p => p.Role == PortRole.Main))
                    {
                        var p = port.Node.Pos;

                        foreach (var tp in pipesSnapshot)
                        {
                            var a = tp.A.Node.Pos;
                            var b = tp.B.Node.Pos;
                            if (!TryProjectOntoSegment(p, a, b, out var t, out var foot))
                                continue;

                            // Keep only true interior hits; skip near-end projections
                            if (t <= 1e-6 || t >= 1.0 - 1e-6)
                                continue;

                            // Use the existing fitting node position (authoritative) to ensure node sharing
                            var splitNode = NodeAt(p);

                            if (!splits.TryGetValue(tp, out var list))
                                splits[tp] = list = new();

                            // Deduplicate by reference (same node) or very close t
                            if (!list.Any(s => ReferenceEquals(s.node, splitNode) || Math.Abs(s.t - t) < 1e-6))
                                list.Add((t, splitNode));
                        }
                    }
                }

                if (splits.Count > 0)
                {
                    // Rebuild pipes with splits applied
                    var newElements = new List<ElementBase>();
                    foreach (var el in g.Elements)
                    {
                        if (el is not TPipe tp || !splits.TryGetValue(tp, out var cutList) || cutList.Count == 0)
                        {
                            newElements.Add(el);
                            continue;
                        }

                        var aNode = tp.A.Node;
                        var bNode = tp.B.Node;

                        var ordered = cutList
                            .OrderBy(x => x.t)
                            .Select(x => x.node)
                            .ToList();

                        // Build chain A -> splits... -> B
                        var chain = new List<TNode>();
                        chain.Add(aNode);
                        foreach (var n in ordered)
                        {
                            // avoid accidental duplicates
                            if (!ReferenceEquals(chain.Last(), n))
                                chain.Add(n);
                        }
                        if (!ReferenceEquals(chain.Last(), bNode))
                            chain.Add(bNode);

                        for (int j = 0; j < chain.Count - 1; j++)
                        {
                            var n0 = chain[j];
                            var n1 = chain[j + 1];
                            if (n0.Pos.DistanceTo(n1.Pos) < 1e-9) continue;

                            var seg = new LineSegment2d(n0.Pos.To2d(), n1.Pos.To2d());
                            var np = new TPipe(
                                tp.Source,
                                seg,
                                self => new TPort(PortRole.Neutral, n0, self),
                                self => new TPort(PortRole.Neutral, n1, self));
                            newElements.Add(np);
                        }
                    }
                    g.Elements.Clear();
                    g.Elements.AddRange(newElements);
                }
            }

            int i = 1;
            foreach (var n in g.Nodes) n.Name = $"N{i++:000}";
            return g;
        }

        private static IEnumerable<Curve2d> GetSegments(Polyline pl)
        {
            int n = pl.NumberOfVertices;
            for (int i = 0; i < n; i++)
            {
                switch (pl.GetSegmentType(i))
                {
                    case SegmentType.Line:
                        yield return pl.GetLineSegment2dAt(i);
                        break;
                    case SegmentType.Arc:
                        yield return pl.GetArcSegment2dAt(i);
                        break;
                }
            }
        }

        private static TFitting CreateFitting(BlockReference fitting)
        {
            var kind = fitting.GetPipelineType();
            return kind switch
            {
                PipelineElementType.Kedelrørsbøjning
                or PipelineElementType.Bøjning45gr
                or PipelineElementType.Bøjning30gr
                or PipelineElementType.Bøjning15gr
                    => new ElbowFormstykke(fitting.Handle, kind),

                PipelineElementType.Buerør
                    => new Bueror(fitting.Handle, kind),

                PipelineElementType.PræisoleretBøjning90gr
                or PipelineElementType.PræisoleretBøjning45gr
                or PipelineElementType.PræisoleretBøjningVariabel
                    => DeterminePreinsulatedElbowAngle(fitting, kind),

                PipelineElementType.Svejsetee
                or PipelineElementType.PreskoblingTee
                or PipelineElementType.Muffetee
                    => new TeeFormstykke(fitting.Handle, kind),

                PipelineElementType.AfgreningMedSpring
                    => new AfgreningMedSpring(fitting.Handle),

                PipelineElementType.AfgreningParallel
                    => new AfgreningParallel(fitting.Handle),

                PipelineElementType.LigeAfgrening
                    => new LigeAfgrening(fitting.Handle),

                PipelineElementType.Stikafgrening
                    => new Stikafgrening(fitting.Handle),

                PipelineElementType.Afgreningsstuds
                    => new AfgreningsStuds(fitting.Handle),

                PipelineElementType.F_Model
                    => new FModel(fitting.Handle),

                PipelineElementType.Y_Model
                    => new YModel(fitting.Handle),

                PipelineElementType.Engangsventil
                or PipelineElementType.PræisoleretVentil
                or PipelineElementType.PræventilMedUdluftning
                    => new Valve(fitting.Handle, kind),

                PipelineElementType.Reduktion
                    => new Reducer(fitting.Handle),

                PipelineElementType.Svanehals
                    => new Svanehals(fitting.Handle),

                PipelineElementType.Materialeskift
                    => new Materialeskift(fitting.Handle),

                PipelineElementType.Endebund
                    => new Endebund(fitting.Handle),

                PipelineElementType.Svejsning
                    => new GenericFitting(fitting.Handle, kind),

                _ => new GenericFitting(fitting.Handle, kind)
            };

            TFitting DeterminePreinsulatedElbowAngle(BlockReference fitting, PipelineElementType kind)
            {
                var angleDeg = Convert.ToDouble(
                    fitting.ReadDynamicCsvProperty(DynamicProperty.Vinkel),
                    CultureInfo.InvariantCulture);
                if (angleDeg >= 46.0)
                    return new PreinsulatedElbowAbove45deg(fitting.Handle, kind);
                else
                    return new PreinsulatedElbowAtOrBelow45deg(fitting.Handle, kind);
            }
        }

    }
}
