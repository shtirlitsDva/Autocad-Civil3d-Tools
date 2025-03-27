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
    internal class LoggerFile : ILog, IDisposable
    {
        private readonly string LogFilePath = @"C:\Temp\HydroLog.log";
        private readonly ConcurrentQueue<string> _buffer = new();
        private readonly Timer _flushTimer;
        private readonly int _flushThreshold = 100;
        private readonly object _flushLock = new();

        private volatile bool _timerRunning = false;
        private DateTime _lastWriteTime;
        private StreamWriter _writer;

        public LoggerFile()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogFilePath)!);
            _writer = new StreamWriter(LogFilePath, append: true) { AutoFlush = true };
            _flushTimer = new Timer(FlushIfIdle, null, Timeout.Infinite, Timeout.Infinite);
        }

        public void Report(string message) => Enqueue(message);
        public void Report(object obj) => Enqueue(obj?.ToString() ?? "null");
        public void Report() => Enqueue("");

        private void Enqueue(string message)
        {
            _buffer.Enqueue(message);
            _lastWriteTime = DateTime.UtcNow;

            if (_buffer.Count >= _flushThreshold)
                Flush();

            StartTimerIfNeeded();
        }

        private void StartTimerIfNeeded()
        {
            if (_timerRunning) return;

            lock (_flushLock)
            {
                if (_timerRunning) return;

                _flushTimer.Change(1000, 1000); // check every second
                _timerRunning = true;
            }
        }

        private void FlushIfIdle(object? state)
        {
            if ((DateTime.UtcNow - _lastWriteTime).TotalSeconds > 5)
            {
                Flush();

                lock (_flushLock)
                {
                    _flushTimer.Change(Timeout.Infinite, Timeout.Infinite);
                    _timerRunning = false;
                }
            }
        }

        private void Flush()
        {
            lock (_flushLock)
            {
                while (_buffer.TryDequeue(out var line))
                {
                    _writer.WriteLine(line);
                }
                _writer.Flush();
            }
        }

        public void Dispose()
        {
            _flushTimer.Dispose();
            Flush();
            _writer.Dispose();
        }

        public void Log(object obj)
        {
            throw new NotImplementedException();
        }

        public void Log(string message)
        {
            throw new NotImplementedException();
        }
    }

}
