using NorsynHydraulicShared;

using System;

namespace DimensioneringV2.ResultCache
{
    /// <summary>
    /// Generic property extractor. When T is a sealed class, JIT devirtualizes all calls.
    /// </summary>
    public interface IKeyPropertyExtractor<T> where T : IHydraulicSegment
    {
        long ExtractScaled(T segment, int precisionFactor);
        
        /// <summary>
        /// Gets the raw (unscaled) value as a string for debugging.
        /// </summary>
        string GetRawValue(T segment);
        
        /// <summary>
        /// Gets the scaled value for debugging (uses default precision factor of 10000).
        /// </summary>
        long ExtractValue(T segment) => ExtractScaled(segment, 10000);
    }

    /// <summary>
    /// Extracts an integer property. Zero overhead when T is sealed.
    /// </summary>
    public sealed class IntPropertyExtractor<T> : IKeyPropertyExtractor<T> where T : IHydraulicSegment
    {
        public Func<T, int> Getter { get; }

        public IntPropertyExtractor(Func<T, int> getter)
        {
            Getter = getter ?? throw new ArgumentNullException(nameof(getter));
        }

        public long ExtractScaled(T segment, int precisionFactor) => Getter(segment);
        
        public string GetRawValue(T segment) => Getter(segment).ToString();
    }

    /// <summary>
    /// Extracts a double property and scales it. Zero overhead when T is sealed.
    /// </summary>
    public sealed class DoublePropertyExtractor<T> : IKeyPropertyExtractor<T> where T : IHydraulicSegment
    {
        public Func<T, double> Getter { get; }

        public DoublePropertyExtractor(Func<T, double> getter)
        {
            Getter = getter ?? throw new ArgumentNullException(nameof(getter));
        }

        public long ExtractScaled(T segment, int precisionFactor)
            => (long)Math.Round(Getter(segment) * precisionFactor, MidpointRounding.AwayFromZero);
        
        public string GetRawValue(T segment) => Getter(segment).ToString("G17");
    }

    /// <summary>
    /// Factory methods for creating property extractors.
    /// </summary>
    public static class KeyProperty<T> where T : IHydraulicSegment
    {
        public static IKeyPropertyExtractor<T> Int(Func<T, int> getter)
            => new IntPropertyExtractor<T>(getter);

        public static IKeyPropertyExtractor<T> Double(Func<T, double> getter)
            => new DoublePropertyExtractor<T>(getter);
    }
}
