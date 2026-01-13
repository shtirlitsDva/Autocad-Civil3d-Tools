namespace NorsynHydraulicCalc.MaxFlowCalc
{
    /// <summary>
    /// Factory for creating max flow calculators.
    /// With the new priority-based configuration system, all mediums use the same base calculator.
    /// </summary>
    internal static class MaxFlowCalcFactory
    {
        /// <summary>
        /// Gets the max flow calculator for the given medium and settings.
        /// </summary>
        public static IMaxFlowCalc GetMaxFlowCalc(MediumTypeEnum medium, IHydraulicSettings settings)
        {
            // All mediums now use the same base calculator since pipe configuration
            // is driven by the PipeTypeConfiguration in settings
            return new MaxFlowCalcBase(settings);
        }
    }
}
