using IntersectUtilities.PipelineNetworkSystem;
using IntersectUtilities.PipelineNetworkSystem.QuikGraphClasses;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.LongitudinalProfiles.KoteReport
{
    internal class KRNode : NodeBase
    {
        public List<IConnection> Connections = new List<IConnection>();
        public KRNode(IPipelineV2 value) : base(value)
        {

        }
        public void AddConnection(IConnection connection)
        {
            Connections.Add(connection);
        }
    }
}