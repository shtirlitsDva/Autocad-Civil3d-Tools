using NorsynHydraulicShared;
using NorsynHydraulicTester.Models;

namespace NorsynHydraulicTester.Services;

public interface ICalculationService
{
    Task<CalculationResult> CalculateClientSegmentAsync(
        TestSegment segment,
        IHydraulicSettings settings);

    Task<CalculationResult> CalculateDistributionSegmentAsync(
        TestSegment segment,
        IHydraulicSettings settings);

    Task<CalculationResult> CalculateDistributionFromHeatDemandAsync(
        TestSegment segment,
        IHydraulicSettings settings);
}
