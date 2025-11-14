using Autodesk.AutoCAD.DatabaseServices;

using IntersectUtilities;
using IntersectUtilities.PipeScheduleV2;
using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.Enums;

using NTRExport.Enums;
using NTRExport.Routing;

using static IntersectUtilities.UtilsCommon.Utils;

namespace NTRExport.TopologyModel
{
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


        // Resolve DN to use for a neighboring connection based on the neighbor's port role.
        // Default behavior: use this fitting's DN for any role.
        public virtual bool TryGetDnForPortRole(PortRole role, out int dn)
        {
            dn = DN;
            return true;
        }

        protected FlowRole ResolveBondedFlowRole(Topology topo)
        {
            const FlowRole fallback = FlowRole.Return;

            if (Variant.IsTwin)
                return fallback;

            FlowRole FlowFromType(PipeTypeEnum pipeType) =>
                pipeType switch
                {
                    PipeTypeEnum.Frem => FlowRole.Supply,
                    PipeTypeEnum.Retur => FlowRole.Return,
                    _ => FlowRole.Unknown,
                };

            var direct = FlowFromType(Type);
            if (direct != FlowRole.Unknown)
                return direct;

            foreach (var port in Ports)
            {
                var role = topo.FindRoleFromPort(this, port);
                if (role != FlowRole.Unknown)
                {
                    prdDbg($"{GetType().Name} {Source} bonded flow role inferred as {role} via port {port.Role}.");
                    return role;
                }
            }

            prdDbg($"{GetType().Name} {Source} bonded flow role fallback to {fallback}.");
            return fallback;
        }

        protected FlowRole ResolveBondedFlowRole(Topology topo, TPort referencePort)
        {
            if (Variant.IsTwin)
                return FlowRole.Return;

            if (referencePort != null)
            {
                var role = topo.FindRoleFromPort(this, referencePort);
                if (role != FlowRole.Unknown)
                {
                    prdDbg($"{GetType().Name} {Source} bonded flow role inferred as {role} via port {referencePort.Role}.");
                    return role;
                }
            }

            return ResolveBondedFlowRole(topo);
        }
    }
}