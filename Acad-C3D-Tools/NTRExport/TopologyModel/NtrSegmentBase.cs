using Autodesk.AutoCAD.DatabaseServices;

using IntersectUtilities.PipelineNetworkSystem;
using IntersectUtilities.UtilsCommon.Enums;

using NTRExport.Interfaces;

namespace NTRExport.TopologyModel
{
    internal abstract class NtrSegmentBase : INtrSegment
    {
        public IPipelineSegmentV2 PipelineSegment => _segment;
        public abstract void BuildTopology();

        protected IPipelineSegmentV2 _segment;
        protected Dictionary<Entity, double> _rotationDict;

        public NtrSegmentBase(
            IPipelineSegmentV2 pseg,
            Dictionary<Entity, double> rdict)
        {
            _segment = pseg;
            _rotationDict = rdict;
        }
    }
}