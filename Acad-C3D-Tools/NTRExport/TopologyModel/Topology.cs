using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using IntersectUtilities;
using IntersectUtilities.PipeScheduleV2;
using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.Enums;

using NetTopologySuite.Algorithm;

using NTRExport.Enums;
using NTRExport.Ntr;
using NTRExport.SoilModel;

namespace NTRExport.TopologyModel
{
    internal class TNode
    {
        public Point2d Pos { get; init; }
        public string Name { get; set; } = ""; // assigned later
        public List<TPort> Ports { get; } = new();
    }

    internal class TPort
    {
        public PortRole Role { get; init; }
        public TNode Node { get; init; }
        public ElementBase Owner { get; init; }
        public TPort(PortRole role, TNode node, ElementBase owner) { Role = role; Node = node; Owner = owner; }
    }

    internal abstract class ElementBase
    {
        public Handle Source { get; }
        protected Entity _entity;
        protected ElementBase(Handle src)
        {
            Source = src;
            var db = Autodesk.AutoCAD.ApplicationServices.Core
                .Application.DocumentManager.MdiActiveDocument.Database;
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
                _ => PipeSystemEnum.Ukendt
            };
        }
        public virtual PipeTypeEnum Type => type();
        private PipeTypeEnum type()
        {
            return _entity switch
            {
                Polyline _ => PipeScheduleV2.GetPipeType(_entity),
                BlockReference br => br.GetPipeTypeEnum(),
                _ => PipeTypeEnum.Ukendt
            };
        }
        public virtual PipeSeriesEnum Series => series();
        private PipeSeriesEnum series()
        {
            return _entity switch
            {
                Polyline _ => PipeScheduleV2.GetPipeSeriesV2(_entity),
                BlockReference br => br.GetPipeSeriesEnum(),
                _ => PipeSeriesEnum.Undefined
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
                    _ => "Unknown"
                };
            }
        }
        public IPipeVariant Variant =>
            Type == PipeTypeEnum.Twin ? new TwinVariant() : new SingleVariant();
        protected virtual (double zUp, double zLow) ComputeTwinOffsets()
        {            
            if (!Variant.IsTwin) return (0.0, 0.0);

            var odMm = PipeScheduleV2.GetPipeOd(System, Dn);
            var gapMm = PipeScheduleV2.GetPipeDistanceForTwin(System, Dn, Type);
            var z = Math.Max(0.0, odMm + gapMm) / 2000.0;
            return (z, -z);        
        }
        public abstract void Emit(NtrGraph graph, Topology topo);

        protected static FlowRole MapFlowRole(FlowRole flow) => flow switch
        {
            FlowRole.Supply => FlowRole.Supply,
            FlowRole.Return => FlowRole.Return,
            _ => FlowRole.Unknown
        };
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
        public TPipe(Handle h,
            Curve2d s,
            Func<TPipe, TPort> makeA,
            Func<TPipe, TPort> makeB) : base(h)
        {
            A = makeA(this);
            B = makeB(this);
        }
        public override IReadOnlyList<TPort> Ports => [A, B];

        public override void Emit(NtrGraph graph, Topology topo)
        {
            var (zUp, zLow) = ComputeTwinOffsets();
            var suffix = Variant.DnSuffix;
            var isTwin = Variant.IsTwin;

            void EmitSegment(Point2d a0, Point2d b0, double s0, double s1)
            {
                var soil = IsCovered(CushionSpans, s0, s1) ? new SoilProfile("Soil_C80", 0.08) : SoilProfile.Default;

                graph.Members.Add(new NtrPipe(Source)
                {
                    A = a0,
                    B = b0,
                    Dn = Dn,
                    Material = Material,
                    DnSuffix = suffix,
                    Flow = isTwin ? FlowRole.Return :
                        Type == PipeTypeEnum.Frem ? FlowRole.Supply : FlowRole.Return,
                    ZOffsetMeters = zUp,
                    Provenance = [Source],
                    Soil = soil
                });

                if (isTwin)
                {
                    graph.Members.Add(new NtrPipe(Source)
                    {
                        A = a0,
                        B = b0,
                        Dn = Dn,
                        Material = Material,
                        DnSuffix = suffix,
                        Flow = FlowRole.Supply,
                        ZOffsetMeters = zLow,
                        Provenance = [Source],
                        Soil = soil
                    });
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
                if (s1 - s0 < 1e-6) continue;

                var pa = Lerp(A.Node.Pos, B.Node.Pos, Length <= 1e-9 ? 0.0 : s0 / Length);
                var pb = Lerp(A.Node.Pos, B.Node.Pos, Length <= 1e-9 ? 0.0 : s1 / Length);
                EmitSegment(pa, pb, s0, s1);
            }
        }        

        private static bool IsCovered(List<(double s0, double s1)> spans, double a, double b)
        {
            var mid = 0.5 * (a + b);
            return spans.Any(z => mid >= z.s0 - 1e-9 && mid <= z.s1 + 1e-9);
        }

        private static Point2d Lerp(Point2d a, Point2d b, double t) => new(a.X + t * (b.X - a.X), a.Y + t * (b.Y - a.Y));
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
        protected TFitting(Handle source, PipelineElementType kind) : base(source)
        {
            ConfigureAllowedKinds(_allowedKinds);
            if (_allowedKinds.Count > 0 && !_allowedKinds.Contains(kind))
            {
                var allowedNames = string.Join(", ", _allowedKinds);
                throw new ArgumentOutOfRangeException(nameof(kind), kind, $"Kind {kind} is not permitted. Allowed kinds: {allowedNames}.");
            }
            Kind = kind;
        }
        public PipelineElementType Kind { get; }
        public override IReadOnlyList<TPort> Ports => _ports;
        public override int Dn => GetDn();
        private int GetDn()
        {
            var br = _entity as BlockReference;
            if (br == null) throw new InvalidOperationException(
                $"Entity {Source} is not {this}!");
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

        protected virtual void EmitStub(NtrGraph graph)
        {
            graph.Members.Add(new NtrStub(Source)
            {
                Provenance = [Source]
            });
        }
    }

    internal class ElbowFormstykke : TFitting
    {
        public Point2d TangentPoint { get; }        

        public ElbowFormstykke(Handle source, Point2d tangentPoint, PipelineElementType kind)
            : base(source, kind)
        {
            TangentPoint = tangentPoint;
        }

        public ElbowFormstykke(Handle source, PipelineElementType kind)
            : base(source, kind)
        {
            var db = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager
                .MdiActiveDocument.Database;

            var br = source.Go<BlockReference>(db);
            if (br == null) throw new Exception($"Received {source} for ElbowFormstykke!");

            TangentPoint = br.Position.To2d();
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

        private void EmitElbowFormstykke(NtrGraph graph)
        {
            var portEnds = Ports.Take(2).ToList();
            if (portEnds.Count < 2) return;

            var aPos = portEnds[0].Node.Pos;
            var bPos = portEnds[1].Node.Pos;

            var (zUp, zLow) = ComputeTwinOffsets();
            var suffix = Variant.DnSuffix;
            var isTwin = Variant.IsTwin;

            FlowRole flowForMain = isTwin ? FlowRole.Return :
                Type == PipeTypeEnum.Frem ? FlowRole.Supply : FlowRole.Return;

            graph.Members.Add(new NtrBend(Source)
            {
                A = aPos,
                B = bPos,
                T = TangentPoint,
                Dn = Dn,
                Material = Material,
                DnSuffix = suffix,
                Flow = flowForMain,
                ZOffsetMeters = zUp,
                Provenance = [Source],
                Soil = new SoilProfile("Soil_C80", 0.08)
            });

            if (isTwin)
            {
                graph.Members.Add(new NtrBend(Source)
                {
                    A = aPos,
                    B = bPos,
                    T = TangentPoint,
                    Dn = Dn,
                    Material = Material,
                    DnSuffix = suffix,
                    Flow = FlowRole.Supply,
                    ZOffsetMeters = zLow,
                    Provenance = [Source],
                    Soil = new SoilProfile("Soil_C80", 0.08)
                });
            }
        }
    }

    internal sealed class Bueror : ElbowFormstykke
    {
        public Bueror(Handle source, PipelineElementType kind)
            : base(source, PipelineElementType.Buerør)
        {
        }

        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.Buerør);
        }
    }

    internal sealed class PreinsulatedElbow : ElbowFormstykke
    {
        public PreinsulatedElbow(Handle source, PipelineElementType kind)
            : base(source, kind)
        {
        }

        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.PræisoleretBøjning90gr);
            allowed.Add(PipelineElementType.PræisoleretBøjning45gr);
            allowed.Add(PipelineElementType.PræisoleretBøjningVariabel);
        }
    }

    internal abstract class TeeMainRun : TFitting
    {
        protected TeeMainRun(Handle source, PipelineElementType kind) : base(source, kind)
        {
        }

        public IEnumerable<TPort> MainPorts => Ports.Where(p => p.Role == PortRole.Main);
        public IEnumerable<TPort> BranchPorts => Ports.Where(p => p.Role == PortRole.Branch);
    }

    internal sealed class TeeFormstykke : TeeMainRun
    {
        public TeeFormstykke(Handle source, PipelineElementType kind)
            : base(source, kind)
        {
        }

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
            : base(source, PipelineElementType.AfgreningMedSpring)
        {
        }

        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.AfgreningMedSpring);
        }
    }

    internal sealed class AfgreningParallel : TeeMainRun
    {
        public AfgreningParallel(Handle source)
            : base(source, PipelineElementType.AfgreningParallel)
        {
        }

        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.AfgreningParallel);
        }
    }

    internal sealed class LigeAfgrening : TeeMainRun
    {
        public LigeAfgrening(Handle source)
            : base(source, PipelineElementType.LigeAfgrening)
        {
        }

        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.LigeAfgrening);
        }
    }

    internal sealed class Stikafgrening : TeeMainRun
    {
        public Stikafgrening(Handle source)
            : base(source, PipelineElementType.Stikafgrening)
        {
        }

        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.Stikafgrening);
        }
    }

    internal sealed class FModel : TFitting
    {
        public FModel(Handle source)
            : base(source, PipelineElementType.F_Model)
        {
        }

        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.F_Model);
        }
    }

    internal sealed class YModel : TFitting
    {
        public YModel(Handle source)
            : base(source, PipelineElementType.Y_Model)
        {
        }

        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.Y_Model);
        }
    }

    internal sealed class AfgreningsStuds : TFitting
    {
        public AfgreningsStuds(Handle source)
            : base(source, PipelineElementType.Afgreningsstuds)
        {
        }

        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.Afgreningsstuds);
        }
    }

    internal sealed class Valve : TFitting
    {
        public Valve(Handle source, PipelineElementType kind)
            : base(source, kind)
        {
        }

        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.Engangsventil);
            allowed.Add(PipelineElementType.PræisoleretVentil);
            allowed.Add(PipelineElementType.PræventilMedUdluftning);
        }
    }

    internal sealed class Reducer : TFitting
    {
        public Reducer(Handle source)
            : base(source, PipelineElementType.Reduktion)
        {
        }

        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.Reduktion);
        }
    }

    internal sealed class Svanehals : TFitting
    {
        public Svanehals(Handle source)
            : base(source, PipelineElementType.Svanehals)
        {
        }

        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.Svanehals);
        }
    }

    internal sealed class Materialeskift : TFitting
    {
        public Materialeskift(Handle source)
            : base(source, PipelineElementType.Materialeskift)
        {
        }

        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.Materialeskift);
        }
    }

    internal sealed class Endebund : TFitting
    {
        public Endebund(Handle source)
            : base(source, PipelineElementType.Endebund)
        {
        }

        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.Endebund);
        }
    }

    //internal sealed class SvejsningFitting : TFitting
    //{
    //    public SvejsningFitting(Handle source)
    //        : base(source, PipelineElementType.Svejsning)
    //    {
    //    }

    //    protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
    //    {
    //        allowed.Clear();
    //        allowed.Add(PipelineElementType.Svejsning);
    //    }
    //}

    internal sealed class GenericFitting : TFitting
    {
        public GenericFitting(Handle source, PipelineElementType kind)
            : base(source, kind)
        {
        }
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
