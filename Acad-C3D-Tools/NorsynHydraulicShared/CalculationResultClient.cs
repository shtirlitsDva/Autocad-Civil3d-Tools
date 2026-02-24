using NorsynHydraulicCalc.Pipes;

namespace NorsynHydraulicCalc
{
    public readonly struct CalculationResultClient(
        string segmentType, Dim dim,
        double reynoldsSupply, double reynoldsReturn,
        double karFlowHeatSupply, double karFlowBVSupply,
        double karFlowHeatReturn, double karFlowBVReturn,
        double dimFlowSupply, double dimFlowReturn,
        double pressureGradientSupply, double pressureGradientReturn,
        double velocitySupply, double velocityReturn, double utilizationRate,
        double effekt)
    {
        public string SegmentType { get; } = segmentType;
        public Dim Dim { get; } = dim;
        public double ReynoldsSupply { get; } = reynoldsSupply;
        public double ReynoldsReturn { get; } = reynoldsReturn;
        public double KarFlowHeatSupply { get; } = karFlowHeatSupply;
        public double KarFlowBVSupply { get; } = karFlowBVSupply;
        public double KarFlowHeatReturn { get; } = karFlowHeatReturn;
        public double KarFlowBVReturn { get; } = karFlowBVReturn;
        public double DimFlowSupply { get; } = dimFlowSupply;
        public double DimFlowReturn { get; } = dimFlowReturn;
        public double PressureGradientSupply { get; } = pressureGradientSupply;
        public double PressureGradientReturn { get; } = pressureGradientReturn;
        public double VelocitySupply { get; } = velocitySupply;
        public double VelocityReturn { get; } = velocityReturn;
        public double UtilizationRate { get; } = utilizationRate;
        public double Effekt { get; } = effekt;
    }
}