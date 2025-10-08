using Autodesk.AutoCAD.DatabaseServices;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.PipelineNetworkSystem
{
    internal class PipelineTransitionV2 : SegmentBaseV2
    {
        private BlockReference _br;
        internal PipelineTransitionV2(BlockReference br) {  _br = br; }
    }
}
