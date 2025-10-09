using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.AutoCAD.DatabaseServices;

namespace IntersectUtilities.PipelineNetworkSystem
{
    internal class PipelineSegmentV2 : SegmentBaseV2
    {
        internal override double MidStation => _midStation;
        private double _midStation;
        private List<Entity> _ents;

        internal PipelineSegmentV2(double midStation, List<Entity> ents)
        {
            _midStation = midStation;
            _ents = ents;
        }
    }
}
