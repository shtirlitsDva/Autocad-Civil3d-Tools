using DimensioneringV2.BruteForceOptimization;
using DimensioneringV2.Common;
using DimensioneringV2.GraphFeatures;
using DimensioneringV2.GraphModel;
using DimensioneringV2.GraphUtilities;
using DimensioneringV2.MetaGraphForSubGraphs;
using DimensioneringV2.ResultCache;
using DimensioneringV2.Services;
using DimensioneringV2.Services.SubGraphs;
using DimensioneringV2.UI;

using DimensioneringV2.UI.CacheStatistics;
using DimensioneringV2.UI.GeneticOptimizedReporting;
using DimensioneringV2.UI.GeneticOptimizedReporting.Cards;
using NorsynHydraulicCalc;

using QuikGraph;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DimensioneringV2.MapCommands
{
    internal class CalculateBF
    {
        internal async Task Execute()
        {
            // Properties to sum from leaf nodes to root
            // Pattern 1: Connected → Supplied (different source/target properties)
            // Pattern 2: Same property (value pre-calculated at leaves, e.g., KarFlow*)
            List<SumProperty<BFEdge>> props =
            [
                new(f => f.NumberOfBuildingsConnected, (f, v) => f.NumberOfBuildingsSupplied = (int)v),
                new(f => f.NumberOfUnitsConnected, (f, v) => f.NumberOfUnitsSupplied = (int)v),
                new(f => f.HeatingDemandConnected, (f, v) => f.HeatingDemandSupplied = v),
                new(f => f.KarFlowHeatSupply, (f, v) => f.KarFlowHeatSupply = v),
                new(f => f.KarFlowBVSupply, (f, v) => f.KarFlowBVSupply = v),
                new(f => f.KarFlowHeatReturn, (f, v) => f.KarFlowHeatReturn = v),
                new(f => f.KarFlowBVReturn, (f, v) => f.KarFlowBVReturn = v),
            ];

            try
            {
                // Init the hydraulic calculation service using current settings
                HydraulicCalculationService.Initialize();

                // Setup cache statistics window (reuses existing if open)
                var cacheStatsVM = CacheStatisticsContext.EnsureWindowVisible();

                var reportingWindow = new GeneticOptimizedReporting();
                reportingWindow.Show();
                GeneticOptimizedReportingContext.VM = (GeneticOptimizedReportingViewModel)reportingWindow.DataContext;
                GeneticOptimizedReportingContext.VM.Dispatcher = reportingWindow.Dispatcher;

                var dispatcher = GeneticOptimizedReportingContext.VM.Dispatcher;
                var settings = HydraulicSettingsService.Instance.Settings;

                // Start statistics tracking
                cacheStatsVM.Start();

                await Task.Run(() =>
                {
                    var graphs = DataService.Instance.Graphs;

                    #region Setup result cache for distribution pipes
                    var extractors = new List<IKeyPropertyExtractor<BFEdge>>
                    {
                        KeyProperty<BFEdge>.Int(s => s.NumberOfBuildingsSupplied),
                        KeyProperty<BFEdge>.Int(s => s.NumberOfUnitsSupplied),
                        KeyProperty<BFEdge>.Double(s => s.KarFlowHeatSupply),
                        KeyProperty<BFEdge>.Double(s => s.KarFlowBVSupply),
                        KeyProperty<BFEdge>.Double(s => s.KarFlowHeatReturn),
                        KeyProperty<BFEdge>.Double(s => s.KarFlowBVReturn),
                    };

                    var cache = new HydraulicCalculationCache<BFEdge>(
                        edge => HydraulicCalculationService.Calc.CalculateDistributionSegment(edge),
                        settings.CacheResults,
                        extractors,
                        settings.CachePrecision,
                        CacheStatisticsContext.Statistics);
                    #endregion

                    // Reset results on AnalysisFeatures before calculation
                    foreach (var f in graphs.SelectMany(g => g.Edges.Select(e => e.PipeSegment)))
                        f.ResetHydraulicResults();

                    foreach (UndirectedGraph<NodeJunction, EdgePipeSegment> originalGraph in graphs)
                    {
                        // Copy to BFNode/BFEdge graph (DTO for performance)
                        UndirectedGraph<BFNode, BFEdge> graph = originalGraph.CopyToBF();

                        // Calculate service pipes (stikledninger) once - they're always leaf nodes
                        foreach (var edge in graph.Edges.Where(x => x.SegmentType == SegmentType.Stikledning))
                        {
                            var result = HydraulicCalculationService.Calc.CalculateClientSegment(edge);
                            edge.ApplyResult(result);
                        }

                        // Split network into subgraphs (islands of non-bridge edges)
                        var subGraphs = HydraulicCalculationsService.CreateSubGraphs(graph);

                        // Build metagraph to track subgraph relationships and data flow
                        var metaGraph = MetaGraphBuilder.BuildMetaGraph(subGraphs);

                        // Calculate sums from leaves to root via metagraph
                        var calculator = new CalculateMetaGraphRecursively(metaGraph);
                        calculator.CalculateBaseSumsForMetaGraph(props);

                        // Process each subgraph in parallel
                        Parallel.ForEach(subGraphs, (subGraph, state, index) =>
                        {
                            var terminals = metaGraph.GetTerminalsForSubgraph(subGraph);
                            var rootNode = metaGraph.GetRootForSubgraph(subGraph);

                            // Find bridges and non-bridges
                            var bridges = FindBridges.DoFindThem(subGraph);
                            var nonbridges = FindBridges.FindNonBridges(subGraph);

                            // Mark bridges on BFEdge (will be pushed to AnalysisFeature at the end)
                            foreach (var edge in bridges) edge.IsBridge = true;

                            Utils.prtDbg($"Idx: {index} N: {subGraph.VertexCount} E: {subGraph.EdgeCount}");

                            int timeToEnumerate = settings.TimeToSteinerTreeEnumeration;

                            var bfVM = new BruteForceGraphCalculationViewModel
                            {
                                Title = $"Brute Force Subgraph #{index + 1}",
                                NodeCount = subGraph.VertexCount,
                                EdgeCount = subGraph.EdgeCount,
                                NonBridgesCount = nonbridges.Count.ToString(),
                                SteinerTreesFound = 0,
                                CalculatedTrees = 0,
                                Cost = 0,
                                TimeToEnumerate = timeToEnumerate,
                                RemainingTime = timeToEnumerate
                            };

                            dispatcher.Invoke(() =>
                            {
                                GeneticOptimizedReportingContext.VM.GraphCalculations.Add(bfVM);
                                bfVM.StartCountdown(dispatcher);
                            });

                            // Enumerate Steiner trees within time limit
                            var stev3 = new SteinerTreesEnumeratorV3(
                                subGraph, terminals.ToHashSet(), TimeSpan.FromSeconds(timeToEnumerate));
                            var solutions = stev3.Enumerate();

                            dispatcher.Invoke(() =>
                            {
                                bfVM.ShowCountdownOverlay = false;
                                bfVM.SteinerTreesFound = solutions.Count;
                            });

                            UndirectedGraph<BFNode, BFEdge> bestGraph;

                            if (solutions.Count > 0)
                            {
                                // Evaluate all enumerated Steiner trees
                                bestGraph = EvaluateSteinerTrees(
                                    solutions, rootNode, metaGraph, props, cache, bfVM, dispatcher);
                            }
                            else
                            {
                                // Fallback: greedy optimization (remove non-bridges one by one)
                                bestGraph = GreedyOptimization(
                                    subGraph, rootNode, metaGraph, props, cache, bfVM, dispatcher);
                            }

                            // Push results from best graph back to AnalysisFeature
                            foreach (var edge in bestGraph.Edges)
                            {
                                edge.PushAllResults();
                            }
                        });
                    }
                });

                // Stop statistics tracking
                cacheStatsVM.Stop();

                // DEBUG: Uncomment to enable cache debug dump
                // To enable: also set CacheStatisticsContext.EnableDebugMode = true; at start of Execute()
                //if (CacheStatisticsContext.EnableDebugMode)
                //{
                //    var debugPath = CacheStatisticsContext.DebugOutputPath 
                //        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), 
                //            $"cache_debug_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
                //    CacheStatisticsContext.Statistics.DumpDebugEntriesToCsv(debugPath);
                //    Utils.prtDbg($"Cache debug dump saved to: {debugPath}");
                //}
            }
            catch (Exception ex)
            {
                Utils.prtDbg($"An error occurred during calculations: {ex.Message}");
                Utils.prtDbg(ex);
                
                // Stop statistics on error too
                CacheStatisticsContext.VM?.Stop();
            }

            // Post-processing
            var graphs = DataService.Instance.Graphs;
            foreach (var graph in graphs)
            {
                PressureAnalysisService.CalculateDifferentialLossAtClient(graph);
            }
        }

        /// <summary>
        /// Evaluates all enumerated Steiner trees and returns the one with lowest cost.
        /// </summary>
        private UndirectedGraph<BFNode, BFEdge> EvaluateSteinerTrees(
            List<List<BFEdge>> solutions,
            BFNode rootNode,
            MetaGraph<UndirectedGraph<BFNode, BFEdge>> metaGraph,
            List<SumProperty<BFEdge>> props,
            HydraulicCalculationCache<BFEdge> cache,
            BruteForceGraphCalculationViewModel bfVM,
            System.Windows.Threading.Dispatcher dispatcher)
        {
            var bag = new ConcurrentBag<(double cost, UndirectedGraph<BFNode, BFEdge> graph)>();
            int enumeratedCount = 0;

            Parallel.ForEach(solutions, solution =>
            {
                var st = new UndirectedGraph<BFNode, BFEdge>();
                foreach (var edge in solution) st.AddEdgeCopy(edge);

                // Calculate sums (with injected sums from child subgraphs) and hydraulics
                GraphSumCalculator.CalculateSums(st, rootNode, props, metaGraph.Sums);
                foreach (var edge in st.Edges)
                {
                    // Skip stikledninger - they're already calculated
                    if (edge.SegmentType == SegmentType.Stikledning) continue;

                    var result = cache.GetOrCalculate(edge);
                    edge.ApplyResult(result);
                }

                var cost = st.Edges.Sum(x => x.Price);
                bag.Add((cost, st));

                int count = Interlocked.Increment(ref enumeratedCount);
                dispatcher.Invoke(() => bfVM.CalculatedTrees = count);
            });

            var best = bag.MinBy(x => x.cost);

            dispatcher.Invoke(() =>
            {
                bfVM.CalculatedTrees = enumeratedCount;
                bfVM.Cost = best.cost;
            });

            return best.graph;
        }

        /// <summary>
        /// Greedy optimization: iteratively remove the most expensive non-bridge edge
        /// until only bridges remain (tree structure).
        /// </summary>
        private UndirectedGraph<BFNode, BFEdge> GreedyOptimization(
            UndirectedGraph<BFNode, BFEdge> subGraph,
            BFNode rootNode,
            MetaGraph<UndirectedGraph<BFNode, BFEdge>> metaGraph,
            List<SumProperty<BFEdge>> props,
            HydraulicCalculationCache<BFEdge> cache,
            BruteForceGraphCalculationViewModel bfVM,
            System.Windows.Threading.Dispatcher dispatcher)
        {
            var seed = subGraph.CopyWithNewEdges();
            int optimizationCounter = 0;

            while (true)
            {
                optimizationCounter++;
                dispatcher.Invoke(() => bfVM.CalculatedTrees = optimizationCounter);

                var bridges = FindBridges.DoFindThem(seed);

                // Stop when all edges are bridges (tree structure achieved)
                if (bridges.Count == seed.Edges.Count())
                    break;

                var nonBridges = seed.Edges.Where(x => !bridges.Contains(x)).ToList();
                var results = new ConcurrentBag<(UndirectedGraph<BFNode, BFEdge> graph, double cost)>();

                // Try removing each non-bridge and evaluate cost
                Parallel.ForEach(nonBridges, candidate =>                     
                {
                    var cGraph = seed.CopyWithNewEdges();
                    var cCandidate = cGraph.Edges.First(
                        x => x.Source == candidate.Source && x.Target == candidate.Target);
                    cGraph.RemoveEdge(cCandidate);

                    // Calculate sums (with injected sums from child subgraphs) and hydraulics
                    GraphSumCalculator.CalculateSums(cGraph, rootNode, props, metaGraph.Sums);

                    foreach (var edge in cGraph.Edges)
                    {
                        // Skip stikledninger - they're already calculated
                        if (edge.SegmentType == SegmentType.Stikledning) continue;

                        var result = cache.GetOrCalculate(edge);
                        edge.ApplyResult(result);
                    }

                    var cost = cGraph.Edges.Sum(x => x.Price);
                    results.Add((cGraph, cost));
                }
                );

                var bestResult = results.MinBy(x => x.cost);
                seed = bestResult.graph;

                dispatcher.Invoke(() => bfVM.Cost = bestResult.cost);
            }

            return seed;
        }
    }
}
