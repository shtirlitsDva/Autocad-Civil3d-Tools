using Autodesk.AutoCAD.DatabaseServices;

using IntersectUtilities.UtilsCommon.Enums;

namespace NTRExport.Topology
{
    internal class NtrSegmentTransition : NtrSegmentBase
    {
        public NtrSegmentTransition(
            PipeSystemEnum pipeSystem,
            PipeTypeEnum pipeType,
            PipeSeriesEnum pipeSeries,
            int dn,
            IEnumerable<Entity> entities,
            Polyline topology) :
            base(pipeSystem, pipeType, pipeSeries, dn, entities, topology)
        {

        }
    }
}
