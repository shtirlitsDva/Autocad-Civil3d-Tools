using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace DimensioneringV2.ResultCache
{
    /// <summary>
    /// Debug record for cache operations.
    /// </summary>
    public record CacheDebugEntry(
        string Operation,       // "STORE" or "LOOKUP"
        string KeyString,       // The SegmentKey.ToString()
        string InputValues,     // The actual input property values
        bool WasHit);           // Whether it was a cache hit

    /// <summary>
    /// Thread-safe cache statistics tracker.
    /// Tracks hits, misses, and timing for performance analysis.
    /// </summary>
    public class CacheStatistics
    {
        private long _hits;
        private long _misses;
        private long _totalCalculations;
        private long _uniqueEntriesStored;
        private readonly Stopwatch _stopwatch;
        private readonly object _lock = new();

        // Debug mode
        private readonly ConcurrentBag<CacheDebugEntry> _debugEntries = new();

        /// <summary>
        /// Event fired when statistics are updated.
        /// </summary>
        public event Action<CacheStatisticsSnapshot>? StatisticsUpdated;

        /// <summary>
        /// Whether debug mode is enabled. 
        /// Reads directly from CacheStatisticsContext.EnableDebugMode.
        /// </summary>
        public bool DebugMode => UI.CacheStatistics.CacheStatisticsContext.EnableDebugMode;

        /// <summary>
        /// Number of debug entries recorded.
        /// </summary>
        public int DebugEntryCount => _debugEntries.Count;

        public CacheStatistics()
        {
            _stopwatch = new Stopwatch();
        }

        /// <summary>
        /// Starts the timing.
        /// </summary>
        public void Start()
        {
            _hits = 0;
            _misses = 0;
            _totalCalculations = 0;
            _uniqueEntriesStored = 0;
            _debugEntries.Clear();
            _stopwatch.Restart();
        }

        /// <summary>
        /// Stops the timing.
        /// </summary>
        public void Stop()
        {
            _stopwatch.Stop();
            RaiseStatisticsUpdated();
        }

        /// <summary>
        /// Records a cache hit.
        /// Thread-safe and lock-free for maximum performance.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void RecordHit()
        {
            Interlocked.Increment(ref _hits);
            Interlocked.Increment(ref _totalCalculations);
            // NOTE: Do NOT call RaiseStatisticsUpdated() here - it's too expensive for hot path.
            // The UI polls via timer instead.
        }

        /// <summary>
        /// Records a cache miss (new calculation performed and stored).
        /// Thread-safe and lock-free for maximum performance.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void RecordMiss()
        {
            Interlocked.Increment(ref _misses);
            Interlocked.Increment(ref _totalCalculations);
            Interlocked.Increment(ref _uniqueEntriesStored);
            // NOTE: Do NOT call RaiseStatisticsUpdated() here - it's too expensive for hot path.
            // The UI polls via timer instead.
        }

        /// <summary>
        /// Updates the count of unique entries stored in the cache.
        /// Call this with the actual cache count for accurate tracking.
        /// </summary>
        public void SetUniqueEntriesStored(long count)
        {
            Interlocked.Exchange(ref _uniqueEntriesStored, count);
        }

        /// <summary>
        /// Gets a snapshot of current statistics.
        /// </summary>
        public CacheStatisticsSnapshot GetSnapshot()
        {
            return new CacheStatisticsSnapshot(
                Interlocked.Read(ref _hits),
                Interlocked.Read(ref _misses),
                Interlocked.Read(ref _totalCalculations),
                Interlocked.Read(ref _uniqueEntriesStored),
                _stopwatch.Elapsed);
        }

        private void RaiseStatisticsUpdated()
        {
            StatisticsUpdated?.Invoke(GetSnapshot());
        }

        /// <summary>
        /// Elapsed time since Start() was called.
        /// </summary>
        public TimeSpan ElapsedTime => _stopwatch.Elapsed;

        /// <summary>
        /// Whether the stopwatch is running.
        /// </summary>
        public bool IsRunning => _stopwatch.IsRunning;

        /// <summary>
        /// Records a debug entry if debug mode is enabled.
        /// </summary>
        public void RecordDebugEntry(string operation, string keyString, string inputValues, bool wasHit)
        {
            if (DebugMode)
            {
                _debugEntries.Add(new CacheDebugEntry(operation, keyString, inputValues, wasHit));
            }
        }

        /// <summary>
        /// Dumps all debug entries to a CSV file.
        /// </summary>
        /// <param name="filePath">The path to write the CSV file.</param>
        public void DumpDebugEntriesToCsv(string filePath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Operation,KeyString,InputValues,WasHit");

            foreach (var entry in _debugEntries)
            {
                // Escape values for CSV
                var key = EscapeCsvField(entry.KeyString);
                var inputs = EscapeCsvField(entry.InputValues);
                sb.AppendLine($"{entry.Operation},{key},{inputs},{entry.WasHit}");
            }

            File.WriteAllText(filePath, sb.ToString());
        }

        private static string EscapeCsvField(string field)
        {
            if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
            {
                return $"\"{field.Replace("\"", "\"\"")}\"";
            }
            return field;
        }

        /// <summary>
        /// Clears all debug entries.
        /// </summary>
        public void ClearDebugEntries()
        {
            _debugEntries.Clear();
        }
    }

    /// <summary>
    /// Immutable snapshot of cache statistics at a point in time.
    /// </summary>
    public readonly record struct CacheStatisticsSnapshot(
        long Hits,
        long Misses,
        long TotalCalculations,
        long UniqueEntriesStored,
        TimeSpan ElapsedTime)
    {
        public double HitRate => TotalCalculations > 0 ? (double)Hits / TotalCalculations * 100 : 0;
        public double CalculationsPerSecond => ElapsedTime.TotalSeconds > 0 
            ? TotalCalculations / ElapsedTime.TotalSeconds 
            : 0;
    }
}
