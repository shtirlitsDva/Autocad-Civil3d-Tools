using DimensioneringV2.BruteForceOptimization;
using DimensioneringV2.GraphModel;
using DimensioneringV2.Services;

using Mapsui.Utilities;

using QuikGraph;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.Genetic
{
    internal class CoherencyManager
    {
        //Graph managing
        private readonly Dictionary<int, BFEdge> _indexToNonBridge;
        private readonly UndirectedGraph<BFNode, BFEdge> _originalGraph;
        private readonly HashSet<BFEdge> _bridges;
        private readonly HashSet<BFEdge> _nonBridges;
        public int ChromosomeLength => _nonBridges.Count;
        public UndirectedGraph<BFNode, BFEdge> OriginalGraph => _originalGraph;

        //Metagraph stuff
        private readonly MetaGraph<UndirectedGraph<BFNode, BFEdge>> _metaGraph;
        private readonly UndirectedGraph<BFNode, BFEdge> _seed;
        private readonly HashSet<BFNode> _terminals;
        private readonly BFNode _rootNode;
        
        internal bool hasNOTSeeded = true;
        internal UndirectedGraph<BFNode, BFEdge> Seed => _seed;
        internal MetaGraph<UndirectedGraph<BFNode, BFEdge>> MetaGraph => _metaGraph;
        internal HashSet<BFNode> Terminals => _terminals;
        internal BFNode RootNode => _rootNode;

        public CoherencyManager(
            MetaGraph<UndirectedGraph<BFNode, BFEdge>> metaGraph,
            UndirectedGraph<BFNode, BFEdge> subGraph,
            UndirectedGraph<BFNode, BFEdge> seed)
        {
            _originalGraph = subGraph;
            _indexToNonBridge = new Dictionary<int, BFEdge>();
            _originalGraph.InitNonBridgeChromosomeIndex();
            foreach (var item in subGraph.Edges.Where(x => x.NonBridgeChromosomeIndex != -1))
            {
                _indexToNonBridge.Add(item.NonBridgeChromosomeIndex, item);
            }

            _bridges = FindBridges.DoFindThem(subGraph);
            _nonBridges = subGraph.Edges.Where(x => !_bridges.Contains(x)).ToHashSet();

            this._metaGraph = metaGraph;
            _seed = seed;

            //Synchronize seed NonBridgeChromosomeIndex with original graph
            foreach (var seedEdge in Seed.Edges)
            {
                var query = OriginalGraph.Edges.Where(x => x.Source == seedEdge.Source && x.Target == seedEdge.Target);
                var result = query.FirstOrDefault();
                if (result != null)
                { seedEdge.NonBridgeChromosomeIndex = result.NonBridgeChromosomeIndex; }
                else
                {
                    throw new Exception("Seed edge not found in original graph!!!");
                }

            }

            _terminals = metaGraph.GetTerminalsForSubgraph(subGraph).ToHashSet();
            _rootNode = metaGraph.GetRootForSubgraph(subGraph);
        }

        public BFEdge OriginalNonBridgeEdgeFromIndex(int index)
        {
            return _indexToNonBridge[index];
        }
    }
}
