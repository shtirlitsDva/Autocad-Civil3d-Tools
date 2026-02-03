using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NorsynHydraulicTester.Services;

namespace NorsynHydraulicTester.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ICalculationService _calculationService;

    [ObservableProperty]
    private int selectedTabIndex;

    [ObservableProperty]
    private bool isCalculating;

    public SettingsViewModel Settings { get; }
    public SegmentInputViewModel SegmentInput { get; }
    public CalculationViewModel Calculation { get; }
    public LookupTableViewModel LookupTable { get; }

    public MainViewModel(
        ICalculationService calculationService,
        SettingsViewModel settings,
        SegmentInputViewModel segmentInput,
        CalculationViewModel calculation,
        LookupTableViewModel lookupTable)
    {
        _calculationService = calculationService;
        Settings = settings;
        SegmentInput = segmentInput;
        Calculation = calculation;
        LookupTable = lookupTable;
    }

    [RelayCommand]
    private async Task RunCalculation()
    {
        if (IsCalculating) return;

        try
        {
            IsCalculating = true;
            LookupTable.LoadLookupTable(Settings);
            await Calculation.RunCalculationAsync(SegmentInput.Segment, Settings);
            SelectedTabIndex = 3;
        }
        finally
        {
            IsCalculating = false;
        }
    }
}
