using CommunityToolkit.Mvvm.ComponentModel;
using NorsynHydraulicCalc;
using NorsynHydraulicTester.Models;

namespace NorsynHydraulicTester.ViewModels;

public partial class SegmentInputViewModel : ObservableObject
{
    [ObservableProperty] private SegmentType segmentType = SegmentType.Stikledning;
    [ObservableProperty] private double length = 50;

    public bool IsStikledning
    {
        get => SegmentType == SegmentType.Stikledning;
        set { if (value) SegmentType = SegmentType.Stikledning; }
    }

    public bool IsFordelingsledning
    {
        get => SegmentType == SegmentType.Fordelingsledning;
        set { if (value) SegmentType = SegmentType.Fordelingsledning; }
    }

    partial void OnSegmentTypeChanged(SegmentType value)
    {
        OnPropertyChanged(nameof(IsStikledning));
        OnPropertyChanged(nameof(IsFordelingsledning));
    }
    [ObservableProperty] private double heatingDemand = 100;
    [ObservableProperty] private int numberOfBuildings = 1;
    [ObservableProperty] private int numberOfUnits = 2;
    [ObservableProperty] private int nyttetimer = 2000;
    [ObservableProperty] private double tempDeltaVarme;
    [ObservableProperty] private double tempDeltaBV;

    public TestSegment Segment
    {
        get
        {
            var segment = new TestSegment
            {
                SegmentType = SegmentType,
                Length = Length,
                HeatingDemandConnected = HeatingDemand,
                NumberOfBuildingsConnected = NumberOfBuildings,
                NumberOfUnitsConnected = NumberOfUnits,
                Nyttetimer = Nyttetimer,
                TempDeltaVarme = TempDeltaVarme,
                TempDeltaBV = TempDeltaBV
            };

            if (SegmentType == SegmentType.Fordelingsledning)
            {
                segment.HeatingDemandSupplied = HeatingDemand;
                segment.NumberOfBuildingsSupplied = NumberOfBuildings;
                segment.NumberOfUnitsSupplied = NumberOfUnits;
            }

            return segment;
        }
    }
}
