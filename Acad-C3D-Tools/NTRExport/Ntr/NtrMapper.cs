using IntersectUtilities.PipeScheduleV2;
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
                        {
                            var suffix = p.Variant.DnSuffix;
                            // If the pipe has cushion spans, split into children with soilC80; else default soil
                            // Twin emits upper (return) at Z=0 and lower (supply) at negative Z offset
                            var isTwin = p.Variant.IsTwin;
                            var zLower = 0.0;
                            if (isTwin)
                            {
                                // Compute Z offset in meters: (OD + gap) in mm → m
                                var odMm = PipeScheduleV2.GetPipeOd(p.System, p.Dn);
                                var gapMm = PipeScheduleV2.GetPipeDistanceForTwin(p.System, p.Dn, p.Type);
                                zLower = -Math.Max(0.0, (odMm + gapMm)) / 1000.0;
                            }

                            void EmitSegment(NtrGraph g0, Pt2 a0, Pt2 b0, double s0, double s1)
                            {
                                var soil = Covered(p.CushionSpans, s0, s1) ? new SoilModel.SoilProfile("Soil_C80", 0.08) : NTRExport.SoilModel.SoilProfile.Default;
                                // upper (return)
                                g0.Members.Add(new NtrPipe { A = a0, B = b0, Dn = p.Dn, Material = p.Material, DnSuffix = suffix, Flow = FlowRole.Return, ZOffsetMeters = 0.0, Provenance = [p.Source], Soil = soil });
                                if (isTwin)
                                {
                                    // lower (supply)
                                    g0.Members.Add(new NtrPipe { A = a0, B = b0, Dn = p.Dn, Material = p.Material, DnSuffix = suffix, Flow = FlowRole.Supply, ZOffsetMeters = zLower, Provenance = [p.Source], Soil = soil });
                                }
                            }

                            if (p.CushionSpans.Count == 0)
                            {
                                EmitSegment(g, p.A.Node.Pos, p.B.Node.Pos, 0.0, p.Length);
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
                                    EmitSegment(g, pa, pb, a, b);
                                }
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
                            var isTwin = near?.Variant.IsTwin ?? false;
                            var flowNear = MapFlow(near?.Flow ?? TFlowRole.Unknown);
                            var zLower = 0.0;
                            if (isTwin && near != null)
                            {
                                var odMm = IntersectUtilities.PipeScheduleV2.PipeScheduleV2.GetPipeOd(near.System, near.Dn);
                                var gapMm = IntersectUtilities.PipeScheduleV2.PipeScheduleV2.GetPipeDistanceForTwin(near.System, near.Dn, near.Type);
                                zLower = -Math.Max(0.0, (odMm + gapMm)) / 1000.0;
                            }
                            // upper (return)
                            g.Members.Add(new NtrBend { A = a, B = b, T = t, Dn = InferMainDn(topo, f), Provenance = new[] { f.Source }, DnSuffix = suffixB, Flow = FlowRole.Return, ZOffsetMeters = 0.0 });
                            if (isTwin)
                            {
                                // lower (supply)
                                g.Members.Add(new NtrBend { A = a, B = b, T = t, Dn = InferMainDn(topo, f), Provenance = new[] { f.Source }, DnSuffix = suffixB, Flow = FlowRole.Supply, ZOffsetMeters = zLower });
                            }
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
                        {
                            var mains = f.Ports.Where(x => x.Role == PortRole.Main).Take(2).ToArray();
                            var br = f.Ports.First(x => x.Role == PortRole.Branch);
                            var nearMain = topo.Elements.OfType<TPipe>().FirstOrDefault(p => p.A.Node == mains[0].Node || p.B.Node == mains[0].Node || p.A.Node == mains[1].Node || p.B.Node == mains[1].Node);
                            var nearBr = topo.Elements.OfType<TPipe>().FirstOrDefault(p => p.A.Node == br.Node || p.B.Node == br.Node);
                            var suffixMain = nearMain?.Variant.DnSuffix ?? "s";
                            var suffixBr = nearBr?.Variant.DnSuffix ?? suffixMain;
                            var isTwin = nearMain?.Variant.IsTwin ?? false;
                            var zLower = 0.0;
                            if (isTwin && nearMain != null)
                            {
                                var odMm = IntersectUtilities.PipeScheduleV2.PipeScheduleV2.GetPipeOd(nearMain.System, nearMain.Dn);
                                var gapMm = IntersectUtilities.PipeScheduleV2.PipeScheduleV2.GetPipeDistanceForTwin(nearMain.System, nearMain.Dn, nearMain.Type);
                                zLower = -Math.Max(0.0, (odMm + gapMm)) / 1000.0;
                            }
                            // upper (return)
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
                                Flow = FlowRole.Return,
                                ZOffsetMeters = 0.0,
                                Provenance = [f.Source]
                            });
                            if (isTwin)
                            {
                                // lower (supply)
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
                                    Flow = FlowRole.Supply,
                                    ZOffsetMeters = zLower,
                                    Provenance = [f.Source]
                                });
                            }
                        }
                        break;

                    // Reducer → RED
                    case TFitting f when f.Kind == PipelineElementType.Reduktion:
                        {
                            var pr = f.Ports.Take(2).ToArray();
                            var rnear1 = topo.Elements.OfType<TPipe>().FirstOrDefault(p => p.A.Node == pr[0].Node || p.B.Node == pr[0].Node);
                            var rnear2 = topo.Elements.OfType<TPipe>().FirstOrDefault(p => p.A.Node == pr[1].Node || p.B.Node == pr[1].Node);
                            var suffix1 = rnear1?.Variant.DnSuffix ?? "s";
                            var suffix2 = rnear2?.Variant.DnSuffix ?? suffix1;
                            var isTwin = (rnear1?.Variant.IsTwin ?? false) || (rnear2?.Variant.IsTwin ?? false);
                            var basis = rnear1 ?? rnear2;
                            var zLower = 0.0;
                            if (isTwin && basis != null)
                            {
                                var odMm = IntersectUtilities.PipeScheduleV2.PipeScheduleV2.GetPipeOd(basis.System, basis.Dn);
                                var gapMm = IntersectUtilities.PipeScheduleV2.PipeScheduleV2.GetPipeDistanceForTwin(basis.System, basis.Dn, basis.Type);
                                zLower = -Math.Max(0.0, (odMm + gapMm)) / 1000.0;
                            }
                            // upper
                            g.Members.Add(new NtrReducer
                            {
                                P1 = pr[0].Node.Pos,
                                P2 = pr[1].Node.Pos,
                                Dn1 = InferDn1(topo, f),
                                Dn2 = InferDn2(topo, f),
                                Dn1Suffix = suffix1,
                                Dn2Suffix = suffix2,
                                Flow = FlowRole.Return,
                                ZOffsetMeters = 0.0,
                                Material = null,
                                Provenance = [f.Source]
                            });
                            if (isTwin)
                            {
                                // lower
                                g.Members.Add(new NtrReducer
                                {
                                    P1 = pr[0].Node.Pos,
                                    P2 = pr[1].Node.Pos,
                                    Dn1 = InferDn1(topo, f),
                                    Dn2 = InferDn2(topo, f),
                                    Dn1Suffix = suffix1,
                                    Dn2Suffix = suffix2,
                                    Flow = FlowRole.Supply,
                                    ZOffsetMeters = zLower,
                                    Material = null,
                                    Provenance = [f.Source]
                                });
                            }
                        }
                        break;

                    // Valves → ARM
                    case TFitting f when f.Kind is PipelineElementType.Engangsventil
                                              or PipelineElementType.PræisoleretVentil
                                              or PipelineElementType.PræventilMedUdluftning:
                        {
                            var pv = f.Ports.Take(2).ToArray();
                            var p1 = pv[0].Node.Pos; var p2 = pv[1].Node.Pos;
                            var pm = new Pt2((p1.X + p2.X) / 2, (p1.Y + p2.Y) / 2);
                            var vnear1 = topo.Elements.OfType<TPipe>().FirstOrDefault(p => p.A.Node == pv[0].Node || p.B.Node == pv[0].Node);
                            var vnear2 = topo.Elements.OfType<TPipe>().FirstOrDefault(p => p.A.Node == pv[1].Node || p.B.Node == pv[1].Node);
                            var suffix1 = vnear1?.Variant.DnSuffix ?? "s";
                            var suffix2 = vnear2?.Variant.DnSuffix ?? suffix1;
                            var isTwin = (vnear1?.Variant.IsTwin ?? false) || (vnear2?.Variant.IsTwin ?? false);
                            var basis = vnear1 ?? vnear2;
                            var zLower = 0.0;
                            if (isTwin && basis != null)
                            {
                                var odMm = IntersectUtilities.PipeScheduleV2.PipeScheduleV2.GetPipeOd(basis.System, basis.Dn);
                                var gapMm = IntersectUtilities.PipeScheduleV2.PipeScheduleV2.GetPipeDistanceForTwin(basis.System, basis.Dn, basis.Type);
                                zLower = -Math.Max(0.0, (odMm + gapMm)) / 1000.0;
                            }
                            // upper
                            g.Members.Add(new NtrInstrument
                            {
                                P1 = p1,
                                P2 = p2,
                                Pm = pm,
                                Dn1 = InferMainDn(topo, f),
                                Dn2 = InferMainDn(topo, f),
                                Flow = FlowRole.Return,
                                ZOffsetMeters = 0.0,
                                Dn1Suffix = suffix1,
                                Dn2Suffix = suffix2,
                                Provenance = [f.Source]
                            });
                            if (isTwin)
                            {
                                // lower
                                g.Members.Add(new NtrInstrument
                                {
                                    P1 = p1,
                                    P2 = p2,
                                    Pm = pm,
                                    Dn1 = InferMainDn(topo, f),
                                    Dn2 = InferMainDn(topo, f),
                                    Flow = FlowRole.Supply,
                                    ZOffsetMeters = zLower,
                                    Dn1Suffix = suffix1,
                                    Dn2Suffix = suffix2,
                                    Provenance = [f.Source]
                                });
                            }
                        }
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
