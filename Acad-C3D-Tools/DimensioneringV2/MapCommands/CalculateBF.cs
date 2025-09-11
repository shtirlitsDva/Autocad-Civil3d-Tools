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
    internal class CalculateBF
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

                    //Reset the results, must happen after precalculation of service lines
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

                            var nonbridges = FindBridges.FindNonBridges(subGraph);
                            foreach (var edge in FindBridges.DoFindThem(subGraph)) edge.OriginalEdge.PipeSegment.IsBridge = true;

                            Utils.prtDbg($"Idx: {index} N: {subGraph.VertexCount} E: {subGraph.EdgeCount}");

                            int timeToEnumerate = settings.TimeToSteinerTreeEnumeration;

                            var bfVM = new BruteForceGraphCalculationViewModel
                            {
                                // Some descriptive title
                                Title = $"Brute Force Subgraph #{index + 1}",
                                NodeCount = subGraph.VertexCount,
                                EdgeCount = subGraph.EdgeCount,
                                NonBridgesCount = nonbridges.Count.ToString(),
                                SteinerTreesFound = 0,       // will be set later
                                CalculatedTrees = 0,         // will increment as we go
                                Cost = 0,                    // will set once we know best
                                TimeToEnumerate = timeToEnumerate
                            };

                            bfVM.RemainingTime = timeToEnumerate;

                            dispatcher.Invoke(() =>
                            {
                                GeneticOptimizedReportingContext.VM.GraphCalculations.Add(bfVM);
                                bfVM.StartCountdown(dispatcher);
                            });

                            var stev3 = new SteinerTreesEnumeratorV3(
                                subGraph, terminals.ToHashSet(), TimeSpan.FromSeconds(timeToEnumerate));
                            var solutions = stev3.Enumerate();

                            dispatcher.Invoke(() =>
                            {
                                bfVM.ShowCountdownOverlay = false;
                                bfVM.SteinerTreesFound = solutions.Count;
                            });

                            var nodeFlags = metaGraph.NodeFlags[subGraph];

                            BFNode? rootNode = metaGraph.GetRootForSubgraph(subGraph);
                            if (rootNode == null)
                            {
                                Utils.prtDbg("Root node not found.");
                                throw new System.Exception("Root node not found.");
                            }

                            //If enumeration did not reach the time limit
                            //We can calculate directly
                            if (solutions.Count > 0)
                            {
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
                            {
                                //Solutions could not be enumerated -> too many non-bridges
                                //Here we will use poor man's Brute Force

                                UndirectedGraph<BFNode, BFEdge> seed = subGraph.CopyWithNewEdges();

                                bool optimizationContinues = true;
                                int optimizationCounter = 0;
                                while (optimizationContinues)
                                {
                                    optimizationCounter++;
                                    dispatcher.Invoke(() =>
                                    {
                                        bfVM.CalculatedTrees = optimizationCounter;
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
                                        bfVM.Cost = bestResult.cost;
                                    });
                                }

                                //Update the original graph with the results from the best result
                                foreach (var edge in seed.Edges)
                                {
                                    edge.PushAllResults();
                                }

                                //IEnumerable<AnalysisFeature> reprojected = 
                                //graphs.SelectMany(x => 
                                //ProjectionService.ReProjectFeatures(
                                //    x.Edges.Select(x => x.PipeSegment)
                                //    .Where(x => x.PipeDim == null), "EPSG:3857", "EPSG:25832"));

                                //AcContext.Current.Post(_ =>
                                //{
                                //    AutoCAD.MarkNullEdges.Mark(reprojected);
                                //}, null);
                            }
                        });
                    }
                });
            }
            catch (System.Exception ex)
            {
                Utils.prtDbg($"An error occurred during calculations: {ex.Message}");
                Utils.prtDbg(ex);
            }

            var graphs = DataService.Instance.Graphs;

            //Perform post processing
            foreach (var graph in graphs)
            {
                PressureAnalysisService.CalculateDifferentialLossAtClient(graph);
            }
        }
    }
}
