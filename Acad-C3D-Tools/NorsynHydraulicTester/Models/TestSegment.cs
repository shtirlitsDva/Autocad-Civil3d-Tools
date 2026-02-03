using NorsynHydraulicCalc;
using NorsynHydraulicCalc.Pipes;
using NorsynHydraulicShared;

namespace NorsynHydraulicTester.Models;

public class TestSegment : IHydraulicSegment
{
    public SegmentType SegmentType { get; set; } = SegmentType.Stikledning;
    public double Length { get; set; } = 50;
    public bool ManualDim { get; set; } = false;
    public double HeatingDemandConnected { get; set; } = 100;
    public int NumberOfBuildingsConnected { get; set; } = 1;
    public int NumberOfUnitsConnected { get; set; } = 2;
    public double HeatingDemandSupplied { get; set; }
    public int NumberOfBuildingsSupplied { get; set; }
    public int NumberOfUnitsSupplied { get; set; }
    public double KarFlowHeatSupply { get; set; }
    public double KarFlowBVSupply { get; set; }
    public double KarFlowHeatReturn { get; set; }
    public double KarFlowBVReturn { get; set; }
    public Dim Dim { get; set; }
    public double TempDeltaVarme { get; set; }
    public double TempDeltaBV { get; set; }
    public int Nyttetimer { get; set; } = 2000;
}
