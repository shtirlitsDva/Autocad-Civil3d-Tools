using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using QuikGraph;

namespace IntersectUtilities.PipelineNetworkSystem.QuikGraphClasses
{
    internal class NodeBase
    {
        public bool Root { get; set; } = false;
        public IPipelineV2 Value { get; private set; }
        internal NodeBase(IPipelineV2 value)
        {
            Value = value;
        }
    }
}
