using Autodesk.AutoCAD.DatabaseServices;

using IntersectUtilities;
using IntersectUtilities.PipeScheduleV2;
using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.Enums;

using NTRExport.Enums;
using NTRExport.Routing;

using static NTRExport.Utils.Utils;

namespace NTRExport.TopologyModel
{
    internal class Reducer : TFitting
    {
        public Reducer(Handle source)
            : base(source, PipelineElementType.Reduktion) { }

        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.Reduktion);
        }

        public override string DotLabelForTest()
        {
            return $"{Source.ToString()} / {this.GetType().Name}\n{DnLabel()}";
        }
        public override string DnLabel()
        {
            var br = _entity as BlockReference;
            if (br == null)
            {
                return "_entity is not BR!";
            }

            var dn1 = Convert.ToInt32(
                br.ReadDynamicCsvProperty(DynamicProperty.DN1));

            var dn2 = Convert.ToInt32(
                br.ReadDynamicCsvProperty(DynamicProperty.DN2));

            return $"{dn1.ToString()}/{dn2.ToString()}";
        }

        public override List<(TPort exitPort, double exitZ, double exitSlope)> Route(
            RoutedGraph g, Topology topo, RouterContext ctx, TPort entryPort, double entryZ, double entrySlope)
        {
            var exits = new List<(TPort exitPort, double exitZ, double exitSlope)>();
            // TODO: Implement properly with elevation/slope propagation
            var isTwin = Variant.IsTwin;
            var suffix = Variant.DnSuffix;
            var flow = Type == PipeTypeEnum.Frem ? FlowRole.Supply : FlowRole.Return;
            var ltg = LTGMain(Source);

            if (!topo.TryOrientReducer(this, out var dn1, out var dn2, out var P1, out var P2))
            {
                var pr = Ports.Take(2).ToArray();
                if (pr.Length < 2) return exits;
                P1 = pr[0].Node.Pos;
                P2 = pr[1].Node.Pos;
                dn1 = topo.InferDn1(this);
                dn2 = topo.InferDn2(this);
            }

            var (zUp1, zLow1) = ComputeTwinOffsets(System, Type, dn1);
            var (zUp2, zLow2) = ComputeTwinOffsets(System, Type, dn2);

            g.Members.Add(
                new RoutedReducer(Source, this)
                {
                    P1 = P1.Z(entryZ + zUp1),
                    P2 = P2.Z(entryZ + zUp2),
                    Dn1 = dn1,
                    Dn2 = dn2,
                    Material = Material,
                    Dn1Suffix = suffix,
                    Dn2Suffix = suffix,
                    FlowRole = flow,
                    LTG = ltg,
                }
            );

            if (isTwin)
            {
                g.Members.Add(
                    new RoutedReducer(Source, this)
                    {
                        P1 = P1.Z(entryZ + zLow1),
                        P2 = P2.Z(entryZ + zLow2),
                        Dn1 = dn1,
                        Dn2 = dn2,
                        Material = Material,
                        Dn1Suffix = suffix,
                        Dn2Suffix = suffix,
                        FlowRole = FlowRole.Supply,
                        LTG = ltg,
                    }
                );
            }
            // Propagate to other port
            var other = ReferenceEquals(entryPort, Ports[0]) ? Ports[1] : Ports[0];
            exits.Add((other, entryZ, entrySlope));
            return exits;
        }
    }
}

