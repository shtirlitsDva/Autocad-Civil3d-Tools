using Autodesk.AutoCAD.DatabaseServices;

using IntersectUtilities.UtilsCommon.Enums;

using NTRExport.Enums;
using NTRExport.Routing;

namespace NTRExport.TopologyModel
{
    internal sealed class Endebund : TFitting
    {
        public Endebund(Handle source)
            : base(source, PipelineElementType.Endebund) { }

        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.Endebund);
        }

        public override List<(TPort exitPort, double exitZ, double exitSlope)> Route(
            RoutedGraph g, Topology topo, RouterContext ctx, TPort entryPort, double entryZ, double entrySlope)
        {
            // ignore for now
            return base.Route(g, topo, ctx, entryPort, entryZ, entrySlope);
        }
    }
}

