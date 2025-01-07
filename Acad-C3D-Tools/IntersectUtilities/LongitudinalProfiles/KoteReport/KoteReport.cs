using IntersectUtilities.PipelineNetworkSystem;
using IntersectUtilities.PipelineNetworkSystem.QuikGraphClasses;

using QuikGraph;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.LongitudinalProfiles.KoteReport
{
    internal static class KoteReport
    {
        private static HashSet<AdjacencyGraph<KRNode, KREdge>>? _graphs;

        public static void BuildGraphs(GraphCollection ographs)
        {
            _graphs = GraphBuilder.BuildGraphs<KRNode, KREdge>(ographs);
        }

        public static void GenerateKoteReport()
        {

        }
    }
}
