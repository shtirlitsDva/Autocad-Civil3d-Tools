using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Windows.Data;
using System.ComponentModel;
using AcAp = Autodesk.AutoCAD.ApplicationServices.Application;
using Mapsui.UI.Wpf;
using System.Collections.ObjectModel;
using DimensioneringV2.GraphFeatures;
using QuikGraph;
using DimensioneringV2.Services;
using Mapsui.Layers;
using Mapsui;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mapsui.Nts;
using Mapsui.Extensions;
using Mapsui.Providers;
using Mapsui.Tiling.Layers;
using Mapsui.Widgets;
using Mapsui.Tiling;
using Mapsui.Tiling.Fetcher;
using BruTile;
using BruTile.Web;
using BruTile.Predefined;
using BruTile.Cache;
using System.IO;

using utils = IntersectUtilities.UtilsCommon.Utils;
using DimensioneringV2.MapStyles;
using DimensioneringV2;
using ProjNet.CoordinateSystems.Transformations;
using ProjNet.CoordinateSystems;
using Autodesk.AutoCAD.Geometry;
using IntersectUtilities.UtilsCommon;
using NorsynHydraulicCalc.Pipes;
using DimensioneringV2.BruteForceOptimization;
using Autodesk.AutoCAD.Runtime;
using DimensioneringV2.GraphUtilities;
using DimensioneringV2.GraphModel;
using DimensioneringV2.Services.SubGraphs;
using System.Collections.Concurrent;
using DimensioneringV2.SteinerTreeProblem;
using System.Threading;
using Microsoft.Win32;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;

namespace DimensioneringV2.UI
{
    internal partial class MainWindowViewModel : ObservableObject
    {
        public IEnumerable<MapPropertyWrapper> MapProperties => GetMapProperties(typeof(AnalysisFeature));
        [ObservableProperty]
        private MapPropertyWrapper selectedMapPropertyWrapper;

        public MainWindowViewModel()
        {
            _dataService = DataService.Instance;
            _dataService.DataLoaded += OnDataLoadedFirstTime;
            _dataService.CalculationsFinishedEvent += OnCalculationsCompleted;
        }

        private IEnumerable<MapPropertyWrapper> GetMapProperties(Type type)
        {
            return type.GetProperties()
                .Where(prop => Attribute.IsDefined(prop, typeof(MapPropertyAttribute)))
                .Select(prop =>
                {
                    var attr = (MapPropertyAttribute)Attribute.GetCustomAttribute(prop, typeof(MapPropertyAttribute));
                    var description = attr.Property.GetDescription();
                    return new MapPropertyWrapper(attr.Property, description);
                });
        }

        partial void OnSelectedMapPropertyWrapperChanged(MapPropertyWrapper value)
        {
            if (value == null) return;
            _styleManager = new StyleManager(value.EnumValue);
            UpdateMap();
        }

        #region CollectFeaturesFromACCommand
        public RelayCommand CollectFeaturesCommand => new RelayCommand(CollectFeaturesExecute);

        private async void CollectFeaturesExecute()
        {
            var docs = AcAp.DocumentManager;
            var ed = docs.MdiActiveDocument.Editor;

            await docs.ExecuteInCommandContextAsync(
                async (obj) =>
                {
                    await ed.CommandAsync("DIM2MAPCOLLECTFEATURES");
                }, null
                );
        }
        #endregion

        #region PerformCalculationsSPDCommand
        public RelayCommand PerformCalculationsSPDCommand =>
            new(async () => await PerformCalculationsSPDExecuteAsync());

        private async Task PerformCalculationsSPDExecuteAsync()
        {
            try
            {
                await Task.Run(() => HydraulicCalculationsService.CalculateSPDijkstra(
                    new List<(Func<AnalysisFeature, dynamic> Getter, Action<AnalysisFeature, dynamic> Setter)>
                    {
                        (f => f.NumberOfBuildingsConnected, (f, v) => f.NumberOfBuildingsSupplied = v),
                        (f => f.NumberOfUnitsConnected, (f, v) => f.NumberOfUnitsSupplied = v),
                        (f => f.HeatingDemandConnected, (f, v) => f.HeatingDemandSupplied = v)
                    }
                    ));

                var graphs = _dataService.Graphs;

                //Perform post processing
                foreach (var graph in graphs)
                {
                    CriticalPathService.Calculate(graph);
                }
            }
            catch (System.Exception ex)
            {
                utils.prdDbg($"An error occurred during calculations: {ex.Message}");
                utils.prdDbg(ex);
            }
        }
        #endregion

        #region PerformCalculationsBFCommand
        public RelayCommand PerformCalculationsBFCommand =>
            new(async () => await PerformCalculationsBFExecuteAsync());
        private async Task PerformCalculationsBFExecuteAsync()
        {
            try
            {
                //Init the hydraulic calculation service using current settings
                HydraulicCalculationService.Initialize();

                var progressWindow = new BruteForceProgressWindow();
                progressWindow.Show();
                BruteForceProgressContext.VM = (BruteForceProgressViewModel)progressWindow.DataContext;
                BruteForceProgressContext.VM.Dispatcher = progressWindow.Dispatcher;

                await Task.Run(() => HydraulicCalculationsService.CalculateBFAnalysis(
                    new List<(Func<BFEdge, dynamic> Getter, Action<BFEdge, dynamic> Setter)>
                    {
                        (f => f.NumberOfBuildingsConnected, (f, v) => f.NumberOfBuildingsSupplied = v),
                        (f => f.NumberOfUnitsConnected, (f, v) => f.NumberOfUnitsSupplied = v),
                        (f => f.HeatingDemandConnected, (f, v) => f.HeatingDemandSupplied = v)
                    }
                    ));
            }
            catch (System.Exception ex)
            {
                utils.prdDbg($"An error occurred during calculations: {ex.Message}");
                utils.prdDbg(ex);
            }

            var graphs = _dataService.Graphs;

            //Perform post processing
            foreach (var graph in graphs)
            {
                CriticalPathService.Calculate(graph);
            }
        }
        #endregion

        #region PerformCalculationsGACommand
        public AsyncRelayCommand PerformCalculationsGACommand => new(PerformCalculationsGAExecuteAsync);

        private async Task PerformCalculationsGAExecuteAsync()
        {
            var props = new List<(Func<BFEdge, dynamic> Getter, Action<BFEdge, dynamic> Setter)>
            {
                (f => f.NumberOfBuildingsConnected, (f, v) => f.NumberOfBuildingsSupplied = v),
                (f => f.NumberOfUnitsConnected, (f, v) => f.NumberOfUnitsSupplied = v),
                (f => f.HeatingDemandConnected, (f, v) => f.HeatingDemandSupplied = v)
            };

            //Init the hydraulic calculation service using current settings
            HydraulicCalculationService.Initialize();

            try
            {
                var progressWindow = new GeneticReporting();
                progressWindow.Show();
                GeneticReportingContext.VM = (GeneticReportingViewModel)progressWindow.DataContext;
                GeneticReportingContext.VM.Dispatcher = progressWindow.Dispatcher;

                await Task.Run(() =>
                {
                    var graphs = _dataService.Graphs;

                    //Reset the results
                    foreach (var f in graphs.SelectMany(g => g.Edges.Select(e => e.PipeSegment))) f.ResetHydraulicResults();

                    foreach (UndirectedGraph<NodeJunction, EdgePipeSegment> graph in graphs)
                    {
                        HydraulicCalculationsService.CalculateGAAnalysis(
                            graph,
                            props,
                            (generation, fitness) =>
                            {
                                progressWindow.Dispatcher.Invoke(() =>
                                {
                                    GeneticReportingContext.VM.UpdatePlot(generation, fitness);
                                });
                            },
                            GeneticReportingContext.VM.CancellationToken
                            );
                    }
                });
            }
            catch (System.Exception ex)
            {
                utils.prdDbg($"An error occurred during calculations: {ex.Message}");
                utils.prdDbg(ex);
            }
        }
        #endregion

        #region ZoomToExtents
        public RelayCommand PerformZoomToExtents =>
            new RelayCommand(ZoomToExtents, () => true);

        private void ZoomToExtents()
        {
            var map = Mymap;
            if (map == null) return;

            var layer = map.Layers.FirstOrDefault(x => x.Name == "Features");
            if (layer == null) return;

            var extent = layer.Extent!.Grow(100);

            map.Navigator.ZoomToBox(extent);
        }
        #endregion

        #region SyncACWindow
        public RelayCommand SyncACWindowCommand =>
            new RelayCommand(SyncACWindow, () => true);
        private void SyncACWindow()
        {
            var vp = Mymap.Navigator.Viewport;
            var mapExtent = vp.ToExtent();
            var minX = mapExtent.MinX;
            var minY = mapExtent.MinY;
            var maxX = mapExtent.MaxX;
            var maxY = mapExtent.MaxY;

            var trans = new CoordinateTransformationFactory().CreateFromCoordinateSystems(
                ProjectedCoordinateSystem.WebMercator,
                ProjectedCoordinateSystem.WGS84_UTM(32, true));

            var minPT = trans.MathTransform.Transform(new double[] { minX, minY });
            var maxPT = trans.MathTransform.Transform(new double[] { maxX, maxY });

            var minPt = new Point3d(minPT[0], minPT[1], 0);
            var maxPt = new Point3d(maxPT[0], maxPT[1], 0);

            var docs = AcAp.DocumentManager;
            var ed = docs.MdiActiveDocument.Editor;

            ed.Zoom(new Autodesk.AutoCAD.DatabaseServices.Extents3d(minPt, maxPt));
        }
        #endregion

        #region Toggle Labels
        public RelayCommand PerformLabelToggle =>
            new RelayCommand(ToggleLabelStyles, () => true);
        private void ToggleLabelStyles()
        {
            _styleManager.Switch();
            UpdateMap();
        }
        #endregion

        #region Perform Pricecalc
        public RelayCommand PerformPriceCalc =>
            new RelayCommand(PriceCalc);

        private void PriceCalc()
        {
            var afs = Features.Cast<AnalysisFeature>();
            //var stik = afs.Where(x => !x.PipeDim.Equals(default(Dim)) && x.NumberOfBuildingsConnected == 1);
            //var fls = afs.Where(x => !x.PipeDim.Equals(default(Dim)) && x.NumberOfBuildingsConnected == 0);
            // Calculate data for service lines (stik)
            var stikTable = afs
                .Where(x => !x.PipeDim.Equals(default(Dim)) && x.NumberOfBuildingsConnected == 1)
                .GroupBy(x => x.PipeDim.DimName)
                .Select(g => new
                {
                    DimName = g.Key,
                    TotalLength = g.Sum(x => x.Length),
                    Price = g.Sum(x => x.Length * x.PipeDim.Price_m),
                    ServiceCount = g.Count(),
                    ServicePrice = g.Count() * g.First().PipeDim.Price_stk(NorsynHydraulicCalc.SegmentType.Stikledning)
                })
                .ToList();

            var stikTotal = new
            {
                TotalPrice = stikTable.Sum(row => row.Price),
                TotalServicePrice = stikTable.Sum(row => row.ServicePrice)
            };

            // Calculate data for supply lines (fls)
            var flsTable = afs
                .Where(x => !x.PipeDim.Equals(default(Dim)) && x.NumberOfBuildingsConnected == 0)
                .GroupBy(x => x.PipeDim.DimName)
                .Select(g => new
                {
                    DimName = g.Key,
                    TotalLength = g.Sum(x => x.Length),
                    Price = g.Sum(x => x.Length * x.PipeDim.Price_m)
                })
                .ToList();

            var flsTotal = new
            {
                TotalPrice = flsTable.Sum(row => row.Price)
            };

            var grandTotal = stikTotal.TotalPrice + stikTotal.TotalServicePrice + flsTotal.TotalPrice;

            PriceSummaryWindow window;
            try
            {
                window = new PriceSummaryWindow(stikTable, flsTable, grandTotal);
                window.Show();
            }
            catch (System.Exception ex)
            {
                utils.prdDbg(ex);
            }
        }
        #endregion

        #region PerformCalculationsGAOptimizedCommand
        public AsyncRelayCommand PerformCalculationsGAOptimizedCommand => new(PerformCalculationsGAOptimizedExecuteAsync);

        private async Task PerformCalculationsGAOptimizedExecuteAsync()
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

                await Task.Run(() =>
                {
                    var graphs = _dataService.Graphs;

                    //Reset the results
                    foreach (var f in graphs.SelectMany(g => g.Edges.Select(e => e.PipeSegment))) f.ResetHydraulicResults();

                    HashSet<string> dims = new HashSet<string>();
                    foreach (var graph in graphs)
                    {
                        foreach (var edge in graph.Edges)
                        {
                            dims.Add(edge.PipeSegment.PipeDim.ToString());
                        }
                    }

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

                        ////Temporary code to test the calculation of subgraphs
                        ////Test looks like passed
                        //Parallel.ForEach(graph.Edges, edge =>
                        //{
                        //    edge.PushBaseSums();
                        //});

                        Parallel.ForEach(subGraphs, (subGraph, state, index) =>
                        {
                            var terminals = metaGraph.GetTerminalsForSubgraph(subGraph);

                            var nonbridges = FindBridges.FindNonBridges(subGraph);

                            Utils.prtDbg($"Idx: {index} N: {subGraph.VertexCount} E: {subGraph.EdgeCount}");

                            if (nonbridges.Count <= 50)
                            {//Use bruteforce
                                var stev3 = new SteinerTreesEnumeratorV3(
                                    subGraph, terminals.ToHashSet(), TimeSpan.FromSeconds(300));
                                var solutions = stev3.Enumerate();

                                var bfVM = new BruteForceGraphCalculationViewModel
                                {
                                    // Some descriptive title
                                    Title = $"Brute Force Subgraph #{index + 1}",
                                    NodeCount = subGraph.VertexCount,
                                    EdgeCount = subGraph.EdgeCount,
                                    NonBridgesCount = nonbridges.Count.ToString(),
                                    SteinerTreesFound = solutions.Count,       // will be set later
                                    CalculatedTrees = 0,         // will increment as we go
                                    Cost = 0                     // will set once we know best
                                };

                                dispatcher.Invoke(() =>
                                {
                                    GeneticOptimizedReportingContext.VM.GraphCalculations.Add(bfVM);
                                });

                                var nodeFlags = metaGraph.NodeFlags[subGraph];

                                BFNode? rootNode = metaGraph.GetRootForSubgraph(subGraph);
                                if (rootNode == null)
                                {
                                    Utils.prtDbg("Root node not found.");
                                    throw new System.Exception("Root node not found.");
                                }

                                ConcurrentBag<(double result, UndirectedGraph<BFNode, BFEdge> graph)> bag = new();

                                int enumeratedCount = 0;

                                Parallel.ForEach(solutions, solution =>
                                {
                                    var st = new UndirectedGraph<BFNode, BFEdge>();
                                    foreach (var edge in solution)
                                        st.AddEdgeCopy(edge);

                                    //Calculate sums again for the subgraph
                                    var visited = new HashSet<BFNode>();
                                    CalculateSubgraphs.BFCalcBaseSums(st, rootNode, visited, metaGraph, props);
                                    HydraulicCalculationsService.BFCalcHydraulics(st);
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

                                BFNode? rootNode = metaGraph.GetRootForSubgraph(subGraph);
                                if (rootNode == null)
                                {
                                    Utils.prtDbg($"Root node not found for subgraph {index + 1}.");
                                    throw new System.Exception("Root node not found.");
                                }

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
                                        HydraulicCalculationsService.BFCalcHydraulics(cGraph);
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
                                    gaVM.CancellationToken
                                    );


                            }
                        });
                    }
                });

                var graphs = _dataService.Graphs;

                //Perform post processing
                foreach (var graph in graphs)
                {
                    CriticalPathService.Calculate(graph);
                }
            }
            catch (System.Exception ex)
            {
                utils.prdDbg($"An error occurred during calculations: {ex.Message}");
                utils.prdDbg(ex);
            }
        }
        #endregion

        #region Dim2ImportDims Command
        public AsyncRelayCommand Dim2ImportDimsCommand => new AsyncRelayCommand(Dim2ImportDims);
        private async Task Dim2ImportDims()
        {
            try
            {
                var graphs = _dataService.Graphs;

                AcContext.Current.Post(_ =>
                {
                    AutoCAD.Dim2WriteDims.Write(
                    graphs.SelectMany(x => x.Edges.Select(e => e.PipeSegment)));
                }, null);
            }
            catch (System.Exception ex)
            {
                utils.prdDbg($"An error occurred during calculations: {ex.Message}");
                utils.prdDbg(ex);
            }
        }
        #endregion

        #region SaveResultCommand
        public AsyncRelayCommand SaveResultCommand => new AsyncRelayCommand(SaveResult);
        private async Task SaveResult()
        {
            try
            {
                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Filter = "D2R Files (*.d2r)|*.d2r|All Files (*.*)|*.*",
                    DefaultExt = "d2r",
                    Title = "Save results",
                    AddExtension = true
                };

                string fileName;
                if (saveFileDialog.ShowDialog() == true)
                {
                    fileName = saveFileDialog.FileName;
                }
                else
                {
                    return;
                }

                if (string.IsNullOrEmpty(fileName)) return;

                if (File.Exists(fileName))
                {
                    MessageBoxResult result = MessageBox.Show(
                        "The file already exists. Do you want to overwrite it?",
                        "File already exists",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result != MessageBoxResult.Yes) return;
                }

                var graphs = _dataService.Graphs;

                var options = new JsonSerializerOptions();
                options.WriteIndented = true;
                options.NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals;
                options.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve;
                options.Converters.Add(new AnalysisFeatureJsonConverter());
                options.Converters.Add(new UndirectedGraphJsonConverter());
                options.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;

                string json = JsonSerializer.Serialize(graphs.ToArray(), options);
                File.WriteAllText(fileName, json);

                Utils.prtDbg($"Results saved to {fileName}");
            }
            catch (System.Exception ex)
            {
                Utils.prtDbg($"An error occurred during saving: {ex.Message}");
                Utils.prtDbg(ex);
            }
        }
        #endregion

        #region LoadResultCommand
        public AsyncRelayCommand LoadResultCommand => new AsyncRelayCommand(LoadResult);
        private async Task LoadResult()
        {
            try
            {
                OpenFileDialog openFileDialog = new OpenFileDialog
                {
                    Filter = "D2R Files (*.d2r)|*.d2r|All Files (*.*)|*.*",
                    DefaultExt = "d2r",
                    Title = "Open D2R File",
                    CheckFileExists = true // Ensures the user selects an existing file
                };

                string fileName;
                if (openFileDialog.ShowDialog() == true)
                {
                    fileName = openFileDialog.FileName;
                }
                else
                {
                    return;
                }

                if (string.IsNullOrEmpty(fileName)) return;

                if (!File.Exists(fileName))
                {
                    MessageBox.Show("The file does not exist.", "File not found", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var graphs = _dataService.Graphs;

                var options = new JsonSerializerOptions();
                options.WriteIndented = true;
                options.NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals;
                options.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve;
                options.Converters.Add(new AnalysisFeatureJsonConverter());
                options.Converters.Add(new UndirectedGraphJsonConverter());
                options.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
                var graphs2 = JsonSerializer.Deserialize<UndirectedGraph<NodeJunction, EdgePipeSegment>[]>(
                    File.ReadAllText(fileName), options);
                if (graphs2 == null) throw new System.Exception("Deserialization failed.");
                _dataService.LoadSavedResultsData(graphs2);

                Utils.prtDbg($"Results loaded from {fileName}");
            }
            catch (System.Exception ex)
            {
                Utils.prtDbg($"An error occurred during loading: {ex.Message}");
                Utils.prtDbg(ex);
            }
        }
        #endregion

        #region Write2Dwg Command
        public AsyncRelayCommand WriteToDwgCommand => new AsyncRelayCommand(WriteToDwg);
        private async Task WriteToDwg()
        {
            try
            {
                var graphs = _dataService.Graphs;

                IEnumerable<AnalysisFeature> reprojected = 
                    graphs.SelectMany(x => ProjectionService.ReProjectFeatures(
                        x.Edges.Select(x => x.PipeSegment), "EPSG:3857", "EPSG:25832"));

                AcContext.Current.Post(_ =>
                {
                    AutoCAD.Write2Dwg.Write(reprojected);
                }, null);
            }
            catch (System.Exception ex)
            {
                utils.prdDbg($"An error occurred during calculations: {ex.Message}");
                utils.prdDbg(ex);
            }
        }
        #endregion

        [ObservableProperty]
        private Map _mymap = new() { CRS = "EPSG:3857" };

        private StyleManager _styleManager;

        public ObservableCollection<IFeature> Features { get; private set; }

        private readonly DataService _dataService;

        private void OnDataLoadedFirstTime(object sender, EventArgs e)
        {
            // Update observable collections
            Features = new(_dataService!.Features.SelectMany(x => x));

            _styleManager = new StyleManager(MapPropertyEnum.Basic);
            CreateMapFirstTime();
        }

        private void OnCalculationsCompleted(object sender, EventArgs e)
        {
            Features = new(_dataService!.Features.SelectMany(x => x));

            SelectedMapPropertyWrapper = null;
            SelectedMapPropertyWrapper = MapProperties.First();
        }

        private static IPersistentCache<byte[]>? _defaultCache;
        private static BruTile.Attribution _stadiaAttribution = new("© Stadia Maps", "https://stadiamaps.com/");
        private void CreateMapFirstTime()
        {
            if (Mymap == null) return;

            _styleManager.CurrentStyle.ApplyStyle(Features);

            var provider = new MemoryProvider(Features)
            {
                CRS = "EPSG:3857"
            };

            Layer layer = new Layer
            {
                DataSource = provider,
                Name = "Features",
                IsMapInfoLayer = true
            };

            var extent = layer.Extent!.Grow(100);

            Mymap.Layers.Clear();

            //OSM map
            Mymap.Layers.Add(Mapsui.Tiling.OpenStreetMap.CreateTileLayer());

            #region Attempt to add Stadia map tiles -> success
            ////Stadia maps tiles
            //string userAgent =
            //    $"user-agent-of-{Path.GetFileNameWithoutExtension(
            //        System.AppDomain.CurrentDomain.FriendlyName)}";

            //var httpTileSource = new HttpTileSource(
            //    new GlobalSphericalMercator(),
            //    "https://tiles.stadiamaps.com/tiles/alidade_smooth_dark/{z}/{x}/{y}@2x.png",
            //    ["a", "b", "c"],
            //    name: "Stadia Maps",
            //    attribution: _stadiaAttribution,
            //    //configureHttpRequestMessage: (r) => r.Headers.TryAddWithoutValidation("User-Agent", userAgent),
            //    persistentCache: _defaultCache
            //    );

            //httpTileSource.AddHeader("User-Agent", userAgent);
            //httpTileSource.AddHeader("Stadia-Auth", "enter api key here");

            //var stadiaLayer = new TileLayer(httpTileSource) { Name = "Stadia" };

            //// Add the custom tile layer to the map
            //Mymap.Layers.Add(stadiaLayer);
            #endregion

            //Add the features layer
            Mymap.Layers.Add(layer);

            Mymap.Navigator.ZoomToBox(extent);
        }
        private void UpdateMap()
        {
            if (Mymap == null) return;

            _styleManager.CurrentStyle.ApplyStyle(Features);

            var provider = new MemoryProvider(Features)
            {
                CRS = "EPSG:3857"
            };

            Layer layer = new Layer
            {
                DataSource = provider,
                Name = "Features",
                IsMapInfoLayer = true
            };

            var exLayer = Mymap.Layers.FirstOrDefault(x => x.Name == "Features");
            if (exLayer != null)
            {
                Mymap.Layers.Remove(exLayer);
            }

            Mymap.Layers.Add(layer);
        }

        #region Popup setup

        [ObservableProperty]
        private bool isPopupOpen;

        [ObservableProperty]
        private string popupText = "";

        [ObservableProperty]
        private double popupX;

        [ObservableProperty]
        private double popupY;

        public ObservableCollection<PropertyItem> FeatureProperties { get; } = new();

        public void OnMapInfo(object? sender, MapInfoEventArgs e)
        {
            if (e.MapInfo?.Feature == null)
            {
                IsPopupOpen = false;
                return;
            }

            var infoFeature = e.MapInfo.Feature as IInfoForFeature;
            if (infoFeature == null)
            {
                IsPopupOpen = false;
                return;
            }

            var items = infoFeature.PropertiesToDataGrid();

            FeatureProperties.Clear();
            foreach (var item in items)
                FeatureProperties.Add(item);

            PopupX = e.MapInfo?.ScreenPosition?.X ?? 0.0;
            PopupY = e.MapInfo?.ScreenPosition?.Y ?? 0.0;
            IsPopupOpen = true;
        }
        #endregion

        internal class MapPropertyWrapper
        {
            public MapPropertyEnum EnumValue { get; }
            public string Description { get; }
            public MapPropertyWrapper(MapPropertyEnum enumValue, string description)
            {
                EnumValue = enumValue;
                Description = description;
            }
        }
    }
}