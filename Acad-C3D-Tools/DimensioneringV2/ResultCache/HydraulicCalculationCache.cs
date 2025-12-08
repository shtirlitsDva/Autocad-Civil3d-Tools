using NorsynHydraulicCalc;
using NorsynHydraulicCalc.Pipes;

using NorsynHydraulicShared;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

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
        private readonly bool _cacheEnabled;

        /// <summary>
        /// Creates a cache with the specified extractors.
        /// </summary>
        /// <param name="calculationFunc">The calculation function to cache.</param>
        /// <param name="cacheEnabled">Whether caching is enabled.</param>
        /// <param name="keyExtractors">Property extractors defining the cache key.</param>
        /// <param name="cachePrecision">Decimal places for double precision (default 4 = 0.0001).</param>
        public HydraulicCalculationCache(
            Func<T, CalculationResultFordeling> calculationFunc,
            bool cacheEnabled,
            IReadOnlyList<IKeyPropertyExtractor<T>> keyExtractors,
            int cachePrecision = 4)
        {
            _calcFunc = calculationFunc ?? throw new ArgumentNullException(nameof(calculationFunc));

            if (keyExtractors is null) throw new ArgumentNullException(nameof(keyExtractors));
            if (keyExtractors.Count == 0)
                throw new ArgumentException("At least one key extractor is required.", nameof(keyExtractors));

            int precisionFactor = (int)Math.Pow(10, cachePrecision);
            _keyBuilder = new SegmentKeyBuilder<T>(keyExtractors, precisionFactor);
            _cacheEnabled = cacheEnabled;
        }

        /// <summary>
        /// Gets cached result or calculates and caches.
        /// Zero heap allocation when T is sealed.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CalculationResultFordeling GetOrCalculate(T segment)
        {
            if (segment.NumberOfBuildingsSupplied == 0)
                return new CalculationResultFordeling(
                    "Fordelingsledning", Dim.NA, 0, 0, 0, 0, 0, 0, 0, 0, 0);

            if (!_cacheEnabled) return _calcFunc(segment);

            var key = _keyBuilder.BuildKey(segment);
            return _cache.GetOrAdd(key, _ => _calcFunc(segment));
        }

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
