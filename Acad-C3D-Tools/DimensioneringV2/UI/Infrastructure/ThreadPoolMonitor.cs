using System;
using System.Threading;
using System.Windows.Threading;

namespace DimensioneringV2.UI.Infrastructure
{
    /// <summary>
    /// Monitors thread pool health during parallel operations.
    /// Helps diagnose thread pool exhaustion issues.
    /// </summary>
    public class ThreadPoolMonitor : IDisposable
    {
        private readonly DispatcherTimer _timer;
        private readonly Action<ThreadPoolSnapshot> _onUpdate;
        private int _peakBusyWorkers;
        private int _peakBusyIO;
        private int _starvationCount;
        private int _lastAvailableWorkers;
        private bool _disposed;

        public ThreadPoolMonitor(Action<ThreadPoolSnapshot> onUpdate, int intervalMs = 250)
        {
            _onUpdate = onUpdate;
            
            ThreadPool.GetMaxThreads(out int maxWorkers, out _);
            _lastAvailableWorkers = maxWorkers;

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(intervalMs)
            };
            _timer.Tick += Timer_Tick;
        }

        public void Start()
        {
            _peakBusyWorkers = 0;
            _peakBusyIO = 0;
            _starvationCount = 0;
            _timer.Start();
        }

        public void Stop()
        {
            _timer.Stop();
            // Final update
            Timer_Tick(null, EventArgs.Empty);
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            ThreadPool.GetMaxThreads(out int maxWorkers, out int maxIO);
            ThreadPool.GetAvailableThreads(out int availableWorkers, out int availableIO);
            ThreadPool.GetMinThreads(out int minWorkers, out int minIO);

            int busyWorkers = maxWorkers - availableWorkers;
            int busyIO = maxIO - availableIO;

            // Track peaks
            if (busyWorkers > _peakBusyWorkers) _peakBusyWorkers = busyWorkers;
            if (busyIO > _peakBusyIO) _peakBusyIO = busyIO;

            // Detect starvation: when available workers drops below min threads
            // or when available suddenly increases (threads were waiting)
            if (availableWorkers < minWorkers || 
                (availableWorkers - _lastAvailableWorkers > Environment.ProcessorCount))
            {
                _starvationCount++;
            }
            _lastAvailableWorkers = availableWorkers;

            // Calculate utilization percentage
            double workerUtilization = maxWorkers > 0 ? (double)busyWorkers / maxWorkers * 100 : 0;

            var snapshot = new ThreadPoolSnapshot(
                MaxWorkerThreads: maxWorkers,
                AvailableWorkerThreads: availableWorkers,
                BusyWorkerThreads: busyWorkers,
                MinWorkerThreads: minWorkers,
                MaxIOThreads: maxIO,
                AvailableIOThreads: availableIO,
                BusyIOThreads: busyIO,
                PeakBusyWorkers: _peakBusyWorkers,
                PeakBusyIO: _peakBusyIO,
                WorkerUtilization: workerUtilization,
                StarvationEvents: _starvationCount);

            _onUpdate(snapshot);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _timer.Stop();
                _disposed = true;
            }
        }
    }

    public readonly record struct ThreadPoolSnapshot(
        int MaxWorkerThreads,
        int AvailableWorkerThreads,
        int BusyWorkerThreads,
        int MinWorkerThreads,
        int MaxIOThreads,
        int AvailableIOThreads,
        int BusyIOThreads,
        int PeakBusyWorkers,
        int PeakBusyIO,
        double WorkerUtilization,
        int StarvationEvents)
    {
        /// <summary>
        /// True if thread pool appears to be under stress.
        /// </summary>
        public bool IsUnderStress => 
            BusyWorkerThreads >= MaxWorkerThreads * 0.9 || 
            AvailableWorkerThreads < MinWorkerThreads;

        /// <summary>
        /// True if starvation has been detected.
        /// </summary>
        public bool HasStarvation => StarvationEvents > 0;
    }
}
