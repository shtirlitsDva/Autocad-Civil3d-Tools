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
            foreach (var f in _cad.Fittings)
            {
                var tf = new TFitting(f.Handle, f.Kind);
                foreach (var cp in f.GetPorts())
                    tf.AddPort(new TPort(cp.Role, NodeAt(cp.Position), tf));
                g.Elements.Add(tf);
            }

            // 3) Pipes snap to nearest existing nodes (ends) or create free-end nodes
            foreach (var p in _cad.Pipes)
            {
                var a = NodeAt(p.Start);
                var b = NodeAt(p.End);
                var tp = new TPipe(
                    p.Handle,
                    self => new TPort(PortRole.Neutral, a, self), // owner filled next line
                    self => new TPort(PortRole.Neutral, b, self))
                {
                    Dn = p.Dn, 
                    Material = p.Material 
                };                
                g.Elements.Add(tp);
            }

            // 4) Name nodes compactly for later mapping
            int i = 1; foreach (var n in g.Nodes) n.Name = $"N{i++:000}";
            return g;
        }

        private static double Dist(Pt2 a, Pt2 b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    }
}
