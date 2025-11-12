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
            // TODO: Implement properly with elevation/slope propagation
            var (zUp, zLow) = ComputeTwinOffsets(System, Type, DN);

            g.Members.Add(
                new RoutedValve(Source, this)
                {
                    P1 = P1.Node.Pos.Z(entryZ + zUp),
                    P2 = P2.Node.Pos.Z(entryZ + zUp),
                    Pm = P1.Node.Pos.Z(entryZ + zUp).MidPoint(P2.Node.Pos.Z(entryZ + zUp)),
                    DN = DN,
                    DnSuffix = Variant.DnSuffix,
                    Material = Material,
                }
            );
            // Propagate to other port
            var other = ReferenceEquals(entryPort, P1) ? P2 : P1;
            exits.Add((other, entryZ, entrySlope));
            return exits;
        }
    }
}

