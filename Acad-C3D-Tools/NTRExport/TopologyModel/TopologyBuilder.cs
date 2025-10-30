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
                    tf.AddPort(new TPort(cadPort.Role, NodeAt(cadPort.Position), tf));
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
                        var elbow = new ElbowFormstykke(
                            pl.Handle,
                            NTRExport.Utils.Utils.GetTangentPoint(arc),
                            PipelineElementType.Kedelrørsbøjning);
                        elbow.AddPort(new TPort(PortRole.Neutral, a, elbow));
                        elbow.AddPort(new TPort(PortRole.Neutral, b, elbow));
                        g.Elements.Add(elbow);
                    }
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
