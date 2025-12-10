using CommunityToolkit.Mvvm.ComponentModel;

using DimensioneringV2.ResultCache;

using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

using OxyLegend = OxyPlot.Legends.Legend;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Threading;

namespace DimensioneringV2.UI
{
    public partial class CacheStatisticsViewModel : ObservableObject
    {
        private readonly CacheStatistics _statistics;
        private readonly DispatcherTimer _timer;
        private readonly List<(double time, long hits, long misses)> _dataPoints = new();
        private long _lastHits;
        private long _lastMisses;
        private DateTime _lastSampleTime;
        private ThreadPoolMonitor? _threadPoolMonitor;

        [ObservableProperty]
        private long hits;

        [ObservableProperty]
        private long misses;

        [ObservableProperty]
        private long totalCalculations;

        [ObservableProperty]
        private long uniqueEntriesStored;

        [ObservableProperty]
        private double hitRate;

        [ObservableProperty]
        private double calculationsPerSecond;

        [ObservableProperty]
        private string elapsedTime = "00:00:00";

        [ObservableProperty]
        private bool isRunning;

        [ObservableProperty]
        private PlotModel hitsMissesPlotModel;

        [ObservableProperty]
        private PlotModel calcPerSecondPlotModel;

        // Thread pool monitoring
        [ObservableProperty]
        private int busyWorkerThreads;

        [ObservableProperty]
        private int maxWorkerThreads;

        [ObservableProperty]
        private int peakBusyWorkers;

        [ObservableProperty]
        private int starvationEvents;

        [ObservableProperty]
        private double workerUtilization;

        [ObservableProperty]
        private bool isThreadPoolStressed;

        [ObservableProperty]
        private PlotModel threadPoolPlotModel = null!;

        private readonly LineSeries _hitsSeries;
        private readonly LineSeries _missesSeries;
        private readonly LineSeries _calcPerSecondSeries;
        private readonly LineSeries _threadPoolSeries;

        public CacheStatisticsViewModel(CacheStatistics statistics)
        {
            _statistics = statistics;

            // Setup Hits/Misses plot
            hitsMissesPlotModel = new PlotModel { Title = "Cache Hits vs Misses" };
            
            _hitsSeries = new LineSeries 
            { 
                Title = "Hits", 
                Color = OxyColors.Green,
                MarkerType = MarkerType.None 
            };
            _missesSeries = new LineSeries 
            { 
                Title = "Misses", 
                Color = OxyColors.Red,
                MarkerType = MarkerType.None 
            };

            hitsMissesPlotModel.Series.Add(_hitsSeries);
            hitsMissesPlotModel.Series.Add(_missesSeries);

            var xAxisHitsMisses = new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Time (s)",
                Minimum = 0
            };
            var yAxisHitsMisses = new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Count",
                Minimum = 0
            };
            hitsMissesPlotModel.Axes.Add(xAxisHitsMisses);
            hitsMissesPlotModel.Axes.Add(yAxisHitsMisses);
            hitsMissesPlotModel.Legends.Add(new OxyLegend { LegendPosition = OxyPlot.Legends.LegendPosition.TopLeft });

            // Setup Calculations per second plot
            calcPerSecondPlotModel = new PlotModel { Title = "Calculations per Second" };
            
            _calcPerSecondSeries = new LineSeries 
            { 
                Title = "Calc/s", 
                Color = OxyColors.Blue,
                MarkerType = MarkerType.None 
            };

            calcPerSecondPlotModel.Series.Add(_calcPerSecondSeries);

            var xAxisCalcPerSec = new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Time (s)",
                Minimum = 0
            };
            var yAxisCalcPerSec = new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Calculations/s",
                Minimum = 0
            };
            calcPerSecondPlotModel.Axes.Add(xAxisCalcPerSec);
            calcPerSecondPlotModel.Axes.Add(yAxisCalcPerSec);

            // Setup Thread Pool utilization plot
            threadPoolPlotModel = new PlotModel { Title = "Thread Pool Utilization" };
            
            _threadPoolSeries = new LineSeries 
            { 
                Title = "Busy Workers", 
                Color = OxyColors.Orange,
                MarkerType = MarkerType.None 
            };

            threadPoolPlotModel.Series.Add(_threadPoolSeries);

            var xAxisThreadPool = new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Time (s)",
                Minimum = 0
            };
            var yAxisThreadPool = new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Threads",
                Minimum = 0
            };
            threadPoolPlotModel.Axes.Add(xAxisThreadPool);
            threadPoolPlotModel.Axes.Add(yAxisThreadPool);

            // Setup timer for periodic updates
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _timer.Tick += Timer_Tick;

            _lastSampleTime = DateTime.Now;

            // Get initial thread pool info
            ThreadPool.GetMaxThreads(out int maxW, out _);
            MaxWorkerThreads = maxW;
        }

        public void Start()
        {
            _statistics.Start();
            _dataPoints.Clear();
            _hitsSeries.Points.Clear();
            _missesSeries.Points.Clear();
            _calcPerSecondSeries.Points.Clear();
            _threadPoolSeries.Points.Clear();
            _lastHits = 0;
            _lastMisses = 0;
            _lastSampleTime = DateTime.Now;
            IsRunning = true;
            
            // Reset thread pool stats
            PeakBusyWorkers = 0;
            StarvationEvents = 0;
            IsThreadPoolStressed = false;
            
            // Start thread pool monitoring
            _threadPoolMonitor?.Dispose();
            _threadPoolMonitor = new ThreadPoolMonitor(OnThreadPoolUpdate, 250);
            _threadPoolMonitor.Start();
            
            _timer.Start();
            UpdateFromSnapshot(_statistics.GetSnapshot());
        }

        public void Stop()
        {
            _statistics.Stop();
            _timer.Stop();
            _threadPoolMonitor?.Stop();
            _threadPoolMonitor?.Dispose();
            _threadPoolMonitor = null;
            IsRunning = false;
            UpdateFromSnapshot(_statistics.GetSnapshot());
        }

        private void OnThreadPoolUpdate(ThreadPoolSnapshot snapshot)
        {
            BusyWorkerThreads = snapshot.BusyWorkerThreads;
            MaxWorkerThreads = snapshot.MaxWorkerThreads;
            PeakBusyWorkers = snapshot.PeakBusyWorkers;
            StarvationEvents = snapshot.StarvationEvents;
            WorkerUtilization = snapshot.WorkerUtilization;
            IsThreadPoolStressed = snapshot.IsUnderStress;

            // Add data point to graph
            var elapsed = _statistics.ElapsedTime.TotalSeconds;
            _threadPoolSeries.Points.Add(new DataPoint(elapsed, snapshot.BusyWorkerThreads));
            ThreadPoolPlotModel.InvalidatePlot(true);
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            var snapshot = _statistics.GetSnapshot();
            UpdateFromSnapshot(snapshot);
            AddDataPoint(snapshot);
        }

        private void UpdateFromSnapshot(CacheStatisticsSnapshot snapshot)
        {
            Hits = snapshot.Hits;
            Misses = snapshot.Misses;
            TotalCalculations = snapshot.TotalCalculations;
            UniqueEntriesStored = snapshot.UniqueEntriesStored;
            HitRate = snapshot.HitRate;
            CalculationsPerSecond = snapshot.CalculationsPerSecond;
            ElapsedTime = snapshot.ElapsedTime.ToString(@"hh\:mm\:ss\.ff");
        }

        private void AddDataPoint(CacheStatisticsSnapshot snapshot)
        {
            var elapsed = snapshot.ElapsedTime.TotalSeconds;
            
            // Add cumulative hits/misses
            _hitsSeries.Points.Add(new DataPoint(elapsed, snapshot.Hits));
            _missesSeries.Points.Add(new DataPoint(elapsed, snapshot.Misses));

            // Calculate instantaneous calculations per second
            var now = DateTime.Now;
            var timeDelta = (now - _lastSampleTime).TotalSeconds;
            if (timeDelta > 0)
            {
                var hitsDelta = snapshot.Hits - _lastHits;
                var missesDelta = snapshot.Misses - _lastMisses;
                var calcDelta = hitsDelta + missesDelta;
                var instantCalcPerSec = calcDelta / timeDelta;

                _calcPerSecondSeries.Points.Add(new DataPoint(elapsed, instantCalcPerSec));
            }

            _lastHits = snapshot.Hits;
            _lastMisses = snapshot.Misses;
            _lastSampleTime = now;

            // Refresh plots
            HitsMissesPlotModel.InvalidatePlot(true);
            CalcPerSecondPlotModel.InvalidatePlot(true);
        }

        public void Reset()
        {
            _hitsSeries.Points.Clear();
            _missesSeries.Points.Clear();
            _calcPerSecondSeries.Points.Clear();
            _threadPoolSeries.Points.Clear();
            _dataPoints.Clear();
            Hits = 0;
            Misses = 0;
            TotalCalculations = 0;
            UniqueEntriesStored = 0;
            HitRate = 0;
            CalculationsPerSecond = 0;
            ElapsedTime = "00:00:00";
            BusyWorkerThreads = 0;
            PeakBusyWorkers = 0;
            StarvationEvents = 0;
            WorkerUtilization = 0;
            IsThreadPoolStressed = false;
            HitsMissesPlotModel.InvalidatePlot(true);
            CalcPerSecondPlotModel.InvalidatePlot(true);
            ThreadPoolPlotModel.InvalidatePlot(true);
        }
    }
}
