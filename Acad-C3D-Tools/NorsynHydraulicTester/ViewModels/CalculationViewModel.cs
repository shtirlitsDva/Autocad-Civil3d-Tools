using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using NorsynHydraulicCalc;
using NorsynHydraulicShared;
using NorsynHydraulicTester.Models;
using NorsynHydraulicTester.Services;

namespace NorsynHydraulicTester.ViewModels;

public partial class CalculationViewModel : ObservableObject
{
    private readonly ICalculationService _calculationService;

    [ObservableProperty]
    private ObservableCollection<CalculationStep> steps = new();

    [ObservableProperty]
    private CalculationStep? selectedStep;

    [ObservableProperty]
    private bool hasResults;

    [ObservableProperty]
    private string? errorMessage;

    public CalculationViewModel(ICalculationService calculationService)
    {
        _calculationService = calculationService;
    }

    public async Task RunCalculationAsync(TestSegment segment, IHydraulicSettings settings)
    {
        Steps.Clear();
        SelectedStep = null;
        ErrorMessage = null;
        HasResults = false;

        try
        {
            CalculationResult result;
            if (segment.SegmentType == SegmentType.Stikledning)
            {
                result = await _calculationService.CalculateClientSegmentAsync(segment, settings);
            }
            else
            {
                result = segment.HeatingDemandConnected > 0
                    ? await _calculationService.CalculateDistributionFromHeatDemandAsync(segment, settings)
                    : await _calculationService.CalculateDistributionSegmentAsync(segment, settings);
            }

            if (result.IsSuccess)
            {
                foreach (var step in result.Steps)
                {
                    Steps.Add(step);
                }
                HasResults = Steps.Count > 0;
                if (Steps.Count > 0)
                    SelectedStep = Steps[0];
            }
            else
            {
                ErrorMessage = result.ErrorMessage;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}
