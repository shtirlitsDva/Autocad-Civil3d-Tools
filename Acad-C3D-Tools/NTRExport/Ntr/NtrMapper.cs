using NTRExport.Enums;
using NTRExport.Geometry;
using NTRExport.TopologyModel;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTRExport.Ntr
{
    internal class NtrMapper
    {
        public NtrGraph Map(Topology topo)
        {
            var g = new NtrGraph();

            foreach (var e in topo.Elements)
            {
                switch (e)
                {
                    case TPipe p:
                        g.Members.Add(new NtrPipe
                        {
                            A = p.A.Node.Pos,
                            B = p.B.Node.Pos,
                            Dn = p.Dn,
                            Material = p.Material,
                            Provenance = new[] { p.Source }
                        });
                        break;

                    case TFitting f when f.Kind == ElementKind.Bend:
                        var ends = f.Ports.Take(2).ToArray();
                        var a = ends[0].Node.Pos; var b = ends[1].Node.Pos;
                        var t = new Pt2((a.X + b.X) / 2, (a.Y + b.Y) / 2); // stub; replace with true elbow point
                        g.Members.Add(new NtrBend { A = a, B = b, T = t, Dn = InferMainDn(topo, f), Provenance = new[] { f.Source } });
                        break;

                    case TFitting f when f.Kind == ElementKind.Tee:
                        var mains = f.Ports.Where(x => x.Role == PortRole.Main).Take(2).ToArray();
                        var br = f.Ports.First(x => x.Role == PortRole.Branch);
                        g.Members.Add(new NtrTee
                        {
                            Ph1 = mains[0].Node.Pos,
                            Ph2 = mains[1].Node.Pos,
                            Pa1 = br.Node.Pos,
                            Pa2 = br.Node.Pos, // Pa2 resolved later if you want
                            Dn = InferMainDn(topo, f),
                            DnBranch = InferBranchDn(topo, f),
                            Provenance = [f.Source]
                        });
                        break;
                }
            }
            return g;
        }

        // DN inference stubs you’ll wire to your schedule/segment tree
        private static int InferMainDn(Topology t, TFitting f) => 200;
        private static int InferBranchDn(Topology t, TFitting f) => 100;
    }
}
