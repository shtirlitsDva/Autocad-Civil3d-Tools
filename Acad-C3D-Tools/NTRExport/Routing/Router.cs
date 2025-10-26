using Autodesk.AutoCAD.DatabaseServices;
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

        private static readonly Dictionary<int, double> CenterToEndLookupMm = new()
        {
            { 15, 28 }, { 20, 29 }, { 25, 38 }, { 32, 48 }, { 40, 57 },
            { 50, 76 }, { 65, 95 }, { 80, 114 }, { 100, 152 }, { 125, 190 },
            { 150, 229 }, { 200, 305 }, { 250, 381 }, { 300, 457 }, { 350, 533 },
            { 400, 610 }, { 450, 686 }, { 500, 762 }, { 550, 1000 }, { 600, 914 }
        };

        public Router(Topology topo, RoutingConfig cfg)
        {
            _topo = topo; _cfg = cfg;
        }

        public RoutedGraph Route()
        {
            var g = new RoutedGraph();
            var pipes = _topo.Elements.OfType<TPipe>().ToList();
            var skipPipes = new HashSet<TPipe>();

            foreach (var e in _topo.Elements)
            {
                switch (e)
                {
                    case TPipe:
                        continue;

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
                        ExpandTee(g, tee, pipes, skipPipes);
                        break;
                    }
                }
            }

            foreach (var p in pipes)
            {
                if (skipPipes.Contains(p)) continue;
                EmitPipeStraight(g, p);
            }

            return g;
        }

        private void ExpandTee(RoutedGraph g, TeeMainRun tee, List<TPipe> pipes, HashSet<TPipe> skipPipes)
        {
            var mains = tee.MainPorts.Take(2).ToArray();
            if (mains.Length < 2) return;

            var branchPort = tee.BranchPorts.FirstOrDefault();
            if (branchPort == null) return;

            var mainPipe1 = _topo.FindPipeAtNodes(mains[0].Node);
            var mainPipe2 = _topo.FindPipeAtNodes(mains[1].Node);
            var branchPipe = _topo.FindPipeAtNodes(branchPort.Node);
            if (mainPipe1 == null || mainPipe2 == null || branchPipe == null) return;

            skipPipes.Add(branchPipe);

            var center = branchPort.Node.Pos;
            var dirMain = DirectionFrom(mainPipe1, mains[0].Node);
            var dirBranch = DirectionFrom(branchPipe, branchPort.Node).GetNormal();
            var farBranch = OtherEnd(branchPipe, branchPort.Node).Pos;

            // Branch geometry: short straight along main to offset, then bend into branch
            var ctEMeters = LookupCenterToEndMeters(branchPipe.Dn, _cfg.PreinsulatedLegMeters);
            var offset = Math.Max(_cfg.TeeOffsetMeters, ctEMeters);
            var p1 = center + dirMain * offset;
            var bendLeg = ctEMeters;
            var p2 = p1 + dirBranch * bendLeg;
            EmitBranchTwin(g, branchPipe, center, p1, p2, farBranch, dirBranch);

            g.Members.Add(new RoutedTee(tee.Source)
            {
                Ph1 = center - dirMain * offset,
                Ph2 = center + dirMain * offset,
                Pa1 = p1,
                Pa2 = p2,
                Dn = _topo.InferMainDn(tee),
                DnBranch = branchPipe.Dn,
                DnMainSuffix = mainPipe1.Variant.DnSuffix,
                DnBranchSuffix = branchPipe.Variant.DnSuffix,
                Flow = RoutedFlow.Return
            });
        }

        private static double LookupCenterToEndMeters(int dn, double defaultMeters)
        {
            if (CenterToEndLookupMm.TryGetValue(dn, out var mm))
            {
                return mm / 1000.0;
            }
            return defaultMeters;
        }

        private void EmitBranchTwin(RoutedGraph g, TPipe branch, Point2d center, Point2d offsetPoint, Point2d bendPoint, Point2d farPoint, Vector2d dirBranch)
        {
            var (zUp, zLow) = TwinOffsetsMeters(branch, branch.Variant.IsTwin);
            var suffix = branch.Variant.DnSuffix;

            void EmitLine(RoutedFlow flow, double zOffset)
            {
                g.Members.Add(new RoutedStraight(branch.Source)
                {
                    A = center,
                    B = offsetPoint,
                    Dn = branch.Dn,
                    DnSuffix = suffix,
                    Flow = flow,
                    ZOffsetMeters = zOffset,
                });
                g.Members.Add(new RoutedBend(branch.Source)
                {
                    A = offsetPoint,
                    B = bendPoint,
                    T = offsetPoint,
                    Dn = branch.Dn,
                    DnSuffix = suffix,
                    Flow = flow,
                    ZOffsetMeters = zOffset,
                });
                g.Members.Add(new RoutedStraight(branch.Source)
                {
                    A = bendPoint,
                    B = farPoint,
                    Dn = branch.Dn,
                    DnSuffix = suffix,
                    Flow = flow,
                    ZOffsetMeters = zOffset,
                });
            }

            if (branch.Variant.IsTwin)
            {
                EmitLine(RoutedFlow.Return, zUp);
                EmitLine(RoutedFlow.Supply, zLow);
            }
            else
            {
                var flow = branch.Type == PipeTypeEnum.Frem ? RoutedFlow.Supply : RoutedFlow.Return;
                EmitLine(flow, 0.0);
            }
        }

        private void EmitPipeStraight(RoutedGraph g, TPipe p)
        {
            var isTwin = p.Variant.IsTwin;
            var dnSuffix = p.Variant.DnSuffix;
            var (zUp, zLow) = TwinOffsetsMeters(p, isTwin);

            if (isTwin)
            {
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

        private static Vector2d DirectionFrom(TPipe pipe, TNode fromNode)
        {
            var other = OtherEnd(pipe, fromNode).Pos;
            var dir = new Vector2d(other.X - fromNode.Pos.X, other.Y - fromNode.Pos.Y);
            return dir.Length <= 1e-9 ? new Vector2d(1, 0) : dir / dir.Length;
        }

        private static TNode OtherEnd(TPipe pipe, TNode node)
        {
            return pipe.A.Node == node ? pipe.B.Node : pipe.A.Node;
        }
    }
}


