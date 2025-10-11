using Autodesk.AutoCAD.DatabaseServices;

using IntersectUtilities.PipelineNetworkSystem;
using IntersectUtilities.UtilsCommon.Enums;

using NTRExport.Interfaces;

namespace NTRExport.Topology
{
    internal abstract class NtrSegmentBase : INtrSegment
    {
        public IPipelineSegmentV2 PipelineSegment => _segment;
        protected IPipelineSegmentV2 _segment;

        public NtrSegmentBase(IPipelineSegmentV2 pseg)
        {
            _segment = pseg;
        }
    }
}