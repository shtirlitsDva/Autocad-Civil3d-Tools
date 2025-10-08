using IntersectUtilities.PipelineNetworkSystem;
using IntersectUtilities.UtilsCommon.Graphs;
using static IntersectUtilities.UtilsCommon.Utils;

using NTRExport.Interfaces;

namespace NTRExport.Topology
{
    internal class TopologyFactory
    {
        internal static void Create(PipelineNetwork network)
        {
            List<Graph<INtrSegment>> graphs = new();

            foreach (var pplGraph in network.PipelineGraphs)
            {
                var queue = new Queue<Node<IPipelineV2>>();
                queue.Enqueue(pplGraph.Root);

                while (queue.Count > 0)
                {
                    var node = queue.Dequeue();

                    foreach (var child in node.Children) queue.Enqueue(child);

                    var ppl = node.Value;

                    //Determine the starting segment
                    if (node.Parent == null)
                    {

                    }
                    else
                    {

                    }                    
                }
            }
        }
    }
}
