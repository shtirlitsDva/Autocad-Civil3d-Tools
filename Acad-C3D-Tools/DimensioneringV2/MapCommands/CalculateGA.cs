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

using NorsynHydraulicCalc;

using QuikGraph;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DimensioneringV2.MapCommands
{
    internal class CalculateGA
    {
        internal async Task Execute()
        {
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

                            // Mark bridges on BFEdge
                            foreach (var edge in bridges) edge.IsBridge = true;

                            Utils.prtDbg($"Idx: {index} N: {subGraph.VertexCount} E: {subGraph.EdgeCount} " +
                                $"NB: {nonbridges.Count}");

                            int timeToEnumerate = settings.TimeToSteinerTreeEnumeration;

                            var placeholderVM = new PlaceholderGraphCalculationViewModel
                            {
                                Title = $"GA/BF Subgraph #{index + 1}",
                                NodeCount = subGraph.VertexCount,
                                EdgeCount = subGraph.EdgeCount,
                                NonBridgesCount = nonbridges.Count.ToString(),
                                Cost = 0,
                                TimeToEnumerate = timeToEnumerate,
                                RemainingTime = timeToEnumerate
                            };

                            dispatcher.Invoke(() =>
                            {
                                GeneticOptimizedReportingContext.VM.GraphCalculations.Add(placeholderVM);
                                placeholderVM.StartCountdown(dispatcher);
                            });

                            // Try to enumerate Steiner trees within time limit
                            var stev3 = new SteinerTreesEnumeratorV3(
                                subGraph, terminals.ToHashSet(), TimeSpan.FromSeconds(timeToEnumerate));
                            var solutions = stev3.Enumerate();

                            if (solutions.Count == 0)
                            {
                                Utils.prtDbg($"SteinerEnumerator exceeded time limit for subgraph {index}! Using GA.");
                            }
                            else
                            {
                                Utils.prtDbg($"SteinerEnumerator found {solutions.Count} solutions for subgraph {index}. Using brute force.");
                            }

                            // Remove the countdown placeholder
                            dispatcher.Invoke(() =>
                            {
                                GeneticOptimizedReportingContext.VM.GraphCalculations.Remove(placeholderVM);
                            });

                            if (solutions.Count > 0)
                            {
                                // Use brute force evaluation of enumerated Steiner trees
                                var bestGraph = EvaluateSteinerTrees(
                                    solutions, rootNode, metaGraph, props, cache,
                                    nonbridges.Count, index, dispatcher);

                                // Push results from best graph back to AnalysisFeature
                                foreach (var edge in bestGraph.Edges)
                                {
                                    edge.PushAllResults();
                                }
                            }
                            else
                            {
                                // Use GA optimization
                                var bestGraph = RunGeneticOptimization(
                                    subGraph, rootNode, metaGraph, props, cache,
                                    nonbridges.Count, index, dispatcher);

                                // Push results from best graph back to AnalysisFeature
                                if (bestGraph != null)
                                {
                                    foreach (var edge in bestGraph.Edges)
                                    {
                                        edge.PushAllResults();
                                    }
                                }
                            }
                        });
                    }
                });

                // Stop statistics tracking
                cacheStatsVM.Stop();

                // Post-processing: Pressure profile analysis
                var graphs = DataService.Instance.Graphs;
                foreach (var graph in graphs)
                {
                    PressureAnalysisService.CalculateDifferentialLossAtClient(graph);
                }
            }
            catch (Exception ex)
            {
                Utils.prtDbg($"An error occurred during calculations: {ex.Message}");
                Utils.prtDbg(ex);

                // Stop statistics on error too
                CacheStatisticsContext.VM?.Stop();
            }

            Utils.prtDbg("Calculations completed.");
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
            int nonBridgesCount,
            long subgraphIndex,
            System.Windows.Threading.Dispatcher dispatcher)
        {
            var bfVM = new BruteForceGraphCalculationViewModel
            {
                Title = $"Brute Force Subgraph #{subgraphIndex + 1}",
                NodeCount = 0,
                EdgeCount = 0,
                NonBridgesCount = nonBridgesCount.ToString(),
                SteinerTreesFound = solutions.Count,
                CalculatedTrees = 0,
                Cost = 0,
                TimeToEnumerate = 0
            };

            dispatcher.Invoke(() =>
            {
                GeneticOptimizedReportingContext.VM.GraphCalculations.Add(bfVM);
                bfVM.ShowCountdownOverlay = false;
            });

            var bag = new ConcurrentBag<(double cost, UndirectedGraph<BFNode, BFEdge> graph)>();
            int enumeratedCount = 0;

            Parallel.ForEach(solutions, solution =>
            {
                var st = new UndirectedGraph<BFNode, BFEdge>();
                foreach (var edge in solution) st.AddEdgeCopy(edge);

                // Calculate sums (with injected sums from child subgraphs)
                GraphSumCalculator.CalculateSums(st, rootNode, props, metaGraph.Sums);

                // Calculate hydraulics for distribution pipes
                foreach (var edge in st.Edges)
                {
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
        /// Runs genetic algorithm optimization when Steiner tree enumeration times out.
        /// First does greedy optimization, then refines with GA.
        /// </summary>
        private UndirectedGraph<BFNode, BFEdge>? RunGeneticOptimization(
            UndirectedGraph<BFNode, BFEdge> subGraph,
            BFNode rootNode,
            MetaGraph<UndirectedGraph<BFNode, BFEdge>> metaGraph,
            List<SumProperty<BFEdge>> props,
            HydraulicCalculationCache<BFEdge> cache,
            int nonBridgesCount,
            long subgraphIndex,
            System.Windows.Threading.Dispatcher dispatcher)
        {
            var gaVM = new GeneticAlgorithmCalculationViewModel
            {
                Title = $"Genetic Algorithm Subgraph #{subgraphIndex + 1}",
                NodeCount = subGraph.VertexCount,
                EdgeCount = subGraph.EdgeCount,
                CurrentGeneration = 0,
                GenerationsSinceLastUpdate = 0,
                Cost = 0,
            };

            dispatcher.Invoke(() =>
            {
                GeneticOptimizedReportingContext.VM.GraphCalculations.Add(gaVM);
            });

            // Phase 1: Greedy optimization (remove non-bridges one by one)
            var seed = GreedyOptimization(subGraph, rootNode, metaGraph, props, cache, gaVM, dispatcher);

            // Phase 2: Genetic algorithm refinement
            var result = HydraulicCalculationsService.CalculateOptimizedGAAnalysis(
                metaGraph,
                subGraph,
                seed,
                props,
                gaVM,
                gaVM.CancellationToken,
                cache);

            return result;
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
            GeneticAlgorithmCalculationViewModel gaVM,
            System.Windows.Threading.Dispatcher dispatcher)
        {
            var seed = subGraph.CopyWithNewEdges();
            int optimizationCounter = 0;

            while (true)
            {
                if (gaVM.StopRequested) break;

                optimizationCounter++;
                dispatcher.Invoke(() => gaVM.BruteForceCount = optimizationCounter);

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

                    // Calculate sums (with injected sums from child subgraphs)
                    GraphSumCalculator.CalculateSums(cGraph, rootNode, props, metaGraph.Sums);

                    // Calculate hydraulics for distribution pipes
                    foreach (var edge in cGraph.Edges)
                    {
                        if (edge.SegmentType == SegmentType.Stikledning) continue;
                        var result = cache.GetOrCalculate(edge);
                        edge.ApplyResult(result);
                    }

                    var cost = cGraph.Edges.Sum(x => x.Price);
                    results.Add((cGraph, cost));
                });

                var bestResult = results.MinBy(x => x.cost);
                seed = bestResult.graph;

                dispatcher.Invoke(() => gaVM.Cost = bestResult.cost);
            }

            return seed;
        }
    }
}
