using Autodesk.AutoCAD.DatabaseServices;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.PipelineNetworkSystem
{
    internal class PipelineSegmentV2 : SegmentBaseV2
    {
        private List<Entity> _ents;
        internal PipelineSegmentV2(List<Entity> ents)
        {
            _ents = ents;
        }
    }
}
