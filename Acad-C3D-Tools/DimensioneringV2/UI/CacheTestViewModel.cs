using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using DimensioneringV2.BruteForceOptimization;
using DimensioneringV2.Common;
using DimensioneringV2.GraphFeatures;
using DimensioneringV2.GraphModel;
using DimensioneringV2.GraphUtilities;
using DimensioneringV2.ResultCache;
using DimensioneringV2.Services;

using NorsynHydraulicCalc;

using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

using QuikGraph;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace DimensioneringV2.UI
{
    public partial class CacheTestViewModel : ObservableObject
    {
        private CancellationTokenSource? _cts;
        private readonly Dispatcher _dispatcher;

        // Custom format with . as thousands separator
        private static readonly NumberFormatInfo _thousandsDotFormat = new()
        {
            NumberGroupSeparator = ".",
            NumberDecimalSeparator = ",",
            NumberGroupSizes = [3]
        };

        private long _iterationsValue = 10_000_000;
        
        public string IterationsInput
        {
            get => _iterationsValue.ToString("N0", _thousandsDotFormat);
            set
            {
                // Remove separators (both . and ,) for parsing
                var cleanValue = value.Replace(".", "").Replace(",", "").Replace(" ", "");
                if (long.TryParse(cleanValue, out var parsed) && parsed > 0)
                {
                    _iterationsValue = parsed;
                }
                OnPropertyChanged();
            }
        }

        [ObservableProperty]
        private string currentPhase = "Ready";

        [ObservableProperty]
        private double progressPercent;

        [ObservableProperty]
        private long currentIteration;

        [ObservableProperty]
        private long totalIterations;

        [ObservableProperty]
        private string elapsedTime = "00:00:00";

        [ObservableProperty]
        private double withCacheTimeMs;

        [ObservableProperty]
        private double withCacheRate;

        [ObservableProperty]
        private double withoutCacheTimeMs;

        [ObservableProperty]
        private double withoutCacheRate;

        [ObservableProperty]
        private double speedup;

        [ObservableProperty]
        private long cacheHits;

        [ObservableProperty]
        private long cacheMisses;

        [ObservableProperty]
        private int testEdgeCount;

        [ObservableProperty]
        private int uniqueKeyCount;

        [ObservableProperty]
        private bool isRunning;

        [ObservableProperty]
        private bool canStart = true;

        // PlotModel fields - initialized in constructor
        [ObservableProperty]
        private PlotModel withCachePlotModel = null!;

        [ObservableProperty]
        private PlotModel withoutCachePlotModel = null!;

        private readonly LineSeries _withCacheSeries;
        private readonly LineSeries _withoutCacheSeries;

        public IRelayCommand RunTestCommand { get; }
        public IRelayCommand StopCommand { get; }

        public CacheTestViewModel()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;

            // Setup WITH cache plot
            _withCacheSeries = new LineSeries
            {
                Title = "Calc/s",
                Color = OxyColors.Green,
                MarkerType = MarkerType.None,
                StrokeThickness = 2
            };

            withCachePlotModel = new PlotModel { Title = "WITH CACHE - Calculations per Second over Time" };
            withCachePlotModel.Series.Add(_withCacheSeries);
            withCachePlotModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Time (s)",
                Minimum = 0
            });
            withCachePlotModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Calculations/second",
                Minimum = double.NaN // Auto-scale from min value
            });

            // Setup WITHOUT cache plot
            _withoutCacheSeries = new LineSeries
            {
                Title = "Calc/s",
                Color = OxyColors.Red,
                MarkerType = MarkerType.None,
                StrokeThickness = 2
            };

            withoutCachePlotModel = new PlotModel { Title = "WITHOUT CACHE - Calculations per Second over Time" };
            withoutCachePlotModel.Series.Add(_withoutCacheSeries);
            withoutCachePlotModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Time (s)",
                Minimum = 0
            });
            withoutCachePlotModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Calculations/second",
                Minimum = double.NaN // Auto-scale from min value
            });

            RunTestCommand = new RelayCommand(RunTest);
            StopCommand = new RelayCommand(Stop);
        }

        private async void RunTest()
        {
            var iterations = _iterationsValue;
            if (iterations <= 0)
            {
                MessageBox.Show("Please enter a valid number of iterations.", "Invalid Input",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var graphs = DataService.Instance.Graphs;
            if (graphs == null || !graphs.Any())
            {
                MessageBox.Show("No graphs loaded. Please load data first.", "Test Cache",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsRunning = true;
            CanStart = false;
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            // Reset UI
            ClearPlots();
            TotalIterations = iterations;
            CurrentIteration = 0;
            ProgressPercent = 0;
            WithCacheTimeMs = 0;
            WithCacheRate = 0;
            WithoutCacheTimeMs = 0;
            WithoutCacheRate = 0;
            Speedup = 0;
            CacheHits = 0;
            CacheMisses = 0;

            try
            {
                CurrentPhase = "Setting up...";

                // Init the hydraulic calculation service
                HydraulicCalculationService.Initialize();
                var settings = HydraulicSettingsService.Instance.Settings;

                // Find the largest graph
                var largestGraph = graphs.OrderByDescending(g => g.EdgeCount).First();
                UndirectedGraph<BFNode, BFEdge> graph = largestGraph.CopyToBF();

                CurrentPhase = "Calculating service pipes...";

                // Calculate service pipes (stikledninger) once
                foreach (var edge in graph.Edges.Where(x => x.SegmentType == SegmentType.Stikledning))
                {
                    var result = HydraulicCalculationService.Calc.CalculateClientSegment(edge);
                    edge.ApplyResult(result);
                }

                // Calculate sums from leaves to root
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

                var rootNode = graph.Vertices.FirstOrDefault(v => v.IsRootNode);
                if (rootNode == null)
                {
                    MessageBox.Show("No root node found in graph.", "Test Cache",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                CurrentPhase = "Calculating sums...";
                GraphSumCalculator.CalculateSums(graph, rootNode, props);

                // Filter edges: fordelingsledninger with BuildingsSupplied > 0
                var testEdges = graph.Edges
                    .Where(e => e.SegmentType == SegmentType.Fordelingsledning && e.NumberOfBuildingsSupplied > 0)
                    .ToList();

                if (testEdges.Count == 0)
                {
                    MessageBox.Show("No distribution pipes with buildings found.", "Test Cache",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                TestEdgeCount = testEdges.Count;

                // Setup extractors for cache
                var extractors = new List<IKeyPropertyExtractor<BFEdge>>
                {
                    KeyProperty<BFEdge>.Int(s => s.NumberOfBuildingsSupplied),
                    KeyProperty<BFEdge>.Int(s => s.NumberOfUnitsSupplied),
                    KeyProperty<BFEdge>.Double(s => s.KarFlowHeatSupply),
                    KeyProperty<BFEdge>.Double(s => s.KarFlowBVSupply),
                    KeyProperty<BFEdge>.Double(s => s.KarFlowHeatReturn),
                    KeyProperty<BFEdge>.Double(s => s.KarFlowBVReturn),
                };

                // Create caches
                var cacheEnabled = new HydraulicCalculationCache<BFEdge>(
                    edge => HydraulicCalculationService.Calc.CalculateDistributionSegment(edge),
                    cacheEnabled: true,
                    extractors,
                    settings.CachePrecision);

                var cacheDisabled = new HydraulicCalculationCache<BFEdge>(
                    edge => HydraulicCalculationService.Calc.CalculateDistributionSegment(edge),
                    cacheEnabled: false,
                    extractors,
                    settings.CachePrecision);

                // Warm up the cache
                CurrentPhase = "Warming up cache...";
                foreach (var edge in testEdges)
                {
                    cacheEnabled.GetOrCalculate(edge);
                }
                UniqueKeyCount = cacheEnabled.CachedCount;

                // ============ TEST WITH CACHE ============
                CurrentPhase = "Testing WITH CACHE...";

                var withCacheResult = await RunTestPhaseAsync(
                    testEdges, cacheEnabled, iterations, 0, 50, token,
                    (sampleTime, calcsPerSec) => _withCacheSeries.Points.Add(new DataPoint(sampleTime, calcsPerSec)),
                    isCacheEnabled: true);

                if (token.IsCancellationRequested)
                {
                    CurrentPhase = "Cancelled";
                    return;
                }

                WithCacheTimeMs = withCacheResult.ElapsedMs;
                WithCacheRate = iterations / (withCacheResult.ElapsedMs / 1000.0);
                CacheHits = withCacheResult.Hits;
                CacheMisses = withCacheResult.Misses;
                RefreshPlots();

                // ============ TEST WITHOUT CACHE ============
                CurrentPhase = "Testing WITHOUT CACHE...";

                var withoutCacheResult = await RunTestPhaseAsync(
                    testEdges, cacheDisabled, iterations, 50, 100, token,
                    (sampleTime, calcsPerSec) => _withoutCacheSeries.Points.Add(new DataPoint(sampleTime, calcsPerSec)),
                    isCacheEnabled: false);

                if (token.IsCancellationRequested)
                {
                    CurrentPhase = "Cancelled";
                    return;
                }

                WithoutCacheTimeMs = withoutCacheResult.ElapsedMs;
                WithoutCacheRate = iterations / (withoutCacheResult.ElapsedMs / 1000.0);
                RefreshPlots();

                // Calculate speedup
                Speedup = WithoutCacheTimeMs / WithCacheTimeMs;
                CurrentPhase = "Test Complete!";
                ProgressPercent = 100;
            }
            catch (OperationCanceledException)
            {
                CurrentPhase = "Cancelled";
            }
            catch (Exception ex)
            {
                CurrentPhase = $"Error: {ex.Message}";
                MessageBox.Show($"Error: {ex.Message}\n\n{ex.StackTrace}", "Test Cache - Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsRunning = false;
                CanStart = true;
                _cts?.Dispose();
                _cts = null;
            }
        }

        private record TestResult(double ElapsedMs, long Hits, long Misses);

        private async Task<TestResult> RunTestPhaseAsync(
            List<BFEdge> testEdges,
            HydraulicCalculationCache<BFEdge> cache,
            long iterations,
            double progressStart,
            double progressEnd,
            CancellationToken token,
            Action<double, double> addDataPoint,
            bool isCacheEnabled)
        {
            var result = await Task.Run(() =>
            {
                var stopwatch = Stopwatch.StartNew();
                var lastUpdateTime = stopwatch.Elapsed;
                long lastIterationAtUpdate = 0;
                int edgeCount = testEdges.Count;

                // For cache-enabled: all iterations after warmup are hits
                // For cache-disabled: all iterations are misses (no caching)
                long hits = isCacheEnabled ? iterations : 0;
                long misses = isCacheEnabled ? 0 : iterations;

                // Use shorter update interval (0.25s) for better graph resolution
                const double updateIntervalSeconds = 0.25;

                for (long i = 0; i < iterations; i++)
                {
                    if (token.IsCancellationRequested)
                        break;

                    var edge = testEdges[(int)(i % edgeCount)];

                    // Just call the cache - no expensive tracking in hot loop
                    cache.GetOrCalculate(edge);

                    // Update UI periodically
                    var elapsed = stopwatch.Elapsed;
                    if ((elapsed - lastUpdateTime).TotalSeconds >= updateIntervalSeconds)
                    {
                        var iterationsDelta = i - lastIterationAtUpdate;
                        var timeDelta = (elapsed - lastUpdateTime).TotalSeconds;
                        var calcsPerSecond = iterationsDelta / timeDelta;
                        var sampleTime = elapsed.TotalSeconds;

                        var progress = progressStart + (double)(i + 1) / iterations * (progressEnd - progressStart);
                        var currentIter = i + 1;
                        var elapsedStr = elapsed.ToString(@"hh\:mm\:ss");

                        // Update UI on dispatcher thread
                        _dispatcher.BeginInvoke(() =>
                        {
                            CurrentIteration = currentIter;
                            ProgressPercent = progress;
                            ElapsedTime = elapsedStr;
                            addDataPoint(sampleTime, calcsPerSecond);
                            RefreshPlots();
                        });

                        lastUpdateTime = elapsed;
                        lastIterationAtUpdate = i;
                    }
                }

                stopwatch.Stop();
                var totalElapsed = stopwatch.Elapsed;

                // Always add final data point
                if (lastIterationAtUpdate < iterations - 1)
                {
                    var finalIterationsDelta = iterations - lastIterationAtUpdate;
                    var finalTimeDelta = (totalElapsed - lastUpdateTime).TotalSeconds;
                    if (finalTimeDelta > 0)
                    {
                        var finalCalcsPerSecond = finalIterationsDelta / finalTimeDelta;
                        _dispatcher.BeginInvoke(() =>
                        {
                            addDataPoint(totalElapsed.TotalSeconds, finalCalcsPerSecond);
                            RefreshPlots();
                        });
                    }
                }

                return new TestResult(totalElapsed.TotalMilliseconds, hits, misses);
            }, token);

            return result;
        }

        private void Stop()
        {
            _cts?.Cancel();
        }

        public void RefreshPlots()
        {
            WithCachePlotModel.InvalidatePlot(true);
            WithoutCachePlotModel.InvalidatePlot(true);
        }

        public void ClearPlots()
        {
            _withCacheSeries.Points.Clear();
            _withoutCacheSeries.Points.Clear();
            RefreshPlots();
        }
    }
}
