using NorsynHydraulicCalc;
using NorsynHydraulicCalc.Pipes;

using NorsynHydraulicShared;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace DimensioneringV2.ResultCache
{
    /// <summary>
    /// High-performance generic cache for hydraulic calculations.
    /// When T is a sealed class, all hot-path operations are devirtualized.
    /// Zero heap allocation per cache lookup.
    /// </summary>
    /// <typeparam name="T">The segment type. Use sealed classes for maximum performance.</typeparam>
    public sealed class HydraulicCalculationCache<T> where T : IHydraulicSegment
    {
        private readonly ConcurrentDictionary<SegmentKey, CalculationResultFordeling> _cache = new();
        private readonly Func<T, CalculationResultFordeling> _calcFunc;
        private readonly SegmentKeyBuilder<T> _keyBuilder;
        private readonly IReadOnlyList<IKeyPropertyExtractor<T>> _keyExtractors;
        private readonly bool _cacheEnabled;
        private readonly CacheStatistics? _statistics;

        /// <summary>
        /// Creates a cache with the specified extractors.
        /// </summary>
        /// <param name="calculationFunc">The calculation function to cache.</param>
        /// <param name="cacheEnabled">Whether caching is enabled.</param>
        /// <param name="keyExtractors">Property extractors defining the cache key.</param>
        /// <param name="cachePrecision">Decimal places for double precision (default 4 = 0.0001).</param>
        /// <param name="statistics">Optional statistics tracker for monitoring cache performance.</param>
        public HydraulicCalculationCache(
            Func<T, CalculationResultFordeling> calculationFunc,
            bool cacheEnabled,
            IReadOnlyList<IKeyPropertyExtractor<T>> keyExtractors,
            int cachePrecision = 4,
            CacheStatistics? statistics = null)
        {
            _calcFunc = calculationFunc ?? throw new ArgumentNullException(nameof(calculationFunc));

            if (keyExtractors is null) throw new ArgumentNullException(nameof(keyExtractors));
            if (keyExtractors.Count == 0)
                throw new ArgumentException("At least one key extractor is required.", nameof(keyExtractors));

            int precisionFactor = (int)Math.Pow(10, cachePrecision);
            _keyBuilder = new SegmentKeyBuilder<T>(keyExtractors, precisionFactor);
            _keyExtractors = keyExtractors;
            _cacheEnabled = cacheEnabled;
            _statistics = statistics;
        }

        /// <summary>
        /// Gets cached result or calculates and caches.
        /// Zero heap allocation when T is sealed (unless debug mode is on).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CalculationResultFordeling GetOrCalculate(T segment)
        {
            if (segment.NumberOfBuildingsSupplied == 0)
                return new CalculationResultFordeling(
                    "Fordelingsledning", Dim.NA, 0, 0, 0, 0, 0, 0, 0, 0, 0);

            if (!_cacheEnabled)
            {
                _statistics?.RecordMiss();
                
                // DEBUG: Uncomment to log cache-disabled operations
                //if (_statistics?.DebugMode == true)
                //{
                //    var inputValues = GetInputValuesString(segment);
                //    _statistics.RecordDebugEntry("CACHE_DISABLED", "N/A", inputValues, false);
                //}
                
                return _calcFunc(segment);
            }

            var key = _keyBuilder.BuildKey(segment);
            
            // Check if key exists first (for statistics)
            if (_cache.TryGetValue(key, out var cached))
            {
                _statistics?.RecordHit();
                
                // DEBUG: Uncomment to log cache hits
                //if (_statistics?.DebugMode == true)
                //{
                //    var inputValues = GetInputValuesString(segment);
                //    _statistics.RecordDebugEntry("HIT", key.ToString(), inputValues, true);
                //}
                
                return cached;
            }

            // Key doesn't exist, calculate and add
            var result = _calcFunc(segment);
            _cache.TryAdd(key, result);
            _statistics?.RecordMiss();
            
            // DEBUG: Uncomment to log cache stores
            //if (_statistics?.DebugMode == true)
            //{
            //    var inputValues = GetInputValuesString(segment);
            //    _statistics.RecordDebugEntry("STORE", key.ToString(), inputValues, false);
            //}
            
            return result;
        }

        // DEBUG: Helper method for debug logging - uncomment when enabling debug mode
        //private string GetInputValuesString(T segment)
        //{
        //    var sb = new StringBuilder();
        //    for (int i = 0; i < _keyExtractors.Count; i++)
        //    {
        //        if (i > 0) sb.Append("; ");
        //        var extractor = _keyExtractors[i];
        //        var rawValue = extractor.GetRawValue(segment);
        //        var scaledValue = extractor.ExtractValue(segment);
        //        sb.Append($"[{i}] raw={rawValue} scaled={scaledValue}");
        //    }
        //    return sb.ToString();
        //}

        /// <summary>
        /// Number of cached calculations.
        /// </summary>
        public int CachedCount => _cache.Count;

        /// <summary>
        /// Clears the cache.
        /// </summary>
        public void Clear() => _cache.Clear();
    }
}
