using Autodesk.AutoCAD.DatabaseServices;

using IntersectUtilities.PipelineNetworkSystem;
using IntersectUtilities.UtilsCommon.Enums;

namespace NTRExport.Topology
{
    internal class NtrSegmentTransition : NtrSegmentBase
    {
        private PipelineTransitionV2 _tseg;
        public NtrSegmentTransition(
            IPipelineSegmentV2 pseg,
            Dictionary<Entity, double> rdict) : base(pseg, rdict)
        {
            _tseg = (PipelineTransitionV2)pseg;
        }
    }
}
