using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DimensioneringV2.ResultCache
{
    /// <summary>
    /// Fixed-size cache key with inline storage. Zero heap allocation.
    /// Supports up to 8 property values stored as longs.
    /// </summary>
    /// <remarks>
    /// Size: 8 longs (64 bytes) + 1 byte count + 4 bytes hash = 69 bytes, padded to 72 bytes.
    /// This is a value type - passed by value, no heap allocation.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct SegmentKey : IEquatable<SegmentKey>
    {
        public const int MaxProperties = 8;

        // Inline storage - no array allocation
        private readonly long _v0, _v1, _v2, _v3, _v4, _v5, _v6, _v7;
        private readonly byte _count;
        private readonly int _hash;

        /// <summary>
        /// Creates a key from a span of values. The span is copied into inline storage.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SegmentKey(ReadOnlySpan<long> values)
        {
            if (values.Length > MaxProperties)
                throw new ArgumentException($"Maximum {MaxProperties} values supported.", nameof(values));

            _count = (byte)values.Length;

            // Copy values to inline storage
            _v0 = values.Length > 0 ? values[0] : 0;
            _v1 = values.Length > 1 ? values[1] : 0;
            _v2 = values.Length > 2 ? values[2] : 0;
            _v3 = values.Length > 3 ? values[3] : 0;
            _v4 = values.Length > 4 ? values[4] : 0;
            _v5 = values.Length > 5 ? values[5] : 0;
            _v6 = values.Length > 6 ? values[6] : 0;
            _v7 = values.Length > 7 ? values[7] : 0;

            // Compute hash from actual values only
            _hash = ComputeHash(values);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ComputeHash(ReadOnlySpan<long> values)
        {
            var hash = new HashCode();
            for (int i = 0; i < values.Length; i++)
            {
                hash.Add(values[i]);
            }
            return hash.ToHashCode();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(SegmentKey other)
        {
            // Fast path: different hash or count means not equal
            if (_hash != other._hash || _count != other._count) return false;

            // Compare only the values that are actually used
            // Unrolled for performance
            return _count switch
            {
                0 => true,
                1 => _v0 == other._v0,
                2 => _v0 == other._v0 && _v1 == other._v1,
                3 => _v0 == other._v0 && _v1 == other._v1 && _v2 == other._v2,
                4 => _v0 == other._v0 && _v1 == other._v1 && _v2 == other._v2 && _v3 == other._v3,
                5 => _v0 == other._v0 && _v1 == other._v1 && _v2 == other._v2 && _v3 == other._v3 &&
                     _v4 == other._v4,
                6 => _v0 == other._v0 && _v1 == other._v1 && _v2 == other._v2 && _v3 == other._v3 &&
                     _v4 == other._v4 && _v5 == other._v5,
                7 => _v0 == other._v0 && _v1 == other._v1 && _v2 == other._v2 && _v3 == other._v3 &&
                     _v4 == other._v4 && _v5 == other._v5 && _v6 == other._v6,
                8 => _v0 == other._v0 && _v1 == other._v1 && _v2 == other._v2 && _v3 == other._v3 &&
                     _v4 == other._v4 && _v5 == other._v5 && _v6 == other._v6 && _v7 == other._v7,
                _ => false
            };
        }

        public override bool Equals(object? obj)
            => obj is SegmentKey other && Equals(other);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode() => _hash;

        public static bool operator ==(SegmentKey left, SegmentKey right) => left.Equals(right);
        public static bool operator !=(SegmentKey left, SegmentKey right) => !left.Equals(right);

        public override string ToString()
        {
            return _count switch
            {
                0 => "SegmentKey[0]: []",
                1 => $"SegmentKey[1]: [{_v0}]",
                2 => $"SegmentKey[2]: [{_v0}, {_v1}]",
                3 => $"SegmentKey[3]: [{_v0}, {_v1}, {_v2}]",
                4 => $"SegmentKey[4]: [{_v0}, {_v1}, {_v2}, {_v3}]",
                5 => $"SegmentKey[5]: [{_v0}, {_v1}, {_v2}, {_v3}, {_v4}]",
                6 => $"SegmentKey[6]: [{_v0}, {_v1}, {_v2}, {_v3}, {_v4}, {_v5}]",
                7 => $"SegmentKey[7]: [{_v0}, {_v1}, {_v2}, {_v3}, {_v4}, {_v5}, {_v6}]",
                8 => $"SegmentKey[8]: [{_v0}, {_v1}, {_v2}, {_v3}, {_v4}, {_v5}, {_v6}, {_v7}]",
                _ => $"SegmentKey[{_count}]: [invalid]"
            };
        }
    }
}