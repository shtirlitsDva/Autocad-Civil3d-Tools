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
    internal class Valve : TFitting
    {
        public Valve(Handle source, PipelineElementType kind)
            : base(source, kind) { }
        protected TPort P1 => Ports.First();
        protected TPort P2 => Ports.Last();
        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.Engangsventil);
            allowed.Add(PipelineElementType.PræisoleretVentil);
            allowed.Add(PipelineElementType.PræventilMedUdluftning);
        }

        public override List<(TPort exitPort, double exitZ, double exitSlope)> Route(
            RoutedGraph g, Topology topo, RouterContext ctx, TPort entryPort, double entryZ, double entrySlope)
        {
            var exits = new List<(TPort exitPort, double exitZ, double exitSlope)>();
            var (zUp, zLow) = ComputeTwinOffsets(System, Type, DN);
            var ltg = LTGMain(Source);

            if (!Variant.IsTwin)
            {
                var flow = ResolveBondedFlowRole(topo);
                var p1 = P1.Node.Pos.Z(entryZ + zUp);
                var p2 = P2.Node.Pos.Z(entryZ + zUp);

                g.Members.Add(
                    new RoutedValve(Source, this)
                    {
                        P1 = p1,
                        P2 = p2,
                        Pm = p1.MidPoint(p2),
                        DN = DN,
                        DnSuffix = Variant.DnSuffix,
                        Material = Material,
                        FlowRole = flow,
                        LTG = ltg,
                    }
                );
            }
            else
            {
                var p1Return = P1.Node.Pos.Z(entryZ + zUp);
                var p2Return = P2.Node.Pos.Z(entryZ + zUp);
                g.Members.Add(
                    new RoutedValve(Source, this)
                    {
                        P1 = p1Return,
                        P2 = p2Return,
                        Pm = p1Return.MidPoint(p2Return),
                        DN = DN,
                        DnSuffix = Variant.DnSuffix,
                        Material = Material,
                        FlowRole = FlowRole.Return,
                        LTG = ltg,
                    }
                );

                var p1Supply = P1.Node.Pos.Z(entryZ + zLow);
                var p2Supply = P2.Node.Pos.Z(entryZ + zLow);
                g.Members.Add(
                    new RoutedValve(Source, this)
                    {
                        P1 = p1Supply,
                        P2 = p2Supply,
                        Pm = p1Supply.MidPoint(p2Supply),
                        DN = DN,
                        DnSuffix = Variant.DnSuffix,
                        Material = Material,
                        FlowRole = FlowRole.Supply,
                        LTG = ltg,
                    }
                );
            }

            // Propagate to other port
            var other = ReferenceEquals(entryPort, P1) ? P2 : P1;
            exits.Add((other, entryZ, entrySlope));
            return exits;
        }
    }
}

