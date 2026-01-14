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
    /// High-performance cache for client (SL) hydraulic calculations.
    /// Cache key includes both segment properties AND parentPipeType.
    /// </summary>
    /// <typeparam name="T">The segment type. Use sealed classes for maximum performance.</typeparam>
    public sealed class ClientCalculationCache<T> where T : IHydraulicSegment
    {
        private readonly ConcurrentDictionary<ClientSegmentKey, CalculationResultClient> _cache = new();
        private readonly Func<T, PipeType?, CalculationResultClient> _calcFunc;
        private readonly ClientSegmentKeyBuilder<T> _keyBuilder;
        private readonly bool _cacheEnabled;
        private readonly CacheStatistics? _statistics;

        /// <summary>
        /// Creates a cache with the specified extractors.
        /// </summary>
        /// <param name="calculationFunc">The calculation function to cache (takes segment and parentPipeType).</param>
        /// <param name="cacheEnabled">Whether caching is enabled.</param>
        /// <param name="keyExtractors">Property extractors defining the cache key.</param>
        /// <param name="cachePrecision">Decimal places for double precision (default 4 = 0.0001).</param>
        /// <param name="statistics">Optional statistics tracker for monitoring cache performance.</param>
        public ClientCalculationCache(
            Func<T, PipeType?, CalculationResultClient> calculationFunc,
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
            _keyBuilder = new ClientSegmentKeyBuilder<T>(keyExtractors, precisionFactor);
            _cacheEnabled = cacheEnabled;
            _statistics = statistics;
        }

        /// <summary>
        /// Gets cached result or calculates and caches.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CalculationResultClient GetOrCalculate(T segment, PipeType? parentPipeType)
        {
            if (!_cacheEnabled)
            {
                _statistics?.RecordMiss();
                return _calcFunc(segment, parentPipeType);
            }

            var key = _keyBuilder.BuildKey(segment, parentPipeType);

            if (_cache.TryGetValue(key, out var cached))
            {
                _statistics?.RecordHit();
                return cached;
            }

            var result = _calcFunc(segment, parentPipeType);
            _cache.TryAdd(key, result);
            _statistics?.RecordMiss();

            return result;
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

    /// <summary>
    /// Cache key for client calculations that includes parentPipeType.
    /// </summary>
    public readonly struct ClientSegmentKey : IEquatable<ClientSegmentKey>
    {
        private readonly SegmentKey _segmentKey;
        private readonly int _parentPipeType; // -1 for null, otherwise cast from PipeType
        private readonly int _hash;

        public ClientSegmentKey(SegmentKey segmentKey, PipeType? parentPipeType)
        {
            _segmentKey = segmentKey;
            _parentPipeType = parentPipeType.HasValue ? (int)parentPipeType.Value : -1;
            _hash = HashCode.Combine(_segmentKey.GetHashCode(), _parentPipeType);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(ClientSegmentKey other)
        {
            return _hash == other._hash &&
                   _parentPipeType == other._parentPipeType &&
                   _segmentKey.Equals(other._segmentKey);
        }

        public override bool Equals(object? obj)
            => obj is ClientSegmentKey other && Equals(other);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode() => _hash;

        public static bool operator ==(ClientSegmentKey left, ClientSegmentKey right) => left.Equals(right);
        public static bool operator !=(ClientSegmentKey left, ClientSegmentKey right) => !left.Equals(right);
    }

    /// <summary>
    /// Builds ClientSegmentKey from segment and parentPipeType.
    /// </summary>
    internal sealed class ClientSegmentKeyBuilder<T> where T : IHydraulicSegment
    {
        private readonly SegmentKeyBuilder<T> _segmentKeyBuilder;

        public ClientSegmentKeyBuilder(IReadOnlyList<IKeyPropertyExtractor<T>> extractors, int precisionFactor)
        {
            _segmentKeyBuilder = new SegmentKeyBuilder<T>(extractors, precisionFactor);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ClientSegmentKey BuildKey(T segment, PipeType? parentPipeType)
        {
            var segmentKey = _segmentKeyBuilder.BuildKey(segment);
            return new ClientSegmentKey(segmentKey, parentPipeType);
        }
    }
}
