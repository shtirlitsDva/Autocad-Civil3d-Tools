using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using NorsynHydraulicCalc;
using NorsynHydraulicTester.Models;
using NorsynHydraulicTester.Services;

namespace NorsynHydraulicTester.ViewModels;

public partial class LookupTableViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<LookupTableRow> flRows = new();

    [ObservableProperty]
    private ObservableCollection<LookupTableRow> slRows = new();

    [ObservableProperty]
    private bool isInitialized;

    [ObservableProperty]
    private string? initializationTime;

    public void LoadLookupTable(SettingsViewModel settings)
    {
        FlRows.Clear();
        SlRows.Clear();

        var logger = new StepCaptureLogger();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var calc = new HydraulicCalc(settings, logger);
        sw.Stop();

        InitializationTime = $"Initialiseret på {sw.ElapsedMilliseconds} ms";

        var flData = calc.TestingGetLookupTable(SegmentType.Fordelingsledning);
        foreach (var row in flData)
        {
            FlRows.Add(new LookupTableRow
            {
                PipeType = row.PipeType,
                DN = row.DN,
                InnerDiameter = row.InnerDiameter,
                MaxVelocity = row.MaxVelocity,
                MaxPressureGradient = row.MaxPressureGradient,
                MaxFlowSupply = row.MaxFlowSupply,
                MaxFlowReturn = row.MaxFlowReturn
            });
        }

        var slData = calc.TestingGetLookupTable(SegmentType.Stikledning);
        foreach (var row in slData)
        {
            SlRows.Add(new LookupTableRow
            {
                PipeType = row.PipeType,
                DN = row.DN,
                InnerDiameter = row.InnerDiameter,
                MaxVelocity = row.MaxVelocity,
                MaxPressureGradient = row.MaxPressureGradient,
                MaxFlowSupply = row.MaxFlowSupply,
                MaxFlowReturn = row.MaxFlowReturn
            });
        }

        IsInitialized = true;
    }
}
