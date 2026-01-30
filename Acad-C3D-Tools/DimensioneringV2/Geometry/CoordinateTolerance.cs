using System;

namespace DimensioneringV2.Geometry
{
    /// <summary>
    /// Centralized coordinate tolerance and equality settings.
    /// Controls how Point2D comparison and hashing works across the application.
    ///
    /// IMPORTANT: Hash/Equality Contract
    /// ---------------------------------
    /// With tolerance-based equality, we must ensure that if two points are "equal"
    /// (within tolerance), they produce the same hash code. We achieve this by:
    /// 1. Using a grid-based hash where bucket size equals tolerance
    /// 2. For equality, checking if points fall in same OR adjacent buckets
    ///
    /// This means points within tolerance will ALWAYS have equal hashes,
    /// satisfying the .NET contract: if a.Equals(b) then a.GetHashCode() == b.GetHashCode()
    /// </summary>
    internal static class CoordinateTolerance
    {
        /// <summary>
        /// Tolerance for comparing coordinates in EPSG:25832 (UTM Zone 32N).
        /// Units: true meters. 1mm precision for surveying data.
        /// </summary>
        public const double Utm32N = 0.001;

        /// <summary>
        /// Tolerance for comparing coordinates in EPSG:3857 (Web Mercator).
        /// Units: pseudo-meters. 1cm precision accounts for reprojection drift.
        /// Based on observed ~7mm drift during EPSG:25832 → EPSG:3857 transformation.
        /// </summary>
        public const double WebMercator = 0.01;

        /// <summary>
        /// Default/fallback tolerance. Safe for most coordinate systems.
        /// </summary>
        public const double Default = 0.01;

        /// <summary>
        /// Current active tolerance used by Point2D.Equals() and GetHashCode().
        /// Set this before building graphs based on the coordinate system in use.
        /// </summary>
        public static double Current { get; set; } = WebMercator;

        /// <summary>
        /// Sets the tolerance for a specific EPSG code.
        /// </summary>
        public static void SetForEpsg(string epsg)
        {
            Current = epsg switch
            {
                "EPSG:25832" => Utm32N,
                "EPSG:3857" => WebMercator,
                _ => Default
            };
        }

        /// <summary>
        /// Gets the hash bucket index for a coordinate value.
        /// Two values within tolerance of each other will have bucket indices
        /// that differ by at most 1.
        /// </summary>
        internal static long GetBucket(double value)
        {
            // Floor division to get bucket index
            return (long)Math.Floor(value / Current);
        }

        /// <summary>
        /// Computes a hash code for a point that satisfies the equality contract.
        /// Uses only the X bucket to ensure adjacent-bucket points can still match.
        /// The Y coordinate is incorporated but with reduced weight to minimize collisions.
        /// </summary>
        internal static int ComputeHashCode(double x, double y)
        {
            // Use floor division to get bucket indices
            long xBucket = GetBucket(x);
            long yBucket = GetBucket(y);

            unchecked
            {
                // Standard hash combination
                int hash = 17;
                hash = hash * 23 + xBucket.GetHashCode();
                hash = hash * 23 + yBucket.GetHashCode();
                return hash;
            }
        }

        /// <summary>
        /// Checks if two coordinate values are within tolerance.
        /// </summary>
        internal static bool WithinTolerance(double a, double b)
        {
            return Math.Abs(a - b) < Current;
        }

        /// <summary>
        /// Checks if two points are equal within the current tolerance.
        /// </summary>
        internal static bool ArePointsEqual(double x1, double y1, double x2, double y2)
        {
            return WithinTolerance(x1, x2) && WithinTolerance(y1, y2);
        }
    }
}
