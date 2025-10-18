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
        private static TFlowRole FlowFromType(PipeTypeEnum type)
        {
            return type switch
            {
                PipeTypeEnum.Frem => TFlowRole.Supply,
                PipeTypeEnum.Retur => TFlowRole.Return,
                _ => TFlowRole.Unknown
            };
        }
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
            foreach (var f in _cad.Fittings)
            {
                var tf = new TFitting(f.Handle, f.Kind);
                foreach (var cp in f.GetPorts())
                    tf.AddPort(new TPort(cp.Role, NodeAt(cp.Position), tf));
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
                        self => new TPort(PortRole.Neutral, b, self))
                    { Dn = p.Dn, Material = p.Material, Variant = (p.Type == PipeTypeEnum.Twin ? new TwinVariant() : new SingleVariant()), Flow = FlowFromType(p.Type) };
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
                            self => new TPort(PortRole.Neutral, b, self))
                        { Dn = p.Dn, Material = p.Material, Variant = (p.Type == PipeTypeEnum.Twin ? new TwinVariant() : new SingleVariant()), Flow = FlowFromType(p.Type) };
                        g.Elements.Add(tp);
                    }
                    else
                    {
                        var a = NodeAt(s.Start);
                        var b = NodeAt(s.End);
                        var tf = new TFitting(p.Handle, PipelineElementType.Kedelrørsbøjning);
                        tf.AddPort(new TPort(PortRole.Main, a, tf));
                        tf.AddPort(new TPort(PortRole.Main, b, tf));
                        g.Elements.Add(tf);
                    }
                }
            }

            // 4) Name nodes compactly for later mapping
            int i = 1; foreach (var n in g.Nodes) n.Name = $"N{i++:000}";
            return g;
        }

        private static double Dist(Pt2 a, Pt2 b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    }
}
