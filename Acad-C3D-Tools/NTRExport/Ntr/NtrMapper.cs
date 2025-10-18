using IntersectUtilities.UtilsCommon.Enums;

using NTRExport.Enums;
using NTRExport.Geometry;
using NTRExport.TopologyModel;

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
                        var suffix = p.Variant.DnSuffix;
                        // If the pipe has cushion spans, split into children with soilC80; else default soil
                        if (p.CushionSpans.Count == 0)
                        {
                            g.Members.Add(new NtrPipe
                            {
                                A = p.A.Node.Pos,
                                B = p.B.Node.Pos,
                                Dn = p.Dn,
                                Material = p.Material,
                                DnSuffix = suffix,
                                Flow = MapFlow(p.Flow),
                                Provenance = [p.Source]
                            });
                        }
                        else
                        {
                            var cuts = new SortedSet<double> { 0.0, p.Length };
                            foreach (var (s0, s1) in p.CushionSpans) { cuts.Add(s0); cuts.Add(s1); }
                            var list = cuts.ToList();
                            for (int i = 0; i < list.Count - 1; i++)
                            {
                                var a = list[i]; var b = list[i + 1]; if (b - a < 1e-6) continue;
                                var pa = Lerp(p.A.Node.Pos, p.B.Node.Pos, t: p.Length <= 1e-9 ? 0.0 : a / p.Length);
                                var pb = Lerp(p.A.Node.Pos, p.B.Node.Pos, t: p.Length <= 1e-9 ? 0.0 : b / p.Length);
                                var soil = Covered(p.CushionSpans, a, b) ? new NTRExport.SoilModel.SoilProfile("Soil_C80", 0.08) : NTRExport.SoilModel.SoilProfile.Default;
                                g.Members.Add(new NtrPipe { A = pa, B = pb, Dn = p.Dn, Material = p.Material, DnSuffix = suffix, Flow = MapFlow(p.Flow), Provenance = [p.Source], Soil = soil });
                            }
                        }
                        break;

                    // Bend-like elements → BOG
                    case TFitting f when f.Kind is PipelineElementType.Kedelrørsbøjning
                                              or PipelineElementType.PræisoleretBøjning90gr
                                              or PipelineElementType.Bøjning45gr
                                              or PipelineElementType.Bøjning30gr
                                              or PipelineElementType.Bøjning15gr
                                              or PipelineElementType.PræisoleretBøjningVariabel
                                              or PipelineElementType.Buerør:
                        {
                            var ends = f.Ports.Take(2).ToArray();
                            var a = ends[0].Node.Pos; var b = ends[1].Node.Pos;
                            var t = new Pt2((a.X + b.X) / 2, (a.Y + b.Y) / 2);
                            var near = topo.Elements.OfType<TPipe>().FirstOrDefault(p => p.A.Node == ends[0].Node || p.B.Node == ends[0].Node || p.A.Node == ends[1].Node || p.B.Node == ends[1].Node);
                            var suffixB = near?.Variant.DnSuffix ?? "s";
                            g.Members.Add(new NtrBend { A = a, B = b, T = t, Dn = InferMainDn(topo, f), Provenance = new[] { f.Source }, DnSuffix = suffixB, Flow = MapFlow(near?.Flow ?? TFlowRole.Unknown) });
                        }
                        break;

                    // Tee-like elements → TEE
                    case TFitting f when f.Kind is PipelineElementType.Svejsetee
                                              or PipelineElementType.PreskoblingTee
                                              or PipelineElementType.Muffetee
                                              or PipelineElementType.LigeAfgrening
                                              or PipelineElementType.AfgreningMedSpring
                                              or PipelineElementType.AfgreningParallel
                                              or PipelineElementType.Stikafgrening:
                        var mains = f.Ports.Where(x => x.Role == PortRole.Main).Take(2).ToArray();
                        var br = f.Ports.First(x => x.Role == PortRole.Branch);
                        var nearMain = topo.Elements.OfType<TPipe>().FirstOrDefault(p => p.A.Node == mains[0].Node || p.B.Node == mains[0].Node || p.A.Node == mains[1].Node || p.B.Node == mains[1].Node);
                        var nearBr = topo.Elements.OfType<TPipe>().FirstOrDefault(p => p.A.Node == br.Node || p.B.Node == br.Node);
                        var suffixMain = nearMain?.Variant.DnSuffix ?? "s";
                        var suffixBr = nearBr?.Variant.DnSuffix ?? suffixMain;
                        g.Members.Add(new NtrTee
                        {
                            Ph1 = mains[0].Node.Pos,
                            Ph2 = mains[1].Node.Pos,
                            Pa1 = br.Node.Pos,
                            Pa2 = br.Node.Pos,
                            Dn = InferMainDn(topo, f),
                            DnBranch = InferBranchDn(topo, f),
                            DnMainSuffix = suffixMain,
                            DnBranchSuffix = suffixBr,
                            Flow = MapFlow(nearMain?.Flow ?? TFlowRole.Unknown),
                            Provenance = [f.Source]
                        });
                        break;

                    // Reducer → RED
                    case TFitting f when f.Kind == PipelineElementType.Reduktion:
                        var pr = f.Ports.Take(2).ToArray();
                        var rnear1 = topo.Elements.OfType<TPipe>().FirstOrDefault(p => p.A.Node == pr[0].Node || p.B.Node == pr[0].Node);
                        var rnear2 = topo.Elements.OfType<TPipe>().FirstOrDefault(p => p.A.Node == pr[1].Node || p.B.Node == pr[1].Node);
                        g.Members.Add(new NtrReducer
                        {
                            P1 = pr[0].Node.Pos,
                            P2 = pr[1].Node.Pos,
                            Dn1 = InferDn1(topo, f),
                            Dn2 = InferDn2(topo, f),
                            Dn1Suffix = rnear1?.Variant.DnSuffix ?? "s",
                            Dn2Suffix = rnear2?.Variant.DnSuffix ?? (rnear1?.Variant.DnSuffix ?? "s"),
                            Flow = MapFlow((rnear1 ?? rnear2)?.Flow ?? TFlowRole.Unknown),
                            Material = null,
                            Provenance = [f.Source]
                        });
                        break;

                    // Valves → ARM
                    case TFitting f when f.Kind is PipelineElementType.Engangsventil
                                              or PipelineElementType.PræisoleretVentil
                                              or PipelineElementType.PræventilMedUdluftning:
                        var pv = f.Ports.Take(2).ToArray();
                        var p1 = pv[0].Node.Pos; var p2 = pv[1].Node.Pos;
                        var pm = new Pt2((p1.X + p2.X) / 2, (p1.Y + p2.Y) / 2);
                        var vnear1 = topo.Elements.OfType<TPipe>().FirstOrDefault(p => p.A.Node == pv[0].Node || p.B.Node == pv[0].Node);
                        var vnear2 = topo.Elements.OfType<TPipe>().FirstOrDefault(p => p.A.Node == pv[1].Node || p.B.Node == pv[1].Node);
                        g.Members.Add(new NtrInstrument
                        {
                            P1 = p1,
                            P2 = p2,
                            Pm = pm,
                            Dn1 = InferMainDn(topo, f),
                            Dn2 = InferMainDn(topo, f),
                            Flow = MapFlow((vnear1 ?? vnear2)?.Flow ?? TFlowRole.Unknown),
                            Dn1Suffix = vnear1?.Variant.DnSuffix ?? "s",
                            Dn2Suffix = vnear2?.Variant.DnSuffix ?? (vnear1?.Variant.DnSuffix ?? "s"),
                            Provenance = [f.Source]
                        });
                        break;

                    // Material change / Svanehals / F/Y models → RO stub or no-op
                    case TFitting f when f.Kind is PipelineElementType.Materialeskift
                                              or PipelineElementType.Svanehals:
                        var pp = f.Ports.Take(2).ToArray();
                        g.Members.Add(new NtrPipe
                        {
                            A = pp[0].Node.Pos,
                            B = pp[1].Node.Pos,
                            Dn = InferMainDn(topo, f),
                            Provenance = [f.Source]
                        });
                        break;

                    case TFitting f when f.Kind is PipelineElementType.F_Model
                                              or PipelineElementType.Y_Model:
                        g.Members.Add(new NtrStub { Provenance = [f.Source] });
                        break;

                    // Welding → ignore
                    case TFitting f when f.Kind == PipelineElementType.Svejsning:
                        break;
                }
            }
            return g;
        }

        private static Pt2 Lerp(Pt2 a, Pt2 b, double t) => new(a.X + t * (b.X - a.X), a.Y + t * (b.Y - a.Y));
        private static bool Covered(List<(double s0, double s1)> spans, double a, double b)
        {
            var mid = 0.5 * (a + b);
            return spans.Any(z => mid >= z.s0 - 1e-9 && mid <= z.s1 + 1e-9);
        }

        // DN inference stubs you’ll wire to your schedule/segment tree
        private static int InferMainDn(Topology t, TFitting f)
        {
            // Try: pick the max DN among connected pipes
            var dns = new List<int>();
            foreach (var n in f.Ports.Select(p => p.Node))
            {
                foreach (var e in t.Elements)
                {
                    if (e is TPipe p)
                    {
                        if (p.A.Node == n || p.B.Node == n) dns.Add(p.Dn);
                    }
                }
            }
            return dns.Count > 0 ? dns.Max() : 200;
        }
        private static int InferBranchDn(Topology t, TFitting f)
        {
            // Try: pick the min DN among connected pipes
            var dns = new List<int>();
            foreach (var n in f.Ports.Select(p => p.Node))
            {
                foreach (var e in t.Elements)
                {
                    if (e is TPipe p)
                    {
                        if (p.A.Node == n || p.B.Node == n) dns.Add(p.Dn);
                    }
                }
            }
            return dns.Count > 0 ? dns.Min() : 100;
        }
        private static int InferDn1(Topology t, TFitting f)
        {
            var dns = new List<int>();
            foreach (var n in f.Ports.Select(p => p.Node))
                foreach (var e in t.Elements)
                    if (e is TPipe p && (p.A.Node == n || p.B.Node == n)) dns.Add(p.Dn);
            return dns.Count > 0 ? dns.Max() : 200;
        }
        private static int InferDn2(Topology t, TFitting f)
        {
            var dns = new List<int>();
            foreach (var n in f.Ports.Select(p => p.Node))
                foreach (var e in t.Elements)
                    if (e is TPipe p && (p.A.Node == n || p.B.Node == n)) dns.Add(p.Dn);
            return dns.Count > 1 ? dns.Min() : 100;
        }

        private static FlowRole MapFlow(TFlowRole f) => f switch
        {
            TFlowRole.Supply => FlowRole.Supply,
            TFlowRole.Return => FlowRole.Return,
            _ => FlowRole.Unknown
        };
    }
}
