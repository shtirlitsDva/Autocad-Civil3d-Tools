using NorsynHydraulicShared;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DimensioneringV2.AutoCAD
{
    internal class LoggerFile : ILog
    {
        private readonly string _logFilePath = @"C:\Temp\HydroLog.log";
        private readonly ConcurrentQueue<string> _queue = new();
        private readonly object _lock = new();

        private Timer? _timer;
        private volatile bool _flushInProgress = false;
        private const int _flushThreshold = 10000;
        private const int _idleFlushMilliseconds = 1500;

        public LoggerFile()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_logFilePath)!);
            File.WriteAllText(_logFilePath, string.Empty);
        }

        public void Report(string message) => Enqueue(message);
        public void Report(object obj) => Enqueue(obj?.ToString() ?? "null");
        public void Report() => Enqueue(string.Empty);

        public void Log(string message) => throw new NotImplementedException();
        public void Log(object obj) => throw new NotImplementedException();

        private void Enqueue(string message)
        {
            _queue.Enqueue(message);

            if (_queue.Count >= _flushThreshold)
            {
                StartAsyncFlush();
            }

            ResetTimer();
        }

        private void ResetTimer()
        {
            lock (_lock)
            {
                _timer?.Dispose(); // cancel previous timer
                _timer = new Timer(OnTimerElapsed, null, _idleFlushMilliseconds, Timeout.Infinite);
            }
        }

        private void OnTimerElapsed(object? state)
        {
            lock (_lock)
            {
                _timer?.Dispose();
                _timer = null;
            }

            StartAsyncFlush();
        }

        private void StartAsyncFlush()
        {
            lock (_lock)
            {
                if (_flushInProgress) return;
                _flushInProgress = true;
            }

            Task.Run(() =>
            {
                try
                {
                    FlushBufferToDisk();
                }
                finally
                {
                    lock (_lock)
                    {
                        _flushInProgress = false;
                    }
                }
            });
        }

        private void FlushBufferToDisk()
        {
            var lines = new List<string>();
            while (_queue.TryDequeue(out var line))
            {
                lines.Add(line);
            }

            if (lines.Count == 0) return;

            try
            {
                File.AppendAllLines(_logFilePath, lines);
            }
            catch
            {
                // Optional: handle logging failures here
            }
        }
    }
}