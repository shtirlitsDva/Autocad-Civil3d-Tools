using Autodesk.AutoCAD.Geometry;

using IntersectUtilities.PipeScheduleV2;
using IntersectUtilities.UtilsCommon.Enums;

using NTRExport.TopologyModel;

namespace NTRExport.Routing
{
    internal sealed class Router
    {
        private readonly Topology _topo;
        private readonly RoutingConfig _cfg;

        public Router(Topology topo, RoutingConfig cfg)
        {
            _topo = topo; _cfg = cfg;
        }

        // Phase 1: 1:1 mapping of Topology → Routed primitives (no macros yet)
        public RoutedGraph Route()
        {
            var g = new RoutedGraph();

            foreach (var e in _topo.Elements)
            {
                switch (e)
                {
                    case TPipe p:
                    {
                        var isTwin = p.Variant.IsTwin;
                        var dnSuffix = p.Variant.DnSuffix;
                        var (zUp, zLow) = TwinOffsetsMeters(p, isTwin);

                        if (isTwin)
                        {
                            // upper (return)
                            g.Members.Add(new RoutedStraight(p.Source)
                            {
                                A = p.A.Node.Pos,
                                B = p.B.Node.Pos,
                                Dn = p.Dn,
                                Material = p.Material,
                                DnSuffix = dnSuffix,
                                Flow = RoutedFlow.Return,
                                ZOffsetMeters = zUp,
                            });
                            // lower (supply)
                            g.Members.Add(new RoutedStraight(p.Source)
                            {
                                A = p.A.Node.Pos,
                                B = p.B.Node.Pos,
                                Dn = p.Dn,
                                Material = p.Material,
                                DnSuffix = dnSuffix,
                                Flow = RoutedFlow.Supply,
                                ZOffsetMeters = zLow,
                            });
                        }
                        else
                        {
                            var flow = p.Type switch
                            {
                                PipeTypeEnum.Frem => RoutedFlow.Supply,
                                PipeTypeEnum.Retur => RoutedFlow.Return,
                                _ => RoutedFlow.Unknown
                            };
                            g.Members.Add(new RoutedStraight(p.Source)
                            {
                                A = p.A.Node.Pos,
                                B = p.B.Node.Pos,
                                Dn = p.Dn,
                                Material = p.Material,
                                DnSuffix = dnSuffix,
                                Flow = flow,
                                ZOffsetMeters = 0.0,
                            });
                        }
                        break;
                    }
                    case PreinsulatedElbow f:
                    {
                        var ends = f.Ports.Take(2).ToArray();
                        if (ends.Length < 2) break;
                        var a = ends[0].Node.Pos; var b = ends[1].Node.Pos;
                        var t = f.TangentPoint;
                        ExpandPreinsulatedElbow(g, f.Source, a, b, t, _topo.InferMainDn(f));
                        break;
                    }
                    case ElbowFormstykke f:
                    {
                        var ends = f.Ports.Take(2).ToArray();
                        if (ends.Length < 2) break;
                        var a = ends[0].Node.Pos; var b = ends[1].Node.Pos;
                        var t = f.TangentPoint;
                        // emit one bend for now; twin duplication handled later by macros
                        g.Members.Add(new RoutedBend(f.Source)
                        {
                            A = a,
                            B = b,
                            T = t,
                            Dn = _topo.InferMainDn(f),
                            DnSuffix = "s",
                            Flow = RoutedFlow.Return,
                        });
                        break;
                    }
                    case Reducer r:
                    {
                        var pr = r.Ports.Take(2).ToArray();
                        if (pr.Length < 2) break;
                        var p1 = pr[0].Node.Pos; var p2 = pr[1].Node.Pos;
                        var dn1 = _topo.InferDn1(r);
                        var dn2 = _topo.InferDn2(r);
                        var near1 = _topo.FindPipeAtNodes(pr[0].Node);
                        var near2 = _topo.FindPipeAtNodes(pr[1].Node);
                        var suffix1 = near1?.Variant.DnSuffix ?? "s";
                        var suffix2 = near2?.Variant.DnSuffix ?? suffix1;
                        g.Members.Add(new RoutedReducer(r.Source)
                        {
                            P1 = p1,
                            P2 = p2,
                            Dn1 = dn1,
                            Dn2 = dn2,
                            Dn1Suffix = suffix1,
                            Dn2Suffix = suffix2,
                            Flow = RoutedFlow.Return,
                        });
                        break;
                    }
                    case TeeMainRun tee:
                    {
                        var mains = tee.MainPorts.Take(2).ToArray();
                        if (mains.Length < 2) break;
                        var br = tee.BranchPorts.FirstOrDefault() ?? tee.Ports.FirstOrDefault();
                        if (br == null) break;
                        var dnMain = _topo.InferMainDn(tee);
                        var dnBranch = _topo.InferBranchDn(tee);
                        var nearMain1 = _topo.FindPipeAtNodes(mains[0].Node);
                        var nearMain2 = _topo.FindPipeAtNodes(mains[1].Node);
                        var nearBr = _topo.FindPipeAtNodes(br.Node);
                        var suffixMain = nearMain1?.Variant.DnSuffix ?? nearMain2?.Variant.DnSuffix ?? "s";
                        var suffixBr = nearBr?.Variant.DnSuffix ?? suffixMain;
                        g.Members.Add(new RoutedTee(tee.Source)
                        {
                            Ph1 = mains[0].Node.Pos,
                            Ph2 = mains[1].Node.Pos,
                            Pa1 = br.Node.Pos,
                            Pa2 = br.Node.Pos,
                            Dn = dnMain,
                            DnBranch = dnBranch,
                            DnMainSuffix = suffixMain,
                            DnBranchSuffix = suffixBr,
                            Flow = RoutedFlow.Return,
                        });
                        break;
                    }
                }
            }
            return g;
        }

        private static (double zUp, double zLow) TwinOffsetsMeters(TPipe p, bool isTwin)
        {
            if (!isTwin) return (0.0, 0.0);
            var odMm = PipeScheduleV2.GetPipeOd(p.System, p.Dn);
            var gapMm = PipeScheduleV2.GetPipeDistanceForTwin(p.System, p.Dn, p.Type);
            var z = Math.Max(0.0, (odMm + gapMm)) / 2000.0;
            return (z, -z);
        }

        private void ExpandPreinsulatedElbow(RoutedGraph g, Handle src, Point2d a, Point2d b, Point2d t, int dn)
        {
            // Create straight segments of configured length at both ends and a bend connecting them
            var leg = _cfg.PreinsulatedLegMeters;
            var dirAB = new Vector2d(b.X - a.X, b.Y - a.Y);
            var len = dirAB.Length;
            if (len <= 1e-9) return;
            var uv = dirAB / len;
            var aLegEnd = new Point2d(a.X + uv.X * leg, a.Y + uv.Y * leg);
            var bLegStart = new Point2d(b.X - uv.X * leg, b.Y - uv.Y * leg);

            g.Members.Add(new RoutedStraight(src)
            {
                A = a,
                B = aLegEnd,
                Dn = dn,
                DnSuffix = "s",
                Flow = RoutedFlow.Return,
            });
            g.Members.Add(new RoutedBend(src)
            {
                A = aLegEnd,
                B = bLegStart,
                T = t,
                Dn = dn,
                DnSuffix = "s",
                Flow = RoutedFlow.Return,
            });
            g.Members.Add(new RoutedStraight(src)
            {
                A = bLegStart,
                B = b,
                Dn = dn,
                DnSuffix = "s",
                Flow = RoutedFlow.Return,
            });
        }
    }
}


