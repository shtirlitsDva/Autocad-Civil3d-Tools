using Autodesk.AutoCAD.DatabaseServices;

using IntersectUtilities.PipelineNetworkSystem;
using IntersectUtilities.UtilsCommon.Enums;

namespace NTRExport.Topology
{
    internal class NtrSegmentTransition : NtrSegmentBase
    {
        public NtrSegmentTransition(IPipelineSegmentV2 pseg) : base(pseg)
        {

        }
    }
}
