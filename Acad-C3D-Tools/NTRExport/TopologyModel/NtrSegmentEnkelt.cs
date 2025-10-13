using Autodesk.AutoCAD.DatabaseServices;

using IntersectUtilities.PipelineNetworkSystem;
using IntersectUtilities.UtilsCommon.Enums;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTRExport.TopologyModel
{
    internal class NtrSegmentEnkelt : NtrSegmentBase
    {
        private PipelineSegmentV2 _pseg;
        public NtrSegmentEnkelt(
            IPipelineSegmentV2 pseg,
            Dictionary<Entity, double> rdict) : base(pseg, rdict)
        {
            _pseg = (PipelineSegmentV2)pseg;
        }

        public override void BuildTopology()
        {

        }
    }
}
