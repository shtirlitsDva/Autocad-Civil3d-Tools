using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using IntersectUtilities;
using IntersectUtilities.PipeScheduleV2;
using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.Enums;

using NTRExport.Enums;
using NTRExport.Routing;
using NTRExport.SoilModel;

using static IntersectUtilities.UtilsCommon.Utils;
using static NTRExport.Utils.Utils;


namespace NTRExport.TopologyModel
{
    internal class TNode
    {
        public Point3d Pos { get; init; }
        public string Name { get; set; } = ""; // assigned later
        public List<TPort> Ports { get; } = new();
    }

    internal class TPort
    {
        public PortRole Role { get; init; }
        public TNode Node { get; init; }
        public ElementBase Owner { get; init; }

        public TPort(PortRole role, TNode node, ElementBase owner)
        {
            Role = role;
            Node = node;
            Owner = owner;
        }
    }

    internal abstract class ElementBase
    {
        public Handle Source { get; }
        protected Entity _entity;

        protected ElementBase(Handle src)
        {
            Source = src;
            var db = Autodesk.AutoCAD.ApplicationServices.Core.Application
                .DocumentManager.MdiActiveDocument.Database;
            var tx = db.TransactionManager.TopTransaction;
            _entity = src.Go<Entity>(db);
        }

        public abstract IReadOnlyList<TPort> Ports { get; }
        public virtual PipeSystemEnum System => system();

        private PipeSystemEnum system()
        {
            return _entity switch
            {
                Polyline _ => PipeScheduleV2.GetPipeSystem(_entity),
                BlockReference br => br.GetPipeSystemEnum(),
                _ => PipeSystemEnum.Ukendt,
            };
        }

        public virtual PipeTypeEnum Type => type();

        private PipeTypeEnum type()
        {
            return _entity switch
            {
                Polyline _ => PipeScheduleV2.GetPipeType(_entity),
                BlockReference br => br.GetPipeTypeEnum(),
                _ => PipeTypeEnum.Ukendt,
            };
        }

        public virtual PipeSeriesEnum Series => series();

        private PipeSeriesEnum series()
        {
            return _entity switch
            {
                Polyline _ => PipeScheduleV2.GetPipeSeriesV2(_entity),
                BlockReference br => br.GetPipeSeriesEnum(),
                _ => PipeSeriesEnum.Undefined,
            };
        }

        public abstract int DN { get; }
        public virtual string Material
        {
            get
            {
                return System switch
                {
                    PipeSystemEnum.Stål => "P235GH",
                    _ => "Unknown",
                };
            }
        }
        public IPipeVariant Variant =>
            Type == PipeTypeEnum.Twin ? new TwinVariant() : new SingleVariant();

        protected virtual (double zUp, double zLow) ComputeTwinOffsets(
            PipeSystemEnum ps,
            PipeTypeEnum pt,
            int dn
        )
        {
            if (!Variant.IsTwin)
                return (0.0, 0.0);

            var odMm = PipeScheduleV2.GetPipeOd(ps, dn);
            var gapMm = PipeScheduleV2.GetPipeDistanceForTwin(ps, dn, pt);
            var z = Math.Max(0.0, odMm + gapMm) / 2000.0;
            return (z, -z);
        }

        public virtual void Route(RoutedGraph g, Topology topo, RouterContext ctx) { }
    }

    internal class TPipe : ElementBase
    {
        public TPort A { get; }
        public TPort B { get; }
        public override int DN => PipeScheduleV2.GetPipeDN(_entity);

        // Cushion spans along this pipe in meters (s0,s1) from A→B
        public List<(double s0, double s1)> CushionSpans { get; } = new();
        public double Length
        {
            get
            {
                var dx = B.Node.Pos.X - A.Node.Pos.X;
                var dy = B.Node.Pos.Y - A.Node.Pos.Y;
                return Math.Sqrt(dx * dx + dy * dy);
            }
        }

        public TPipe(Handle h, Curve2d s, Func<TPipe, TPort> makeA, Func<TPipe, TPort> makeB) : base(h)
        {
            A = makeA(this);
            B = makeB(this);
        }

        public override IReadOnlyList<TPort> Ports => [A, B];

        public override void Route(RoutedGraph g, Topology topo, RouterContext ctx)
        {
            var isTwin = Variant.IsTwin;
            var suffix = Variant.DnSuffix;
            var (zUp, zLow) = ComputeTwinOffsets(System, Type, DN);
            var flow = Type == PipeTypeEnum.Frem ? FlowRole.Supply : FlowRole.Return;
            var ltg = LTGMain(Source);

            var cuts = new SortedSet<double> { 0.0, Length };
            foreach (var (s0, s1) in CushionSpans)
            {
                cuts.Add(Math.Max(0.0, Math.Min(Length, s0)));
                cuts.Add(Math.Max(0.0, Math.Min(Length, s1)));
            }

            var segments = cuts.ToList();
            for (int i = 0; i < segments.Count - 1; i++)
            {
                var s0 = segments[i];
                var s1 = segments[i + 1];
                if (s1 - s0 < 1e-6)
                    continue;

                var soil = IsCovered(CushionSpans, s0, s1)
                    ? new SoilProfile("Soil_C80", 0.08)
                    : SoilProfile.Default;

                var aPos = Lerp(A.Node.Pos, B.Node.Pos, Length <= 1e-9 ? 0.0 : s0 / Length);
                var bPos = Lerp(A.Node.Pos, B.Node.Pos, Length <= 1e-9 ? 0.0 : s1 / Length);

                if (isTwin)
                {
                    g.Members.Add(
                        new RoutedStraight(Source, this)
                        {
                            A = aPos.Z(zUp),
                            B = bPos.Z(zUp),
                            DN = DN,
                            Material = Material,
                            DnSuffix = suffix,
                            FlowRole = FlowRole.Return,
                            Soil = soil,
                            LTG = ltg,
                        }
                    );
                    g.Members.Add(
                        new RoutedStraight(Source, this)
                        {
                            A = aPos.Z(zLow),
                            B = bPos.Z(zLow),
                            DN = DN,
                            Material = Material,
                            DnSuffix = suffix,
                            FlowRole = FlowRole.Supply,
                            Soil = soil,
                            LTG = ltg,
                        }
                    );
                }
                else
                {
                    g.Members.Add(
                        new RoutedStraight(Source, this)
                        {
                            A = aPos,
                            B = bPos,
                            DN = DN,
                            Material = Material,
                            DnSuffix = suffix,
                            FlowRole = flow,
                            Soil = soil,
                            LTG = ltg,
                        }
                    );
                }
            }
        }


        private static bool IsCovered(List<(double s0, double s1)> spans, double a, double b)
        {
            var mid = 0.5 * (a + b);
            return spans.Any(z => mid >= z.s0 - 1e-9 && mid <= z.s1 + 1e-9);
        }

        private static Point3d Lerp(Point3d a, Point3d b, double t) =>
            new(a.X + t * (b.X - a.X), a.Y + t * (b.Y - a.Y), a.Z + t * (b.Z - a.Z));
    }

    #region PipeVariant
    internal interface IPipeVariant
    {
        string DnSuffix { get; }
        bool IsTwin { get; }
    }

    internal sealed class SingleVariant : IPipeVariant
    {
        public string DnSuffix => "s";
        public bool IsTwin => false;
    }

    internal sealed class TwinVariant : IPipeVariant
    {
        public string DnSuffix => "t";
        public bool IsTwin => true;
    }
    #endregion

    internal abstract class TFitting : ElementBase
    {
        private readonly List<TPort> _ports = new();
        private readonly HashSet<PipelineElementType> _allowedKinds = new();

        protected TFitting(Handle source, PipelineElementType kind)
            : base(source)
        {
            ConfigureAllowedKinds(_allowedKinds);
            if (_allowedKinds.Count > 0 && !_allowedKinds.Contains(kind))
            {
                var allowedNames = string.Join(", ", _allowedKinds);
                throw new ArgumentOutOfRangeException(
                    nameof(kind),
                    kind,
                    $"Kind {kind} is not permitted. Allowed kinds: {allowedNames}."
                );
            }
            Kind = kind;
        }

        public PipelineElementType Kind { get; }
        public override IReadOnlyList<TPort> Ports => _ports;
        public override int DN => GetDn();

        private int GetDn()
        {
            var br = _entity as BlockReference;
            if (br == null)
            {
                var pl = _entity as Polyline;
                if (pl == null) throw new InvalidOperationException($"Entity {Source} не мышенок и не зверь!");
                return PipeScheduleV2.GetPipeDN(pl);
            }

            return Convert.ToInt32(br.ReadDynamicCsvProperty(DynamicProperty.DN1));
        }

        public void AddPort(TPort port)
        {
            if (!ReferenceEquals(port.Owner, this))
            {
                throw new InvalidOperationException("Port owner must be the fitting itself.");
            }

            _ports.Add(port);
        }

        public void AddPorts(IEnumerable<TPort> ports)
        {
            foreach (var port in ports)
            {
                AddPort(port);
            }
        }

        protected virtual void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
        }

        public override void Route(RoutedGraph g, Topology topo, RouterContext ctx)
        {
            // Default no-op; derived classes route if supported
        }

    }

    internal class ElbowFormstykke : TFitting
    {
        public Point3d TangentPoint { get; }

        public ElbowFormstykke(Handle source, Point3d tangentPoint, PipelineElementType kind)
            : base(source, kind)
        {
            TangentPoint = tangentPoint;
        }

        public ElbowFormstykke(Handle source, PipelineElementType kind)
            : base(source, kind)
        {
            var db = Autodesk.AutoCAD.ApplicationServices.Core.Application
                .DocumentManager.MdiActiveDocument.Database;

            var br = source.Go<BlockReference>(db);
            if (br == null)
                throw new System.Exception($"Received {source} for ElbowFormstykke!");

            TangentPoint = br.Position;
        }

        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.Kedelrørsbøjning);
            allowed.Add(PipelineElementType.Bøjning45gr);
            allowed.Add(PipelineElementType.Bøjning30gr);
            allowed.Add(PipelineElementType.Bøjning15gr);
        }

        public override void Route(RoutedGraph g, Topology topo, RouterContext ctx)
        {
            var ends = Ports.Take(2).ToArray();
            if (ends.Length < 2)
                return;
            var a = ends[0].Node.Pos;
            var b = ends[1].Node.Pos;
            var t = TangentPoint;
            var (zUp, zLow) = ComputeTwinOffsets(System, Type, DN);

            var flowMain = Variant.IsTwin
                ? FlowRole.Return
                : (Type == PipeTypeEnum.Frem ? FlowRole.Supply : FlowRole.Return);

            g.Members.Add(
                new Routing.RoutedBend(Source, this)
                {
                    A = a.Z(zUp),
                    B = b.Z(zUp),
                    T = t.Z(zUp),
                    DN = DN,
                    Material = Material,
                    DnSuffix = Variant.DnSuffix,
                    FlowRole = flowMain,
                    LTG = LTGMain(Source),
                }
            );

            if (Variant.IsTwin)
            {
                g.Members.Add(
                    new Routing.RoutedBend(Source, this)
                    {
                        A = a.Z(zLow),
                        B = b.Z(zLow),
                        T = t.Z(zLow),
                        DN = DN,
                        Material = Material,
                        DnSuffix = Variant.DnSuffix,
                        FlowRole = FlowRole.Supply,
                        LTG = LTGMain(Source),
                    }
                );
            }
        }


    }

    internal sealed class Bueror : ElbowFormstykke
    {
        public Bueror(Handle source, PipelineElementType kind)
            : base(source, CalculateTangentPoint(source), kind) { }

        private static Point3d CalculateTangentPoint(Handle source)
        {
            var db = Autodesk.AutoCAD.ApplicationServices.Core.Application
                .DocumentManager.MdiActiveDocument.Database;

            var br = source.Go<BlockReference>(db);
            if (br == null)
                throw new System.Exception($"Received {source} for Buerør! Must be BlockReference!");

            using var tx = db.TransactionManager.StartOpenCloseTransaction();
            var btr = br.BlockTableRecord.Go<BlockTableRecord>(tx);

            foreach (ObjectId id in btr)
            {
                if (!id.IsDerivedFrom<Arc>()) continue;

                var arc = id.Go<Arc>(tx);

                // Find tangent intersection using pure math (apply block transform)
                var tangentPoint = GetTangentPoint(arc, br.BlockTransform);

                if (tangentPoint != default)
                {
                    return tangentPoint;
                }

                break;
            }

            throw new System.Exception(
                $"Buerør: Arc not found for buerør {source}!");
        }

        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.Buerør);
        }

        public override void Route(RoutedGraph g, Topology topo, RouterContext ctx)
        {
            base.Route(g, topo, ctx);
        }
    }

    internal abstract class PreinsulatedElbowBase : ElbowFormstykke
    {
        protected PreinsulatedElbowBase(Handle source, PipelineElementType kind)
            : base(source, kind) { }

        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.PræisoleretBøjning90gr);
            allowed.Add(PipelineElementType.PræisoleretBøjning45gr);
            allowed.Add(PipelineElementType.PræisoleretBøjningVariabel);
        }

        protected abstract int ThresholdDN { get; }

        public override void Route(RoutedGraph g, Topology topo, RouterContext ctx)
        {
            var ends = Ports.Take(2).ToArray();
            if (ends.Length < 2) return;

            var a = ends[0].Node.Pos;
            var b = ends[1].Node.Pos;
            var t = TangentPoint;
            var r = (DN <= ThresholdDN ? Geometry.GetBogRadius5D(DN) : Geometry.GetBogRadius3D(DN)) / 1000.0;

            // Solve fillet points a' and b' for a radius r between lines (a↔t) and (b↔t)
            Point2d a2 = a.To2d();
            Point2d b2 = b.To2d();
            Point2d t2 = t.To2d();

            var va = a2 - t2; var vb = b2 - t2;
            if (va.Length < 1e-9 || vb.Length < 1e-9) return;

            var ua = va.GetNormal();
            var ub = vb.GetNormal();
            var dot = Math.Max(-1.0, Math.Min(1.0, ua.DotProduct(ub)));
            var alpha = Math.Acos(dot);
            var sinHalf = Math.Sin(alpha * 0.5);
            var cosHalf = Math.Cos(alpha * 0.5);
            if (sinHalf < 1e-9) return; // parallel/degenerate

            var l = r * (cosHalf / sinHalf); // R * cot(alpha/2)

            // Check feasibility: tangent points must lie between a↔t and b↔t
            var lenAT = va.Length; var lenBT = vb.Length;
            if (l > lenAT - 1e-9 || l > lenBT - 1e-9)
            {
                // radius too large for available leg length; clamp
                l = Math.Max(0.0, Math.Min(lenAT, lenBT) * 0.5);
            }

            var aPrime2 = new Point2d(t2.X + ua.X * l, t2.Y + ua.Y * l);
            var bPrime2 = new Point2d(t2.X + ub.X * l, t2.Y + ub.Y * l);

            var (zUp, zLow) = ComputeTwinOffsets(System, Type, DN);
            var mainFlow = Variant.IsTwin ? FlowRole.Return : (Type == PipeTypeEnum.Frem ? FlowRole.Supply : FlowRole.Return);
            var ltg = LTGMain(Source);

            void EmitFor(double z, FlowRole flow)
            {
                var aPrime = aPrime2.To3d(z);
                var bPrime = bPrime2.To3d(z);
                var aZ = a.Z(z);
                var bZ = b.Z(z);
                var tZ = t.Z(z);

                // a → a'
                g.Members.Add(
                    new Routing.RoutedStraight(Source, this)
                    {
                        A = aZ,
                        B = aPrime,
                        DN = DN,
                        Material = Material,
                        DnSuffix = Variant.DnSuffix,
                        FlowRole = flow,
                        LTG = ltg,
                    }
                );

                // bend a' → b' with PT = t
                g.Members.Add(
                    new Routing.RoutedBend(Source, this)
                    {
                        A = aPrime,
                        B = bPrime,
                        T = tZ,
                        DN = DN,
                        Material = Material,
                        DnSuffix = Variant.DnSuffix,
                        FlowRole = flow,
                        LTG = ltg,
                        Norm = DN <= ThresholdDN ? "" : "EN 10253-2 - Type A"
                    }
                );

                // b' → b
                g.Members.Add(
                    new Routing.RoutedStraight(Source, this)
                    {
                        A = bPrime,
                        B = bZ,
                        DN = DN,
                        Material = Material,
                        DnSuffix = Variant.DnSuffix,
                        FlowRole = flow,
                        LTG = ltg,
                    }
                );
            }

            // Emit main flow
            EmitFor(Variant.IsTwin ? zUp : 0.0, mainFlow);

            // Emit supply for twin
            if (Variant.IsTwin)
            {
                EmitFor(zLow, FlowRole.Supply);
            }
        }
    }

    internal sealed class PreinsulatedElbowAbove45deg : PreinsulatedElbowBase
    {
        public PreinsulatedElbowAbove45deg(Handle source, PipelineElementType kind)
            : base(source, kind) { }

        protected override int ThresholdDN => 400;
    }

    internal sealed class PreinsulatedElbowAtOrBelow45deg : PreinsulatedElbowBase
    {
        public PreinsulatedElbowAtOrBelow45deg(Handle source, PipelineElementType kind)
            : base(source, kind) { }

        protected override int ThresholdDN => 200;
    }

    internal abstract class TeeMainRun : TFitting
    {
        protected TeeMainRun(Handle source, PipelineElementType kind)
            : base(source, kind)
        {
            OffsetMain = ComputeTwinOffsets(System, Type, DnM);
        }

        public override int DN => DnM;
        protected int DnM =>
            _entity switch
            {
                BlockReference br => Convert.ToInt32(
                    br.ReadDynamicCsvProperty(DynamicProperty.DN1)
                ),
                _ => throw new InvalidOperationException(
                    $"Entity {Source} is not a BlockReference!"
                ),
            };
        protected int DnB =>
            _entity switch
            {
                BlockReference br => Convert.ToInt32(
                    br.ReadDynamicCsvProperty(DynamicProperty.DN2)
                ),
                _ => throw new InvalidOperationException(
                    $"Entity {Source} is not a BlockReference!"
                ),
            };
        protected TPort MainPort1 => Ports.First(p => p.Role == PortRole.Main);
        protected TPort MainPort2 => Ports.Last(p => p.Role == PortRole.Main);
        protected Point2d MidPoint => MainPort1.Node.Pos.To2d().MidPoint(MainPort2.Node.Pos.To2d());
        protected TPort BranchPort => Ports.First(p => p.Role == PortRole.Branch);
        protected (double zUp, double zLow) OffsetMain;

        public override void Route(RoutedGraph g, Topology topo, RouterContext ctx)
        {
            g.Members.Add(
                new RoutedStraight(Source, this)
                {
                    A = MainPort1.Node.Pos.Z(OffsetMain.zUp),
                    B = MainPort2.Node.Pos.Z(OffsetMain.zUp),
                    DN = DnM,
                    Material = Material,
                    DnSuffix = Variant.DnSuffix,
                    FlowRole = Variant.IsTwin
                        ? FlowRole.Return
                        : (Type == PipeTypeEnum.Frem ? FlowRole.Supply : FlowRole.Return),
                    LTG = LTGMain(Source),
                }
            );

            if (Variant.IsTwin)
            {
                g.Members.Add(
                    new RoutedStraight(Source, this)
                    {
                        A = MainPort1.Node.Pos.Z(OffsetMain.zLow),
                        B = MainPort2.Node.Pos.Z(OffsetMain.zLow),
                        DN = DnM,
                        Material = Material,
                        DnSuffix = Variant.DnSuffix,
                        FlowRole = FlowRole.Supply,
                        LTG = LTGMain(Source),
                    }
                );
            }
        }
    }

    internal sealed class TeeFormstykke : TeeMainRun
    {
        public TeeFormstykke(Handle source, PipelineElementType kind)
            : base(source, kind) { }

        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.Svejsetee);
            allowed.Add(PipelineElementType.PreskoblingTee);
            allowed.Add(PipelineElementType.Muffetee);
        }
    }

    internal sealed class AfgreningMedSpring : TeeMainRun
    {
        public AfgreningMedSpring(Handle source)
            : base(source, PipelineElementType.AfgreningMedSpring) { }

        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.AfgreningMedSpring);
        }

        public override void Route(RoutedGraph g, Topology topo, RouterContext ctx)
        {
            // TODO: implement macro; placeholder no-op
        }
    }

    internal sealed class AfgreningParallel : TeeMainRun
    {
        public AfgreningParallel(Handle source)
            : base(source, PipelineElementType.AfgreningParallel) { }

        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.AfgreningParallel);
        }

        public override void Route(RoutedGraph g, Topology topo, RouterContext ctx)
        {
            // TODO: implement macro; placeholder no-op
        }
    }

    internal sealed class LigeAfgrening : TeeMainRun
    {
        public LigeAfgrening(Handle source)
            : base(source, PipelineElementType.LigeAfgrening) { }

        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.LigeAfgrening);
        }

        public override void Route(RoutedGraph g, Topology topo, RouterContext ctx)
        {
            base.Route(g, topo, ctx);

            var offsetBranch = ComputeTwinOffsets(System, Type, DnB);

            if (!Variant.IsTwin)
            {
                g.Members.Add(
                    new RoutedStraight(Source, this)
                    {
                        A = BranchPort.Node.Pos,
                        B = MidPoint.To3d(),
                        DN = DnB,
                        Material = Material,
                        DnSuffix = Variant.DnSuffix,
                        FlowRole = Type == PipeTypeEnum.Frem ? FlowRole.Supply : FlowRole.Return,
                        LTG = LTGBranch(Source),
                    }
                );
            }
            else if (DnM == DnB)
            {
                g.Members.Add(
                    new RoutedStraight(Source, this)
                    {
                        A = BranchPort.Node.Pos.Z(OffsetMain.zUp),
                        B = MidPoint.To3d().Z(OffsetMain.zUp),
                        DN = DnB,
                        Material = Material,
                        DnSuffix = Variant.DnSuffix,
                        FlowRole = Variant.IsTwin
                            ? FlowRole.Return
                            : (Type == PipeTypeEnum.Frem ? FlowRole.Supply : FlowRole.Return),
                        LTG = LTGMain(Source),
                    });

                if (Variant.IsTwin)
                {
                    g.Members.Add(
                        new RoutedStraight(Source, this)
                        {
                            A = BranchPort.Node.Pos.Z(OffsetMain.zLow),
                            B = MidPoint.To3d().Z(OffsetMain.zLow),
                            DN = DnB,
                            Material = Material,
                            DnSuffix = Variant.DnSuffix,
                            FlowRole = FlowRole.Supply,
                            LTG = LTGMain(Source),
                        });
                }
            }
            else //Solve twin geometry with bends
            {
                //Project system to 2D plane through branch port,
                //mid point and plane normal is main run
                var branchOrigin = BranchPort.Node.Pos.To2d();
                var toMid = MidPoint - branchOrigin;
                var branchDistance = toMid.Length;
                if (branchDistance < 1e-9)
                {
                    prdDbg($"LigeAfgrening.Route: branch and main coincide for {Source}.");
                    return;
                }

                var branchDirPlan = toMid.GetNormal();

                Point2d branchStartUp = new Point2d(0.0, offsetBranch.zUp);
                Point2d branchEndUp = new Point2d(branchDistance, offsetBranch.zUp);
                Point2d mainCentreUp = new Point2d(branchDistance, OffsetMain.zUp);

                Point2d branchStartLow = new Point2d(0.0, offsetBranch.zLow);
                Point2d branchEndLow = new Point2d(branchDistance, offsetBranch.zLow);
                Point2d mainCentreLow = new Point2d(branchDistance, OffsetMain.zLow);

                double mainStubLength = PipeScheduleV2.GetPipeOd(System, DnM) / 2000.0 + 0.01;
                double bendOd = Geometry.GetBogRadius5D(DnB) / 1000.0;

                var filletReturn = Geometry.SolveBranchFillet(
                    branchStartUp,
                    branchEndUp,
                    mainCentreUp,
                    bendOd,
                    mainStubLength
                );

                if (filletReturn is null)
                {
                    prdDbg($"LigeAfgrening.Route: unable to solve return branch fillet for {Source}.");
                    return;
                }

                var filletSupply = Geometry.SolveBranchFillet(
                    branchStartLow,
                    branchEndLow,
                    mainCentreLow,
                    bendOd,
                    mainStubLength
                );

                if (filletSupply is null)
                {
                    prdDbg($"LigeAfgrening.Route: unable to solve supply branch fillet for {Source}.");
                    return;
                }

                Point3d ToWorld(Point2d local)
                {
                    var plan = branchOrigin + branchDirPlan.MultiplyBy(local.X);
                    return new Point3d(plan.X, plan.Y, local.Y);
                }

                void EmitTwinBranch(
                    Geometry.BranchFilletSolution fillet,
                    Point2d branchStartLocal,
                    Point2d mainCentreLocal,
                    FlowRole flowRole
                )
                {
                    var branchStartWorld = ToWorld(branchStartLocal);
                    var branchTangentWorld = ToWorld(fillet.BranchTangent);
                    var mainTangentWorld = ToWorld(fillet.MainTangent);
                    var tangentIntersectionWorld = ToWorld(fillet.TangentIntersection);
                    var mainCentreWorld = ToWorld(mainCentreLocal);

                    g.Members.Add(
                        new RoutedStraight(Source, this)
                        {
                            A = branchStartWorld,
                            B = branchTangentWorld,
                            DN = DnB,
                            Material = Material,
                            DnSuffix = Variant.DnSuffix,
                            FlowRole = flowRole,
                            LTG = LTGBranch(Source),
                        }
                    );

                    g.Members.Add(
                        new RoutedBend(Source, this)
                        {
                            A = branchTangentWorld,
                            B = mainTangentWorld,
                            T = tangentIntersectionWorld,
                            DN = DnB,
                            Material = Material,
                            DnSuffix = Variant.DnSuffix,
                            FlowRole = flowRole,
                            LTG = LTGBranch(Source),
                        }
                    );

                    g.Members.Add(
                        new RoutedStraight(Source, this)
                        {
                            A = mainTangentWorld,
                            B = mainCentreWorld,
                            DN = DnB,
                            Material = Material,
                            DnSuffix = Variant.DnSuffix,
                            FlowRole = flowRole,
                            LTG = LTGBranch(Source),
                        }
                    );
                }

                EmitTwinBranch(filletReturn.Value, branchStartUp, mainCentreUp, FlowRole.Return);
                EmitTwinBranch(filletSupply.Value, branchStartLow, mainCentreLow, FlowRole.Supply);
            }
        }
    }

    internal sealed class Stikafgrening : TeeMainRun
    {
        public Stikafgrening(Handle source)
            : base(source, PipelineElementType.Stikafgrening) { }

        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.Stikafgrening);
        }

        public override void Route(RoutedGraph g, Topology topo, RouterContext ctx)
        {
            // TODO: implement macro; placeholder no-op
        }
    }

    internal sealed class FModel : TFitting
    {
        public FModel(Handle source)
            : base(source, PipelineElementType.F_Model) { }

        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.F_Model);
        }

        public override void Route(RoutedGraph g, Topology topo, RouterContext ctx)
        {
            // Complex; handle later
        }
    }

    internal sealed class YModel : TFitting
    {
        public YModel(Handle source)
            : base(source, PipelineElementType.Y_Model) { }

        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.Y_Model);
        }

        public override void Route(RoutedGraph g, Topology topo, RouterContext ctx)
        {
            // Complex; handle later
        }
    }

    internal sealed class AfgreningsStuds : TFitting
    {
        public AfgreningsStuds(Handle source)
            : base(source, PipelineElementType.Afgreningsstuds) { }

        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.Afgreningsstuds);
        }

        public override void Route(RoutedGraph g, Topology topo, RouterContext ctx)
        {
            // Placeholder no-op
        }
    }

    internal sealed class Valve : TFitting
    {
        public Valve(Handle source, PipelineElementType kind)
            : base(source, kind) { }

        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.Engangsventil);
            allowed.Add(PipelineElementType.PræisoleretVentil);
            allowed.Add(PipelineElementType.PræventilMedUdluftning);
        }

        public override void Route(RoutedGraph g, Topology topo, RouterContext ctx)
        {
            var pr = Ports.Take(2).ToArray();
            if (pr.Length < 2)
                return;
            var p1 = pr[0].Node.Pos;
            var p2 = pr[1].Node.Pos;
            var pm = new Point2d((p1.X + p2.X) * 0.5, (p1.Y + p2.Y) * 0.5);
            var dn = topo.InferMainDn(this);
            g.Members.Add(
                new Routing.RoutedValve(Source, this)
                {
                    P1 = new Point3d(p1.X, p1.Y, 0.0),
                    P2 = new Point3d(p2.X, p2.Y, 0.0),
                    Pm = new Point3d(pm.X, pm.Y, 0.0),
                    Dn1 = dn,
                    Dn1Suffix = "s",
                    Dn2Suffix = "s",
                    Material = Material,
                }
            );
        }
    }

    internal sealed class Reducer : TFitting
    {
        public Reducer(Handle source)
            : base(source, PipelineElementType.Reduktion) { }

        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.Reduktion);
        }

        public override void Route(RoutedGraph g, Topology topo, RouterContext ctx)
        {
            var pr = Ports.Take(2).ToArray();
            if (pr.Length < 2)
                return;
            var p1 = pr[0].Node.Pos;
            var p2 = pr[1].Node.Pos;
            var dn1 = topo.InferDn1(this);
            var dn2 = topo.InferDn2(this);
            var near1 = topo.FindPipeAtNodes(pr[0].Node);
            var near2 = topo.FindPipeAtNodes(pr[1].Node);
            var s1 = near1?.Variant.DnSuffix ?? "s";
            var s2 = near2?.Variant.DnSuffix ?? s1;
            g.Members.Add(
                new Routing.RoutedReducer(Source, this)
                {
                    P1 = new Point3d(p1.X, p1.Y, 0.0),
                    P2 = new Point3d(p2.X, p2.Y, 0.0),
                    Dn1 = dn1,
                    Dn2 = dn2,
                    Dn1Suffix = s1,
                    Dn2Suffix = s2,
                    FlowRole = FlowRole.Return,
                }
            );
        }
    }

    internal sealed class Svanehals : TFitting
    {
        public Svanehals(Handle source)
            : base(source, PipelineElementType.Svanehals) { }

        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.Svanehals);
        }

        public override void Route(RoutedGraph g, Topology topo, RouterContext ctx)
        {
            // For later
        }
    }

    internal sealed class Materialeskift : TFitting
    {
        public Materialeskift(Handle source)
            : base(source, PipelineElementType.Materialeskift) { }

        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.Materialeskift);
        }

        public override void Route(RoutedGraph g, Topology topo, RouterContext ctx)
        {
            var pr = Ports.Take(2).ToArray();
            if (pr.Length < 2)
                return;
            var p1 = pr[0].Node.Pos;
            var p2 = pr[1].Node.Pos;
            var dn = topo.InferMainDn(this);
            g.Members.Add(
                new Routing.RoutedStraight(Source, this)
                {
                    A = new Point3d(p1.X, p1.Y, 0.0),
                    B = new Point3d(p2.X, p2.Y, 0.0),
                    DN = dn,
                    DnSuffix = "s",
                    Material = Material,
                    FlowRole = FlowRole.Unknown,
                }
            );
        }
    }

    internal sealed class Endebund : TFitting
    {
        public Endebund(Handle source)
            : base(source, PipelineElementType.Endebund) { }

        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.Endebund);
        }

        public override void Route(RoutedGraph g, Topology topo, RouterContext ctx)
        {
            // ignore for now
        }
    }

    internal sealed class GenericFitting : TFitting
    {
        public GenericFitting(Handle source, PipelineElementType kind)
            : base(source, kind) { }
    }

    internal class Topology
    {
        public List<TNode> Nodes { get; } = new();
        public List<ElementBase> Elements { get; } = new();

        public IEnumerable<TPipe> Pipes => Elements.OfType<TPipe>();
        public IEnumerable<TFitting> Fittings => Elements.OfType<TFitting>();

        public TPipe? FindPipeAtNodes(TNode nodeA, TNode? nodeB = null)
        {
            foreach (var pipe in Pipes)
            {
                if (pipe.A.Node == nodeA || pipe.B.Node == nodeA)
                {
                    if (nodeB == null || pipe.A.Node == nodeB || pipe.B.Node == nodeB)
                    {
                        return pipe;
                    }
                }
            }

            return null;
        }

        public int InferMainDn(TFitting fitting)
        {
            var dns = new List<int>();
            foreach (var node in fitting.Ports.Select(p => p.Node))
            {
                foreach (var pipe in Pipes)
                {
                    if (pipe.A.Node == node || pipe.B.Node == node)
                    {
                        dns.Add(pipe.DN);
                    }
                }
            }
            return dns.Count > 0 ? dns.Max() : 200;
        }

        public int InferBranchDn(TFitting fitting)
        {
            var dns = new List<int>();
            foreach (var node in fitting.Ports.Select(p => p.Node))
            {
                foreach (var pipe in Pipes)
                {
                    if (pipe.A.Node == node || pipe.B.Node == node)
                    {
                        dns.Add(pipe.DN);
                    }
                }
            }
            return dns.Count > 0 ? dns.Min() : 100;
        }

        public int InferDn1(TFitting fitting) => InferMainDn(fitting);

        public int InferDn2(TFitting fitting)
        {
            var dns = new List<int>();
            foreach (var node in fitting.Ports.Select(p => p.Node))
            {
                foreach (var pipe in Pipes)
                {
                    if (pipe.A.Node == node || pipe.B.Node == node)
                    {
                        dns.Add(pipe.DN);
                    }
                }
            }
            return dns.Count > 1 ? dns.Min() : 100;
        }
    }
}
