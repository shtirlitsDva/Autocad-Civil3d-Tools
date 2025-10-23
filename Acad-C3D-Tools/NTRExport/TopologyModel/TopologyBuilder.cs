using IntersectUtilities.UtilsCommon.Enums;

using NTRExport.CadExtraction;
using NTRExport.Enums;
using NTRExport.Geometry;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTRExport.TopologyModel
{
    internal class TopologyBuilder
    {
        private readonly CadModel _cad;
        public TopologyBuilder(CadModel cad) { _cad = cad; }
        
        public Topology Build()
        {
            var g = new Topology();
            // 1) Create nodes at every MuffeIntern port
            var nodeIndex = new List<TNode>();
            TNode NodeAt(Pt2 p)
            {
                var n = nodeIndex.FirstOrDefault(x => Dist(x.Pos, p) < Tolerance.Tol);
                if (n != null) return n;
                n = new TNode { Pos = p };
                g.Nodes.Add(n); nodeIndex.Add(n);
                return n;
            }

            // 2) Fittings with traversable ports
            foreach (var fitting in _cad.Fittings)
            {
                var ports = fitting.GetPorts();
                var tf = CreateFitting(fitting, ports);

                foreach (var cadPort in ports)
                {
                    tf.AddPort(new TPort(cadPort.Role, NodeAt(cadPort.Position), tf));
                }

                g.Elements.Add(tf);
            }

            // 3) Pipes: split polyline into line and arc segments
            foreach (var p in _cad.Pipes)
            {
                var segs = p.GetSegments().ToList();
                if (segs.Count == 0)
                {
                    var a = NodeAt(p.Start);
                    var b = NodeAt(p.End);
                    var tp = new TPipe(
                        p.Handle,
                        self => new TPort(PortRole.Neutral, a, self),
                        self => new TPort(PortRole.Neutral, b, self));
                    g.Elements.Add(tp);
                    continue;
                }

                foreach (var s in segs)
                {
                    if (s.Kind == CadExtraction.CadSegmentKind.Line)
                    {
                        var a = NodeAt(s.Start);
                        var b = NodeAt(s.End);
                        var tp = new TPipe(
                            p.Handle,
                            self => new TPort(PortRole.Neutral, a, self),
                            self => new TPort(PortRole.Neutral, b, self));
                        g.Elements.Add(tp);
                    }
                    else
                    {
                        var a = NodeAt(s.Start);
                        var b = NodeAt(s.End);
                        var elbow = new ElbowFormstykke(p.Handle, PipelineElementType.Kedelrørsbøjning, TangentFromArc(s));
                        elbow.AddPort(new TPort(PortRole.Main, a, elbow));
                        elbow.AddPort(new TPort(PortRole.Main, b, elbow));
                        g.Elements.Add(elbow);
                    }
                }
            }

            // 4) Name nodes compactly for later mapping
            int i = 1; foreach (var n in g.Nodes) n.Name = $"N{i++:000}";
            return g;
        }

        private static double Dist(Pt2 a, Pt2 b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

        private static TFitting CreateFitting(ICadFitting fitting, IReadOnlyList<CadPort> ports)
        {
            var tangent = EstimateTangent(ports);
            return fitting.Kind switch
            {
                PipelineElementType.Kedelrørsbøjning
                or PipelineElementType.Bøjning45gr
                or PipelineElementType.Bøjning30gr
                or PipelineElementType.Bøjning15gr
                    => new ElbowFormstykke(fitting.Handle, fitting.Kind, tangent),

                PipelineElementType.Buerør
                    => new Bueror(fitting.Handle, tangent),

                PipelineElementType.PræisoleretBøjning90gr
                or PipelineElementType.PræisoleretBøjning45gr
                or PipelineElementType.PræisoleretBøjningVariabel
                    => new PreinsulatedElbow(fitting.Handle, fitting.Kind, tangent),

                PipelineElementType.Svejsetee
                or PipelineElementType.PreskoblingTee
                or PipelineElementType.Muffetee
                    => new TeeFormstykke(fitting.Handle, fitting.Kind),

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
                    => new Valve(fitting.Handle, fitting.Kind),

                PipelineElementType.Reduktion
                    => new Reducer(fitting.Handle),

                PipelineElementType.Svanehals
                    => new Svanehals(fitting.Handle),

                PipelineElementType.Materialeskift
                    => new Materialeskift(fitting.Handle),

                PipelineElementType.Endebund
                    => new Endebund(fitting.Handle),

                PipelineElementType.Svejsning
                    => new GenericFitting(fitting.Handle, fitting.Kind),

                _ => new GenericFitting(fitting.Handle, fitting.Kind)
            };
        }

        private static Pt2 EstimateTangent(IReadOnlyList<CadPort> ports)
        {
            if (ports == null || ports.Count == 0)
            {
                return new Pt2(0.0, 0.0);
            }

            double sumX = 0.0;
            double sumY = 0.0;
            foreach (var port in ports)
            {
                sumX += port.Position.X;
                sumY += port.Position.Y;
            }

            var inv = 1.0 / ports.Count;
            return new Pt2(sumX * inv, sumY * inv);
        }

        private static Pt2 TangentFromArc(CadPipeSegment segment)
        {
            var center = segment.Center;
            var start = segment.Start;
            var end = segment.End;

            var radius = Dist(start, center);
            if (radius <= 1e-9)
            {
                return new Pt2((start.X + end.X) * 0.5, (start.Y + end.Y) * 0.5);
            }

            var startAngle = Math.Atan2(start.Y - center.Y, start.X - center.X);
            var endAngle = Math.Atan2(end.Y - center.Y, end.X - center.X);
            var delta = endAngle - startAngle;
            if (delta > Math.PI) delta -= 2 * Math.PI;
            if (delta < -Math.PI) delta += 2 * Math.PI;
            var midAngle = startAngle + delta / 2.0;

            return new Pt2(
                center.X + radius * Math.Cos(midAngle),
                center.Y + radius * Math.Sin(midAngle));
        }
    }
}
