using NorsynHydraulicShared;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace DimensioneringV2.ResultCache
{
    /// <summary>
    /// High-performance generic key builder. When T is a sealed class (like BFEdge),
    /// all property access is devirtualized and potentially inlined by the JIT.
    /// Zero heap allocation per key.
    /// </summary>
    public sealed class SegmentKeyBuilder<T> where T : IHydraulicSegment
    {
        private readonly Func<T, int>[] _intGetters;
        private readonly Func<T, double>[] _doubleGetters;
        private readonly int _intCount;
        private readonly int _doubleCount;
        private readonly int _precisionFactor;

        public SegmentKeyBuilder(IReadOnlyList<IKeyPropertyExtractor<T>> extractors, int precisionFactor)
        {
            if (extractors is null) throw new ArgumentNullException(nameof(extractors));
            if (extractors.Count == 0) throw new ArgumentException("At least one extractor required.", nameof(extractors));
            if (extractors.Count > SegmentKey.MaxProperties)
                throw new ArgumentException($"Maximum {SegmentKey.MaxProperties} properties supported.", nameof(extractors));

            _precisionFactor = precisionFactor;

            var intGetters = new List<Func<T, int>>();
            var doubleGetters = new List<Func<T, double>>();

            foreach (var extractor in extractors)
            {
                switch (extractor)
                {
                    case IntPropertyExtractor<T> intEx:
                        intGetters.Add(intEx.Getter);
                        break;
                    case DoublePropertyExtractor<T> doubleEx:
                        doubleGetters.Add(doubleEx.Getter);
                        break;
                    default:
                        throw new ArgumentException($"Unknown extractor type: {extractor.GetType().Name}");
                }
            }

            _intGetters = intGetters.ToArray();
            _doubleGetters = doubleGetters.ToArray();
            _intCount = _intGetters.Length;
            _doubleCount = _doubleGetters.Length;
        }

        /// <summary>
        /// Builds a cache key. Zero heap allocation - uses stackalloc + value type.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SegmentKey BuildKey(T segment)
        {
            Span<long> values = stackalloc long[_intCount + _doubleCount];
            int idx = 0;

            for (int i = 0; i < _intCount; i++)
            {
                values[idx++] = _intGetters[i](segment);
            }

            for (int i = 0; i < _doubleCount; i++)
            {
                values[idx++] = (long)Math.Round(
                    _doubleGetters[i](segment) * _precisionFactor,
                    MidpointRounding.AwayFromZero);
            }

            return new SegmentKey(values);
        }
    }
}
