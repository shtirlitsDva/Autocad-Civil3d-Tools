using DimensioneringV2.BruteForceOptimization;

using QuikGraph;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.GraphModel
{
    internal class MetaGraph<T>
    {
        private List<MetaNode<T>> _roots = new List<MetaNode<T>>();
        internal List<MetaNode<T>> Roots => _roots;

        // Maps each subgraph to a dictionary of BFNodes, each of which has a SubgraphNodeMetadata object:
        internal Dictionary<UndirectedGraph<BFNode, BFEdge>,
            Dictionary<BFNode, SubgraphNodeMetadata>> NodeFlags
        { get; } = new();

        //Maps nodes to transferable sums
        internal Dictionary<BFNode, List<dynamic>> Sums { get; } = new();

        public MetaGraph() { }

        internal IEnumerable<BFNode> GetTerminalsForSubgraph(UndirectedGraph<BFNode, BFEdge> subGraph)
        {
            var nodeFlags = NodeFlags[subGraph];
            var query1 = nodeFlags.Select(x => x.Key).ToList();
            var query2 = subGraph.Vertices.Where(x => x.IsBuildingNode).ToList();
            return query1.Concat(query2);
        }
    }

    internal class SubgraphNodeMetadata
    {
        public bool IsRoot { get; set; }
        public bool IsLeaf { get; set; }
    }
}
