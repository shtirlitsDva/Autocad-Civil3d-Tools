using IntersectUtilities.PipeScheduleV2;
using IntersectUtilities.UtilsCommon.Enums;

using NTRExport.Enums;
using NTRExport.TopologyModel;

namespace NTRExport.Ntr
{
    internal class NtrMapper
    {
        public NtrGraph Map(Topology topo)
        {
            var g = new NtrGraph();

            foreach (var element in topo.Elements)
            {
                element.Emit(g, topo);
            }
            return g;
        }

        // Routed path (Phase 1: optional usage)
        public NtrGraph MapRouted(Routing.RoutedGraph routed)
        {
            var g = new NtrGraph();
            foreach (var m in routed.Members)
            {
                switch (m)
                {
                    case Routing.RoutedStraight s:
                        g.Members.Add(new NtrPipe(s.Source)
                        {
                            A = s.A,
                            B = s.B,
                            Dn = s.Dn,
                            Material = s.Material,
                            DnSuffix = s.DnSuffix,
                            Flow = s.Flow == Routing.RoutedFlow.Supply ? FlowRole.Supply : s.Flow == Routing.RoutedFlow.Return ? FlowRole.Return : FlowRole.Unknown,
                            ZOffsetMeters = s.ZOffsetMeters,
                            Provenance = new[] { s.Source },
                            Soil = s.Soil,
                        });
                        break;
                    case Routing.RoutedBend b:
                        g.Members.Add(new NtrBend(b.Source)
                        {
                            A = b.A,
                            B = b.B,
                            T = b.T,
                            Dn = b.Dn,
                            Material = b.Material,
                            ZOffsetMeters = b.ZOffsetMeters,
                            DnSuffix = b.DnSuffix,
                            Flow = b.Flow == Routing.RoutedFlow.Supply ? FlowRole.Supply : b.Flow == Routing.RoutedFlow.Return ? FlowRole.Return : FlowRole.Unknown,
                            Provenance = new[] { b.Source },
                        });
                        break;
                    case Routing.RoutedReducer rr:
                        g.Members.Add(new NtrReducer(rr.Source)
                        {
                            P1 = rr.P1,
                            P2 = rr.P2,
                            Dn1 = rr.Dn1,
                            Dn2 = rr.Dn2,
                            Dn1Suffix = rr.Dn1Suffix,
                            Dn2Suffix = rr.Dn2Suffix,
                            Flow = FlowRole.Return,
                            ZOffsetMeters = 0.0,
                            Material = rr.Material,
                            Provenance = new[] { rr.Source }
                        });
                        break;
                    case Routing.RoutedTee rt:
                        g.Members.Add(new NtrTee(rt.Source)
                        {
                            Ph1 = rt.Ph1,
                            Ph2 = rt.Ph2,
                            Pa1 = rt.Pa1,
                            Pa2 = rt.Pa2,
                            Dn = rt.Dn,
                            DnBranch = rt.DnBranch,
                            DnMainSuffix = rt.DnMainSuffix,
                            DnBranchSuffix = rt.DnBranchSuffix,
                            Flow = FlowRole.Return,
                            ZOffsetMeters = 0.0,
                            Provenance = new[] { rt.Source }
                        });
                        break;
                }
            }
            return g;
        }

        private static bool Covered(List<(double s0, double s1)> spans, double a, double b)
        {
            var mid = 0.5 * (a + b);
            return spans.Any(z => mid >= z.s0 - 1e-9 && mid <= z.s1 + 1e-9);
        }        
    }
}
