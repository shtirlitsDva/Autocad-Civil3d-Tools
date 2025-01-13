using IntersectUtilities.PipelineNetworkSystem.QuikGraphClasses;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.LongitudinalProfiles.KoteReport
{
    internal class KREdge : EdgeBase<KRNode>
    {
        public KREdge(KRNode source, KRNode target) : base(source, target)
        {
        }

        public IConnection SourceCon { get; set; }
        public IConnection TargetCon { get; set; }
    }
}