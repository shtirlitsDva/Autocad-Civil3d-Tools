using IntersectUtilities.PipelineNetworkSystem;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTRExport.Interfaces
{
    internal interface INtrSegment
    {
        public IPipelineSegmentV2 PipelineSegment { get; }
        void BuildTopology();
    }
}
