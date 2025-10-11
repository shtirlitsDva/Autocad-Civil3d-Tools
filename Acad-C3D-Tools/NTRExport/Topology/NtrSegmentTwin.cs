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
        public NtrSegmentTwin(IPipelineSegmentV2 pseg) : base(pseg)
        {

        }
    }
}
