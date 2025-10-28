using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using IntersectUtilities.PipeScheduleV2;
using IntersectUtilities.UtilsCommon.Enums;

using NTRExport.TopologyModel;
using NTRExport.Enums;
using IntersectUtilities;
using IntersectUtilities.UtilsCommon;

namespace NTRExport.Routing
{
    internal sealed class Router
    {
        private readonly Topology _topo;
        
        public Router(Topology topo)
        {
            _topo = topo;
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
            var dirBranch = DirectionFrom(branchPipe, branchPort.Node); // branch is 90° in plan relative to main
            var farBranch = OtherEnd(branchPipe, branchPort.Node).Pos;

            // Preinsulated twin tee: fabricate as RO + BOG + RO only (no TEE record)
            if (branchPipe.Variant.IsTwin)
            {
                // Read legs (meters) using ports and block insertion
                var Lb = GetLegMeters(tee, PortRole.Branch, dirBranch); // BRANCH leg length (horizontal)
                var Lm = GetLegMeters(tee, PortRole.Main, dirMain);    // MAIN leg length (may be adjusted)
                var R = LookupCenterToEndMeters(branchPipe.Dn, 0.0); // 5D CtE = radius in meters
                if (Lb <= 0) Lb = R;
                if (Lm <= 0) Lm = R;

                // Z offsets for twin
                var (zMainUp, zMainLow) = TwinOffsetsMeters(mainPipe1, true);
                var (zBrUp, zBrLow) = TwinOffsetsMeters(branchPipe, true);

                void EmitFor(RoutedFlow flow, double zMain, double zBranch)
                {
                    var dz = zBranch - zMain;

                    // Unit vectors in plan
                    var u = dirMain; var v = dirBranch;

                    // Place PT along main at distance Lm from center; P1,P2 offset by R from PT
                    var pt = center + u * (Lm + R);
                    var p1 = pt - u * R;         // bend start on main stub
                    var p2 = pt + v * R;         // bend end on branch leg

                    // Emit with distinct Z per end: main stub slopes up to zBranch; branch stays at zBranch
                    g.Members.Add(new RoutedStraight(branchPipe.Source)
                    {
                        A = center,
                        B = p1,
                        Dn = branchPipe.Dn,
                        DnSuffix = branchPipe.Variant.DnSuffix,
                        Flow = flow,
                        ZA = zMain,
                        ZB = zMain + dz,
                    });

                    g.Members.Add(new RoutedBend(branchPipe.Source)
                    {
                        A = p1,
                        B = p2,
                        T = pt,
                        Dn = branchPipe.Dn,
                        DnSuffix = branchPipe.Variant.DnSuffix,
                        Flow = flow,
                        Z1 = zMain + dz,
                        Z2 = zBranch,
                        Zt = zBranch, // set PT at branch Z to ensure quarter plane tilt; acceptable for ROHR2
                    });

                    g.Members.Add(new RoutedStraight(branchPipe.Source)
                    {
                        A = p2,
                        B = farBranch,
                        Dn = branchPipe.Dn,
                        DnSuffix = branchPipe.Variant.DnSuffix,
                        Flow = flow,
                        ZA = zBranch,
                        ZB = zBranch,
                    });
                }

                EmitFor(RoutedFlow.Return, zMainUp, zBrUp);
                EmitFor(RoutedFlow.Supply, zMainLow, zBrLow);
                return; // skip RoutedTee
            }

            // Bonded/single: simple perpendicular branch without bend duplication
            {
                // Bonded: just connect branch straight to farBranch; no BOG, no TEE
                var p2 = center + dirBranch * GetLegMeters(tee, PortRole.Branch, dirBranch);
                var (zUp, zLow) = TwinOffsetsMeters(branchPipe, branchPipe.Variant.IsTwin);
                if (branchPipe.Variant.IsTwin)
                {
                    g.Members.Add(new RoutedStraight(branchPipe.Source)
                    {
                        A = p2,
                        B = farBranch,
                        Dn = branchPipe.Dn,
                        DnSuffix = branchPipe.Variant.DnSuffix,
                        Flow = RoutedFlow.Return,
                        ZA = zUp,
                        ZB = zUp,
                    });
                    g.Members.Add(new RoutedStraight(branchPipe.Source)
                    {
                        A = p2,
                        B = farBranch,
                        Dn = branchPipe.Dn,
                        DnSuffix = branchPipe.Variant.DnSuffix,
                        Flow = RoutedFlow.Supply,
                        ZA = zLow,
                        ZB = zLow,
                    });
                }
                else
                {
                    g.Members.Add(new RoutedStraight(branchPipe.Source)
                    {
                        A = p2,
                        B = farBranch,
                        Dn = branchPipe.Dn,
                        DnSuffix = branchPipe.Variant.DnSuffix,
                        Flow = branchPipe.Type == PipeTypeEnum.Frem ? RoutedFlow.Supply : RoutedFlow.Return,
                        ZA = 0.0,
                        ZB = 0.0,
                    });
                }
            }
        }

        private static double LookupCenterToEndMeters(int dn, double defaultMeters)
        {
            var mm = Geometry.GetBogRadius5D(dn);            
            return mm / 1000.0;            
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
            var leg = LookupCenterToEndMeters(dn, 0.0);
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

        private static double GetBranchLegMeters(TeeMainRun tee, int branchDn)
        {
            // Stub: will be implemented to read nested BRANCH reference and measure distance to insertion
            // Fallback to 5D center-to-end to keep geometry reasonable until implemented
            var mm = Geometry.GetBogRadius5D(branchDn);
            return mm / 1000.0;
        }

        private static double GetLegMeters(TeeMainRun tee, PortRole role, Vector2d alongDir)
        {
            try
            {
                var db = Autodesk.AutoCAD.ApplicationServices.Core.Application
                    .DocumentManager.MdiActiveDocument.Database;
                var br = teeBlock(tee, db);
                if (br == null) return 0.0;
                var ins = new Point2d(br.Position.X, br.Position.Y);

                var candidates = tee.Ports.Where(p => p.Role == role).ToList();
                if (candidates.Count == 0) return 0.0;

                double bestDot = double.NegativeInfinity;
                double bestLen = 0.0;
                foreach (var p in candidates)
                {
                    var w = new Vector2d(p.Node.Pos.X - ins.X, p.Node.Pos.Y - ins.Y);
                    var dot = w.X * alongDir.X + w.Y * alongDir.Y;
                    if (dot > bestDot)
                    {
                        bestDot = dot;
                        bestLen = Math.Sqrt(w.X * w.X + w.Y * w.Y);
                    }
                }
                return bestLen;
            }
            catch
            {
                return 0.0;
            }
        }

        private static BlockReference? teeBlock(TeeMainRun tee, Database db)
        {
            try
            {
                return tee.Source.Go<BlockReference>(db);
            }
            catch { return null; }
        }
    }
}


