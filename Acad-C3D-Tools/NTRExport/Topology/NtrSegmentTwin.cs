using Autodesk.AutoCAD.DatabaseServices;

using IntersectUtilities.PipelineNetworkSystem;
using IntersectUtilities.UtilsCommon.Enums;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTRExport.Topology
{
    internal class NtrSegmentTwin : NtrSegmentBase
    {
        private PipelineSegmentV2 _pseg;
        public NtrSegmentTwin(
            IPipelineSegmentV2 pseg,
            Dictionary<Entity, double> rdict) : base(pseg, rdict)
        {
            _pseg = (PipelineSegmentV2)pseg;
        }
    }
}
