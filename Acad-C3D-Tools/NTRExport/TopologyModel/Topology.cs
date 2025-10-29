using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using IntersectUtilities;
using IntersectUtilities.PipeScheduleV2;
using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.Enums;

using NTRExport.Enums;
using NTRExport.Ntr;
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
            var db = Autodesk
                .AutoCAD
                .ApplicationServices
                .Core
                .Application
                .DocumentManager
                .MdiActiveDocument
                .Database;
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

        public abstract int Dn { get; }
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

        public abstract void Emit(NtrGraph graph, Topology topo);

        public virtual void Route(RoutedGraph g, Topology topo, RouterContext ctx) { }
    }

    internal class TPipe : ElementBase
    {
        public TPort A { get; }
        public TPort B { get; }
        public override int Dn => PipeScheduleV2.GetPipeDN(_entity);

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

        public override void Emit(NtrGraph graph, Topology topo)
        {
            var (zUp, zLow) = ComputeTwinOffsets(System, Type, Dn);
            var suffix = Variant.DnSuffix;
            var isTwin = Variant.IsTwin;

            void EmitSegment(Point3d a0, Point3d b0, double s0, double s1)
            {
                var soil = IsCovered(CushionSpans, s0, s1)
                    ? new SoilProfile("Soil_C80", 0.08)
                    : SoilProfile.Default;

                graph.Members.Add(
                    new NtrPipe(Source)
                    {
                        A = a0,
                        B = b0,
                        Dn = Dn,
                        Material = Material,
                        DnSuffix = suffix,
                        Flow =
                            isTwin ? FlowRole.Return
                            : Type == PipeTypeEnum.Frem ? FlowRole.Supply
                            : FlowRole.Return,
                        Provenance = [Source],
                        Soil = soil,
                        LTG = LTGMain(Source),
                    }
                );

                if (isTwin)
                {
                    graph.Members.Add(
                        new NtrPipe(Source)
                        {
                            A = a0,
                            B = b0,
                            Dn = Dn,
                            Material = Material,
                            DnSuffix = suffix,
                            Flow = FlowRole.Supply,
                            Provenance = [Source],
                            Soil = soil,
                            LTG = LTGMain(Source),
                        }
                    );
                }
            }

            if (CushionSpans.Count == 0)
            {
                EmitSegment(A.Node.Pos, B.Node.Pos, 0.0, Length);
                return;
            }

            var cuts = new SortedSet<double> { 0.0, Length };
            foreach (var (s0, s1) in CushionSpans)
            {
                cuts.Add(Math.Max(0.0, Math.Min(Length, s0)));
                cuts.Add(Math.Max(0.0, Math.Min(Length, s1)));
            }

            var list = cuts.ToList();
            for (int i = 0; i < list.Count - 1; i++)
            {
                var s0 = list[i];
                var s1 = list[i + 1];
                if (s1 - s0 < 1e-6)
                    continue;

                var pa = Lerp(A.Node.Pos, B.Node.Pos, Length <= 1e-9 ? 0.0 : s0 / Length);
                var pb = Lerp(A.Node.Pos, B.Node.Pos, Length <= 1e-9 ? 0.0 : s1 / Length);
                EmitSegment(pa, pb, s0, s1);
            }
        }

        public override void Route(RoutedGraph g, Topology topo, RouterContext ctx)
        {
            if (ctx.IsSkipped(this))
                return;

            var isTwin = Variant.IsTwin;
            var suffix = Variant.DnSuffix;
            var (zUp, zLow) = ComputeTwinOffsets(System, Type, Dn);

            if (isTwin)
            {
                g.Members.Add(
                    new Routing.RoutedStraight(Source)
                    {
                        A = A.Node.Pos.Z(zUp),
                        B = B.Node.Pos.Z(zUp),
                        Dn = Dn,
                        Material = Material,
                        DnSuffix = suffix,
                        Flow = FlowRole.Return,
                        LTG = LTGMain(Source),
                    }
                );
                g.Members.Add(
                    new Routing.RoutedStraight(Source)
                    {
                        A = A.Node.Pos.Z(zLow),
                        B = B.Node.Pos.Z(zLow),
                        Dn = Dn,
                        Material = Material,
                        DnSuffix = suffix,
                        Flow = FlowRole.Supply,
                        LTG = LTGMain(Source),
                    }
                );
            }
            else
            {
                var flow = Type == PipeTypeEnum.Frem ? FlowRole.Supply : FlowRole.Return;
                g.Members.Add(
                    new Routing.RoutedStraight(Source)
                    {
                        A = A.Node.Pos,
                        B = B.Node.Pos,
                        Dn = Dn,
                        Material = Material,
                        DnSuffix = suffix,
                        Flow = flow,
                        LTG = LTGMain(Source),
                    }
                );
            }
        }

        private static bool IsCovered(List<(double s0, double s1)> spans, double a, double b)
        {
            var mid = 0.5 * (a + b);
            return spans.Any(z => mid >= z.s0 - 1e-9 && mid <= z.s1 + 1e-9);
        }

        private static Point3d Lerp(Point3d a, Point3d b, double t) =>
            new(a.X + t * (b.X - a.X), a.Y + t * (b.Y - a.Y), 0);
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
        public override int Dn => GetDn();

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

        public override void Emit(NtrGraph graph, Topology topo)
        {
            EmitStub(graph);
        }

        public override void Route(RoutedGraph g, Topology topo, RouterContext ctx)
        {
            // Default no-op; derived classes route if supported
        }

        protected virtual void EmitStub(NtrGraph graph)
        {
            graph.Members.Add(new NtrStub(Source) { Provenance = [Source] });
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
                throw new Exception($"Received {source} for ElbowFormstykke!");

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

        public override void Emit(NtrGraph graph, Topology topo)
        {
            EmitElbowFormstykke(graph);
        }

        public override void Route(RoutedGraph g, Topology topo, RouterContext ctx)
        {
            var ends = Ports.Take(2).ToArray();
            if (ends.Length < 2)
                return;
            var a = ends[0].Node.Pos;
            var b = ends[1].Node.Pos;
            var t = TangentPoint;
            var (zUp, zLow) = ComputeTwinOffsets(System, Type, Dn);

            var flowMain = Variant.IsTwin
                ? FlowRole.Return
                : (Type == PipeTypeEnum.Frem ? FlowRole.Supply : FlowRole.Return);

            g.Members.Add(
                new Routing.RoutedBend(Source)
                {
                    A = a.Z(zUp),
                    B = b.Z(zUp),
                    T = t.Z(zUp),
                    Dn = Dn,
                    Material = Material,
                    DnSuffix = Variant.DnSuffix,
                    Flow = flowMain,                    
                    LTG = LTGMain(Source),
                }
            );

            if (Variant.IsTwin)
            {
                g.Members.Add(
                    new Routing.RoutedBend(Source)
                    {
                        A = a.Z(zLow),
                        B = b.Z(zLow),
                        T = t.Z(zLow),
                        Dn = Dn,
                        Material = Material,
                        DnSuffix = Variant.DnSuffix,
                        Flow = FlowRole.Supply,
                        LTG = LTGMain(Source),
                    }
                );
            }
        }

        private void EmitElbowFormstykke(NtrGraph graph)
        {
            var portEnds = Ports.Take(2).ToList();
            if (portEnds.Count < 2)
                return;

            var aPos = portEnds[0].Node.Pos;
            var bPos = portEnds[1].Node.Pos;

            var (zUp, zLow) = ComputeTwinOffsets(System, Type, Dn);
            var suffix = Variant.DnSuffix;
            var isTwin = Variant.IsTwin;

            FlowRole flowForMain =
                isTwin ? FlowRole.Return
                : Type == PipeTypeEnum.Frem ? FlowRole.Supply
                : FlowRole.Return;

            graph.Members.Add(
                new NtrBend(Source)
                {
                    A = aPos.Z(zUp),
                    B = bPos.Z(zUp),
                    T = TangentPoint,
                    Dn = Dn,
                    Material = Material,
                    DnSuffix = suffix,
                    Flow = flowForMain,
                    Provenance = [Source],
                    Soil = new SoilProfile("Soil_C80", 0.08),
                    LTG = LTGMain(Source),
                }
            );

            if (isTwin)
            {
                graph.Members.Add(
                    new NtrBend(Source)
                    {
                        A = aPos.Z(zLow),
                        B = bPos.Z(zLow),
                        T = TangentPoint,
                        Dn = Dn,
                        Material = Material,
                        DnSuffix = suffix,
                        Flow = FlowRole.Supply,
                        Provenance = [Source],
                        Soil = new SoilProfile("Soil_C80", 0.08),
                        LTG = LTGMain(Source),
                    }
                );
            }
        }
    }

    internal sealed class Bueror : ElbowFormstykke
    {
        public Bueror(Handle source, PipelineElementType kind)
            : base(source, PipelineElementType.Buerør) { }

        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.Buerør);
        }
    }

    internal sealed class PreinsulatedElbow : ElbowFormstykke
    {
        public PreinsulatedElbow(Handle source, PipelineElementType kind)
            : base(source, kind) { }

        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.PræisoleretBøjning90gr);
            allowed.Add(PipelineElementType.PræisoleretBøjning45gr);
            allowed.Add(PipelineElementType.PræisoleretBøjningVariabel);
        }

        public override void Route(RoutedGraph g, Topology topo, RouterContext ctx)
        {
            var ends = Ports.Take(2).ToArray();
            if (ends.Length < 2)
                return;
            var a = ends[0].Node.Pos;
            var b = ends[1].Node.Pos;
            var t = TangentPoint;
            var leg = Routing.Geometry.GetBogRadius5D(Dn) / 1000.0;
            var dir = new Vector2d(b.X - a.X, b.Y - a.Y);
            var len = dir.Length;
            if (len <= 1e-9)
                return;
            var uv = dir / len;
            var aLegEnd = new Point2d(a.X + uv.X * leg, a.Y + uv.Y * leg);
            var bLegStart = new Point2d(b.X - uv.X * leg, b.Y - uv.Y * leg);

            g.Members.Add(
                new Routing.RoutedStraight(Source)
                {
                    A = new Point3d(a.X, a.Y, 0.0),
                    B = new Point3d(aLegEnd.X, aLegEnd.Y, 0.0),
                    Dn = Dn,
                    DnSuffix = Variant.DnSuffix,
                    Flow = FlowRole.Return,
                }
            );
            g.Members.Add(
                new Routing.RoutedBend(Source)
                {
                    A = new Point3d(aLegEnd.X, aLegEnd.Y, 0.0),
                    B = new Point3d(bLegStart.X, bLegStart.Y, 0.0),
                    T = new Point3d(t.X, t.Y, 0.0),
                    Dn = Dn,
                    DnSuffix = Variant.DnSuffix,
                    Flow = FlowRole.Return,
                }
            );
            g.Members.Add(
                new Routing.RoutedStraight(Source)
                {
                    A = new Point3d(bLegStart.X, bLegStart.Y, 0.0),
                    B = new Point3d(b.X, b.Y, 0.0),
                    Dn = Dn,
                    DnSuffix = Variant.DnSuffix,
                    Flow = FlowRole.Return,
                }
            );
        }
    }

    internal abstract class TeeMainRun : TFitting
    {
        protected TeeMainRun(Handle source, PipelineElementType kind)
            : base(source, kind)
        {
            OffsetMain = ComputeTwinOffsets(System, Type, DnM);
        }

        public override int Dn => DnM;
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
                new RoutedStraight(Source)
                {
                    A = MainPort1.Node.Pos.Z(OffsetMain.zUp),
                    B = MainPort2.Node.Pos.Z(OffsetMain.zUp),
                    Dn = DnM,
                    Material = Material,
                    DnSuffix = Variant.DnSuffix,
                    Flow = Variant.IsTwin
                        ? FlowRole.Return
                        : (Type == PipeTypeEnum.Frem ? FlowRole.Supply : FlowRole.Return),
                    LTG = LTGMain(Source),
                }
            );

            if (Variant.IsTwin)
            {
                g.Members.Add(
                    new RoutedStraight(Source)
                    {
                        A = MainPort1.Node.Pos.Z(OffsetMain.zLow),
                        B = MainPort2.Node.Pos.Z(OffsetMain.zLow),                        
                        Dn = DnM,
                        Material = Material,
                        DnSuffix = Variant.DnSuffix,
                        Flow = FlowRole.Supply,
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
                    new RoutedStraight(Source)
                    {
                        A = BranchPort.Node.Pos,
                        B = MidPoint.To3d(),
                        Dn = DnB,
                        Material = Material,
                        DnSuffix = Variant.DnSuffix,
                        Flow = Type == PipeTypeEnum.Frem ? FlowRole.Supply : FlowRole.Return,
                        LTG = LTGBranch(Source),
                    }
                );
            }
            else if (DnM == DnB)
            {
                g.Members.Add(
                    new RoutedStraight(Source)
                    {
                        A = BranchPort.Node.Pos.Z(OffsetMain.zUp),
                        B = MidPoint.To3d().Z(OffsetMain.zUp),
                        Dn = DnB,
                        Material = Material,
                        DnSuffix = Variant.DnSuffix,
                        Flow = Variant.IsTwin
                            ? FlowRole.Return
                            : (Type == PipeTypeEnum.Frem ? FlowRole.Supply : FlowRole.Return),                        
                        LTG = LTGMain(Source),
                    });

                if (Variant.IsTwin)
                {
                    g.Members.Add(
                        new RoutedStraight(Source)
                        {
                            A = BranchPort.Node.Pos.Z(OffsetMain.zLow),
                            B = MidPoint.To3d().Z(OffsetMain.zLow),
                            Dn = DnB,
                            Material = Material,
                            DnSuffix = Variant.DnSuffix,
                            Flow = FlowRole.Supply,
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
                        new RoutedStraight(Source)
                        {
                            A = branchStartWorld,
                            B = branchTangentWorld,
                            Dn = DnB,
                            Material = Material,
                            DnSuffix = Variant.DnSuffix,
                            Flow = flowRole,
                            LTG = LTGBranch(Source),
                        }
                    );

                    g.Members.Add(
                        new RoutedBend(Source)
                        {
                            A = branchTangentWorld,
                            B = mainTangentWorld,
                            T = tangentIntersectionWorld,
                            Dn = DnB,
                            Material = Material,
                            DnSuffix = Variant.DnSuffix,
                            Flow = flowRole,
                            LTG = LTGBranch(Source),
                        }
                    );

                    g.Members.Add(
                        new RoutedStraight(Source)
                        {
                            A = mainTangentWorld,
                            B = mainCentreWorld,
                            Dn = DnB,
                            Material = Material,
                            DnSuffix = Variant.DnSuffix,
                            Flow = flowRole,
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
                new Routing.RoutedInstrument(Source)
                {
                    P1 = new Point3d(p1.X, p1.Y, 0.0),
                    P2 = new Point3d(p2.X, p2.Y, 0.0),
                    Pm = new Point3d(pm.X, pm.Y, 0.0),
                    Dn1 = dn,
                    Dn2 = dn,
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
                new Routing.RoutedReducer(Source)
                {
                    P1 = new Point3d(p1.X, p1.Y, 0.0),
                    P2 = new Point3d(p2.X, p2.Y, 0.0),
                    Dn1 = dn1,
                    Dn2 = dn2,
                    Dn1Suffix = s1,
                    Dn2Suffix = s2,
                    Flow = FlowRole.Return,
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
                new Routing.RoutedStraight(Source)
                {
                    A = new Point3d(p1.X, p1.Y, 0.0),
                    B = new Point3d(p2.X, p2.Y, 0.0),
                    Dn = dn,
                    DnSuffix = "s",
                    Material = Material,
                    Flow = FlowRole.Unknown,
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

    //internal sealed class SvejsningFitting : TFitting
    //{
    //    public SvejsningFitting(Handle source)
    //        : base(source, PipelineElementType.Svejsning)
    //    {
    //    }
    //
    //    protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
    //    {
    //        allowed.Clear();
    //        allowed.Add(PipelineElementType.Svejsning);
    //    }
    //}

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
                        dns.Add(pipe.Dn);
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
                        dns.Add(pipe.Dn);
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
                        dns.Add(pipe.Dn);
                    }
                }
            }
            return dns.Count > 1 ? dns.Min() : 100;
        }
    }
}
