using DimensioneringV2.BruteForceOptimization;
using DimensioneringV2.GraphFeatures;
using DimensioneringV2.GraphModel;
using DimensioneringV2.GraphUtilities;
using DimensioneringV2.ResultCache;
using DimensioneringV2.Services;
using DimensioneringV2.Services.SubGraphs;
using DimensioneringV2.UI;

using QuikGraph;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DimensioneringV2.MapCommands
{
    internal class CalculateGA
    {
        internal async Task Execute()
        {
            var props = new List<(Func<BFEdge, dynamic> Getter, Action<BFEdge, dynamic> Setter)>
            {
                (f => f.NumberOfBuildingsConnected, (f, v) => f.NumberOfBuildingsSupplied = v),
                (f => f.NumberOfUnitsConnected, (f, v) => f.NumberOfUnitsSupplied = v),
                (f => f.HeatingDemandConnected, (f, v) => f.HeatingDemandSupplied = v)
            };

            try
            {
                //Init the hydraulic calculation service using current settings
                HydraulicCalculationService.Initialize();

                var reportingWindow = new GeneticOptimizedReporting();
                reportingWindow.Show();
                GeneticOptimizedReportingContext.VM = (GeneticOptimizedReportingViewModel)reportingWindow.DataContext;
                GeneticOptimizedReportingContext.VM.Dispatcher = reportingWindow.Dispatcher;

                var dispatcher = GeneticOptimizedReportingContext.VM.Dispatcher;

                var settings = HydraulicSettingsService.Instance.Settings;

                await Task.Run(() =>
                {
                    var graphs = DataService.Instance.Graphs;

                    #region Setup result cache and precalculate service lines
                    var cache = new HydraulicCalculationCache(
                        HydraulicCalculationService.Calc.CalculateHydraulicSegment,
                        HydraulicSettingsService.Instance.Settings.CacheResults,
                        HydraulicSettingsService.Instance.Settings.CachePrecision
                        );
                    cache.PrecalculateServicePipes(
                        graphs
                        .SelectMany(g => g.Edges.Select(e => e.PipeSegment))
                        .Where(x => x.SegmentType == NorsynHydraulicCalc.SegmentType.Stikledning));
                    #endregion

                    //Reset the results after the precalculation of service lines
                    foreach (var f in graphs.SelectMany(g => g.Edges.Select(e => e.PipeSegment))) f.ResetHydraulicResults();

                    foreach (UndirectedGraph<NodeJunction, EdgePipeSegment> originalGraph in graphs)
                    {
                        //First prepare calculation graph
                        UndirectedGraph<BFNode, BFEdge> graph = originalGraph.CopyToBF();

                        //Split the network into subgraphs
                        var subGraphs = HydraulicCalculationsService.CreateSubGraphs(graph);

                        //Build metagraph for the subgraphs
                        var metaGraph = MetaGraphBuilder.BuildMetaGraph(subGraphs);

                        //Calculate sums of calculation input properties
                        //We can use SPDijkstra to calculate the sums here
                        //As we are only interested in the sums for bridge nodes
                        //because when the network is split into subgraphs
                        //it will read the bridge nodes and variance will only
                        //happen at non-bridge nodes

                        var c = new CalculateMetaGraphRecursively(metaGraph);
                        c.CalculateBaseSumsForMetaGraph(props);

                        Parallel.ForEach(subGraphs, (subGraph, state, index) =>
                        {
                            var terminals = metaGraph.GetTerminalsForSubgraph(subGraph);
                            var rootNode = metaGraph.GetRootForSubgraph(subGraph);

                            var nonbridges = FindBridges.FindNonBridges(subGraph);
                            foreach (var edge in FindBridges.DoFindThem(subGraph)) edge.OriginalEdge.PipeSegment.IsBridge = true;

                            Utils.prtDbg($"Idx: {index} N: {subGraph.VertexCount} E: {subGraph.EdgeCount} " +
                                $"NB: {nonbridges.Count}");

                            int timeToEnumerate = settings.TimeToSteinerTreeEnumeration;

                            var placeholderVM = new PlaceholderGraphCalculationViewModel
                            {
                                // Some descriptive title
                                Title = $"Brute Force Subgraph #{index + 1}",
                                NodeCount = subGraph.VertexCount,
                                EdgeCount = subGraph.EdgeCount,
                                NonBridgesCount = nonbridges.Count.ToString(),
                                Cost = 0,                    // will set once we know best
                                TimeToEnumerate = timeToEnumerate
                            };

                            placeholderVM.RemainingTime = timeToEnumerate;

                            dispatcher.Invoke(() =>
                            {
                                GeneticOptimizedReportingContext.VM.GraphCalculations.Add(placeholderVM);
                                placeholderVM.StartCountdown(dispatcher);
                            });

                            var stev3 = new SteinerTreesEnumeratorV3(
                                    subGraph, terminals.ToHashSet(), TimeSpan.FromSeconds(15));
                            var solutions = stev3.Enumerate();

                            if (solutions.Count == 0)
                            {
                                Utils.prtDbg($"SteinerEnumerator exceeded time limit for subgraph {index}!\n" +
                                    $"Using GA.");
                                //throw new System.Exception($"SteinerEnumerator exceeded time limit for subgraph {index}!" +
                                //    $"\nSOLUTION INCOMPLETE!");
                            }
                            else
                            {
                                Utils.prtDbg($"SteinerEnumerator found {solutions.Count} solutions for subgraph {index}." +
                                    $"\nUsing brute force.");
                            }

                            //Remove the countdown placeholder
                            dispatcher.Invoke(() =>
                            { GeneticOptimizedReportingContext.VM.GraphCalculations.Remove(placeholderVM); });

                            if (solutions.Count > 0)
                            {//Use bruteforce
                                var bfVM = new BruteForceGraphCalculationViewModel
                                {
                                    // Some descriptive title
                                    Title = $"Brute Force Subgraph #{index + 1}",
                                    NodeCount = subGraph.VertexCount,
                                    EdgeCount = subGraph.EdgeCount,
                                    NonBridgesCount = nonbridges.Count.ToString(),
                                    SteinerTreesFound = solutions.Count,       // will be set later
                                    CalculatedTrees = 0,         // will increment as we go
                                    Cost = 0,                    // will set once we know best
                                    TimeToEnumerate = 0
                                };

                                dispatcher.Invoke(() =>
                                {
                                    GeneticOptimizedReportingContext.VM.GraphCalculations.Add(bfVM);
                                    bfVM.ShowCountdownOverlay = false;
                                    bfVM.SteinerTreesFound = solutions.Count;
                                });

                                var nodeFlags = metaGraph.NodeFlags[subGraph];

                                ConcurrentBag<(double result, UndirectedGraph<BFNode, BFEdge> graph)> bag = new();

                                int enumeratedCount = 0;

                                Parallel.ForEach(solutions, solution =>
                                {
                                    var st = new UndirectedGraph<BFNode, BFEdge>();
                                    foreach (var edge in solution) st.AddEdgeCopy(edge);

                                    //Calculate sums again for the subgraph
                                    var visited = new HashSet<BFNode>();
                                    CalculateSubgraphs.BFCalcBaseSums(st, rootNode, visited, metaGraph, props);
                                    HydraulicCalculationsService.BFCalcHydraulics(st, cache);
                                    var result = st.Edges.Sum(x => x.Price);

                                    bag.Add((result, st));

                                    Interlocked.Increment(ref enumeratedCount);

                                    int countCopy = enumeratedCount;

                                    dispatcher.Invoke(() =>
                                    {
                                        bfVM.CalculatedTrees = countCopy;
                                    });
                                });

                                var best = bag.MinBy(x => x.result);

                                dispatcher.Invoke(() =>
                                {
                                    bfVM.CalculatedTrees = enumeratedCount;
                                    bfVM.Cost = best.result;
                                });

                                //Update the original graph with the results from the best result
                                foreach (var edge in best.graph.Edges)
                                {
                                    edge.PushAllResults();
                                }
                            }
                            else
                            {//Use GA
                                var gaVM = new GeneticAlgorithmCalculationViewModel
                                {
                                    // Some descriptive title
                                    Title = $"Genetic Algorithm Subgraph #{index + 1}",
                                    NodeCount = subGraph.VertexCount,
                                    EdgeCount = subGraph.EdgeCount,
                                    CurrentGeneration = 0,         // will increment as we go
                                    GenerationsSinceLastUpdate = 0, // will increment as we go
                                    Cost = 0,                     // will set once we know best
                                };

                                dispatcher.Invoke(() =>
                                {
                                    GeneticOptimizedReportingContext.VM.GraphCalculations.Add(gaVM);
                                });

                                UndirectedGraph<BFNode, BFEdge> seed = subGraph.CopyWithNewEdges();

                                bool optimizationContinues = true;
                                int optimizationCounter = 0;
                                while (optimizationContinues)
                                {
                                    if (gaVM.StopRequested) break;

                                    optimizationCounter++;
                                    dispatcher.Invoke(() =>
                                    {
                                        gaVM.BruteForceCount = optimizationCounter;
                                    });

                                    var bridges = FindBridges.DoFindThem(seed);
                                    if (bridges.Count == seed.Edges.Count())
                                    {
                                        optimizationContinues = false;
                                        break;
                                    }

                                    var nonBridges = seed.Edges.Where(x => !bridges.Contains(x)).ToList();

                                    var results = new ConcurrentBag<(UndirectedGraph<BFNode, BFEdge> graph, double cost)>();

                                    int counter = 0;
                                    Parallel.ForEach(nonBridges, candidate =>
                                    {
                                        counter++;
                                        var cGraph = seed.CopyWithNewEdges();
                                        var cCandidate = cGraph.Edges.First(
                                            x => x.Source == candidate.Source &&
                                            x.Target == candidate.Target);
                                        cGraph.RemoveEdge(cCandidate);

                                        //Calculate sums again for the subgraph
                                        var visited = new HashSet<BFNode>();
                                        CalculateSubgraphs.BFCalcBaseSums(cGraph, rootNode, visited, metaGraph, props);
                                        HydraulicCalculationsService.BFCalcHydraulics(cGraph, cache);
                                        var result = cGraph.Edges.Sum(x => x.Price);

                                        results.Add((cGraph, result));
                                    });

                                    var bestResult = results.MinBy(x => x.cost);
                                    seed = bestResult.graph;

                                    dispatcher.Invoke(() =>
                                    {
                                        gaVM.Cost = bestResult.cost;
                                    });
                                }

                                HydraulicCalculationsService.CalculateOptimizedGAAnalysis(
                                    metaGraph,
                                    subGraph,
                                    seed,
                                    props,
                                    gaVM,
                                    gaVM.CancellationToken,
                                    cache
                                    );
                            }
                        });
                    }
                });

                //Perform post processing
                //Pressure profile analysis
                var graphs = DataService.Instance.Graphs;

                foreach (var graph in graphs)
                {
                    PressureAnalysisService.CalculateDifferentialLossAtClient(graph);
                }

            }
            catch (System.Exception ex)
            {
                Utils.prtDbg($"An error occurred during calculations: {ex.Message}");
                Utils.prtDbg(ex);
            }

            Utils.prtDbg("Calculations completed.");
        }
    }
}
