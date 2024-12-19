using DimensioneringV2.BruteForceOptimization;
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
        private readonly Dictionary<int, BFEdge> _indexToNonBridge;
        private readonly Dictionary<BFEdge, int> _nonBridgeToIndex;
        private readonly UndirectedGraph<BFNode, BFEdge> _originalGraph;
        private readonly ConcurrentHashSet<BitArray> _uniqueBitRepresentations;
        private readonly HashSet<BFEdge> _bridges;
        private readonly HashSet<BFEdge> _nonBridges;
        public int ChromosomeLength => _nonBridges.Count;
        public UndirectedGraph<BFNode, BFEdge> OriginalGraph => _originalGraph;
        public CoherencyManager(UndirectedGraph<BFNode, BFEdge> graph)
        {
            _originalGraph = graph;
            _indexToNonBridge = new Dictionary<int, BFEdge>();
            _nonBridgeToIndex = new Dictionary<BFEdge, int>();
            graph.InitNonBridgeChromosomeIndex();
            foreach (var item in graph.Edges)
            {
                _indexToNonBridge.Add(item.NonBridgeChromosomeIndex, item);
                _nonBridgeToIndex.Add(item, item.NonBridgeChromosomeIndex);
            }

            _uniqueBitRepresentations = new ConcurrentHashSet<BitArray>();

            _bridges = FindBridges.DoFindThem(graph);
            _nonBridges = graph.Edges.Where(x => !_bridges.Contains(x)).ToHashSet();
        }

        public bool IsUnique(BitArray bitArray)
        {
            return _uniqueBitRepresentations.Add(bitArray);
        }
        public BFEdge OriginalNonBridgeEdgeFromIndex(int index)
        {
            return _indexToNonBridge[index];
        }
        public int IndexFromOriginalNonBridgeEdge(BFEdge edge)
        {
            return _nonBridgeToIndex[edge];
        }
    }
}
