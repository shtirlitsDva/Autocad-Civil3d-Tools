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
        internal override double MidStation => _midStation;
        private double _midStation;
        private BlockReference _br;
        internal PipelineTransitionV2(double midStation, BlockReference br) 
        {  
            _midStation = midStation;
            _br = br; 
        }
    }
}
