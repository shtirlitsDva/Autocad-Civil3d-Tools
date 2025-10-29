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
                            Flow = s.FlowRole,                            
                            Provenance = [s.Source],
                            Soil = s.Soil,
                            LTG = s.LTG,
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
                            DnSuffix = b.DnSuffix,
                            Flow = b.FlowRole,
                            Provenance = [b.Source],
                            LTG = b.LTG,
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
                            Flow = rr.FlowRole,                            
                            Material = rr.Material,
                            Provenance = [rr.Source],
                            LTG = rr.LTG,
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
                            Flow = rt.FlowRole,
                            Provenance = [rt.Source],
                            LTG = rt.LTG,
                        });
                        break;
                    case Routing.RoutedInstrument rin:
                        g.Members.Add(new NtrInstrument(rin.Source)
                        {
                            P1 = rin.P1,
                            P2 = rin.P2,
                            Pm = rin.Pm,
                            Dn1 = rin.Dn1,
                            Dn2 = rin.Dn2,
                            Dn1Suffix = rin.Dn1Suffix,
                            Dn2Suffix = rin.Dn2Suffix,
                            Material = rin.Material,                            
                            Provenance = [rin.Source],
                            LTG = rin.LTG,
                        });
                        break;
                }
            }
            return g;
        }        
    }
}
