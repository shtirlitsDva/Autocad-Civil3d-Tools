using DimensioneringV2.BruteForceOptimization;
using DimensioneringV2.ResultCache;

using NorsynHydraulicCalc;

using QuikGraph;

namespace DimensioneringV2.GraphUtilities
{
    /// <summary>
    /// Calculates hydraulics for graph edges using cached results.
    /// </summary>
    internal static class GraphHydraulicsCalculator
    {
        /// <summary>
        /// Calculates hydraulics for distribution pipes using the cache.
        /// Service pipes (stikledninger) should already have results applied before calling this.
        /// </summary>
        public static void CalculateHydraulics(
            UndirectedGraph<BFNode, BFEdge> graph,
            HydraulicCalculationCache<BFEdge> cache)
        {
            
        }
    }
}
