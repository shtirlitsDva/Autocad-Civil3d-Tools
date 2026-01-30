using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using DimensioneringV2.Geometry;

namespace DimensioneringV2
{
    /// <summary>
    /// Legacy tolerance class.
    /// For coordinate comparisons, prefer using <see cref="CoordinateTolerance"/> directly.
    /// </summary>
    internal static class Tolerance
    {
        /// <summary>
        /// Default tolerance for general numeric comparisons (curve parameters, etc.)
        /// This is NOT for coordinate comparisons - use <see cref="CoordinateTolerance"/> for that.
        /// </summary>
        public const double Default = 1e-6;

        /// <summary>
        /// Tolerance for coordinate comparisons in UTM (EPSG:25832).
        /// Equivalent to <see cref="CoordinateTolerance.Utm32N"/>.
        /// </summary>
        public static double Coordinate => CoordinateTolerance.Utm32N;
    }
}
