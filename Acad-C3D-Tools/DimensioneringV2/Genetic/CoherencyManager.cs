using DimensioneringV2.BruteForceOptimization;
using DimensioneringV2.GraphModel;
using DimensioneringV2.Services;

using GeneticSharp;

using Mapsui.Utilities;

using NorsynHydraulicCalc.Pipes;

using QuikGraph;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
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
        
        private int _seeded = 0; // 0 = not seeded, 1 = seeded (thread-safe via Interlocked)

        // Graduated penalty support
        private readonly double _graduatedPenaltyUpperBound;

        /// <summary>
        /// Upper bound cost used for graduated penalty calculation.
        /// Calculated as total edge length * largest pipe price per meter.
        /// </summary>
        public double GraduatedPenaltyUpperBound => _graduatedPenaltyUpperBound;

        /// <summary>
        /// Total number of terminal nodes that must be connected.
        /// </summary>
        public int TotalTerminalCount => _terminals.Count;
        
        /// <summary>
        /// Thread-safe check-and-set for seeding. Returns true only for the first caller.
        /// </summary>
        internal bool TryClaimSeed()
        {
            // Atomically: if _seeded == 0, set to 1 and return true; else return false
            return Interlocked.CompareExchange(ref _seeded, 1, 0) == 0;
        }
        
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

            // Calculate graduated penalty upper bound if the setting is enabled
            if (GASettingsService.Instance.Settings.UseGraduatedPenalty)
            {
                // Sum all edge lengths
                double totalLength = subGraph.Edges.Sum(e => e.Length);

                // Get largest steel pipe price per meter
                var settings = HydraulicSettingsService.Instance.Settings;
                var steelPipe = new PipeSteel(settings.RuhedSteel);
                var largestDim = steelPipe.GetAllDimsSorted().Last();
                double largestPricePerMeter = largestDim.Price_m;

                // Upper bound = total length * largest price (an unreachably high cost)
                _graduatedPenaltyUpperBound = totalLength * largestPricePerMeter;
            }
            else
            {
                _graduatedPenaltyUpperBound = 0;
            }
        }

        public BFEdge OriginalNonBridgeEdgeFromIndex(int index)
        {
            return _indexToNonBridge[index];
        }

        /// <summary>
        /// Rebuilds a graph from chromosome genes using only IChromosome interface methods.
        /// Gene value 0 = edge is ON (included), 1 = edge is OFF (excluded).
        /// Works with any IChromosome implementation.
        /// </summary>
        public UndirectedGraph<BFNode, BFEdge> RebuildGraphFromChromosome(IChromosome chromosome)
        {
            var graph = new UndirectedGraph<BFNode, BFEdge>();

            // Add all vertices from original graph
            graph.AddVertexRange(_originalGraph.Vertices);

            // Add bridge edges (always included, not represented in chromosome)
            foreach (var edge in _bridges)
            {
                var newEdge = new BFEdge(edge);
                newEdge.NonBridgeChromosomeIndex = edge.NonBridgeChromosomeIndex;
                graph.AddEdge(newEdge);
            }

            // Add non-bridge edges based on gene values (using interface method)
            for (int i = 0; i < chromosome.Length; i++)
            {
                var gene = chromosome.GetGene(i);
                int geneValue = (int)gene.Value;

                // 0 = edge ON, 1 = edge OFF
                if (geneValue == 0)
                {
                    var originalEdge = OriginalNonBridgeEdgeFromIndex(i);
                    var newEdge = new BFEdge(originalEdge);
                    newEdge.NonBridgeChromosomeIndex = originalEdge.NonBridgeChromosomeIndex;
                    graph.AddEdge(newEdge);
                }
            }

            return graph;
        }
    }
}
