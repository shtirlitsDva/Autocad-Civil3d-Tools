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
                            A = new Autodesk.AutoCAD.Geometry.Point2d(s.A.X, s.A.Y),
                            B = new Autodesk.AutoCAD.Geometry.Point2d(s.B.X, s.B.Y),
                            Dn = s.Dn,
                            Material = s.Material,
                            DnSuffix = s.DnSuffix,
                            Flow = s.Flow == Routing.RoutedFlow.Supply ? FlowRole.Supply : s.Flow == Routing.RoutedFlow.Return ? FlowRole.Return : FlowRole.Unknown,
                            ZOffsetMeters = s.ZOffsetMeters,
                            ZA = s.ZA ?? s.A.Z,
                            ZB = s.ZB ?? s.B.Z,
                            Provenance = new[] { s.Source },
                            Soil = s.Soil,
                        });
                        break;
                    case Routing.RoutedBend b:
                        g.Members.Add(new NtrBend(b.Source)
                        {
                            A = new Autodesk.AutoCAD.Geometry.Point2d(b.A.X, b.A.Y),
                            B = new Autodesk.AutoCAD.Geometry.Point2d(b.B.X, b.B.Y),
                            T = new Autodesk.AutoCAD.Geometry.Point2d(b.T.X, b.T.Y),
                            Dn = b.Dn,
                            Material = b.Material,
                            ZOffsetMeters = b.ZOffsetMeters,
                            ZA = b.Z1 ?? b.A.Z,
                            ZB = b.Z2 ?? b.B.Z,
                            ZT = b.Zt ?? b.T.Z,
                            DnSuffix = b.DnSuffix,
                            Flow = b.Flow == Routing.RoutedFlow.Supply ? FlowRole.Supply : b.Flow == Routing.RoutedFlow.Return ? FlowRole.Return : FlowRole.Unknown,
                            Provenance = new[] { b.Source },
                        });
                        break;
                    case Routing.RoutedReducer rr:
                        g.Members.Add(new NtrReducer(rr.Source)
                        {
                            P1 = new Autodesk.AutoCAD.Geometry.Point2d(rr.P1.X, rr.P1.Y),
                            P2 = new Autodesk.AutoCAD.Geometry.Point2d(rr.P2.X, rr.P2.Y),
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
                            Ph1 = new Autodesk.AutoCAD.Geometry.Point2d(rt.Ph1.X, rt.Ph1.Y),
                            Ph2 = new Autodesk.AutoCAD.Geometry.Point2d(rt.Ph2.X, rt.Ph2.Y),
                            Pa1 = new Autodesk.AutoCAD.Geometry.Point2d(rt.Pa1.X, rt.Pa1.Y),
                            Pa2 = new Autodesk.AutoCAD.Geometry.Point2d(rt.Pa2.X, rt.Pa2.Y),
                            Dn = rt.Dn,
                            DnBranch = rt.DnBranch,
                            DnMainSuffix = rt.DnMainSuffix,
                            DnBranchSuffix = rt.DnBranchSuffix,
                            Flow = FlowRole.Return,
                            ZOffsetMeters = 0.0,
                            Provenance = new[] { rt.Source }
                        });
                        break;
                    case Routing.RoutedInstrument rin:
                        g.Members.Add(new NtrInstrument(rin.Source)
                        {
                            P1 = new Autodesk.AutoCAD.Geometry.Point2d(rin.P1.X, rin.P1.Y),
                            P2 = new Autodesk.AutoCAD.Geometry.Point2d(rin.P2.X, rin.P2.Y),
                            Pm = new Autodesk.AutoCAD.Geometry.Point2d(rin.Pm.X, rin.Pm.Y),
                            Dn1 = rin.Dn1,
                            Dn2 = rin.Dn2,
                            Dn1Suffix = rin.Dn1Suffix,
                            Dn2Suffix = rin.Dn2Suffix,
                            Material = rin.Material,
                            ZOffsetMeters = rin.ZOffsetMeters,
                            Provenance = new[] { rin.Source }
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
