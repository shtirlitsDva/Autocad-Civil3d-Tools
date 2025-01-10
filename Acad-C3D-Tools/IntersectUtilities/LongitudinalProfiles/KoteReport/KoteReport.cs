using IntersectUtilities.PipelineNetworkSystem;
using IntersectUtilities.PipelineNetworkSystem.QuikGraphClasses;
using static IntersectUtilities.UtilsCommon.Utils;

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
            if (_graphs == null) { prdDbg("_graphs is null!"); return; }

            foreach (var graph in _graphs)
            {
                var root = graph.Vertices.Where(x => x.Root).FirstOrDefault();
                if (root == null) { prdDbg("No root found!"); continue; }

                //Establish connections and ports
                Queue<KRNode> queue = new Queue<KRNode>();
                queue.Enqueue(root);

                while (queue.Count > 0)
                {
                    var parentNode = queue.Dequeue();
                    var children = graph.OutEdges(parentNode).Select(x => x.Target);

                    foreach (var child in children)
                    {
                        var pppl = parentNode.Value;
                        var cppl = child.Value;

                        if (cppl is PipelineV2Na) continue;

                        pppl.GetConnectionLocationToParent
                    }
                }
            }
        }
    }
}
