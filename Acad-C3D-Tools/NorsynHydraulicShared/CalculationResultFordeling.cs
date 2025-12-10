using NorsynHydraulicCalc.Pipes;

namespace NorsynHydraulicCalc
{
    public readonly struct CalculationResultFordeling(
        string segmentType, Dim dim,
        double reynoldsSupply, double reynoldsReturn,        
        double dimFlowSupply, double dimFlowReturn,
        double pressureGradientSupply, double pressureGradientReturn,
        double velocitySupply, double velocityReturn, double utilizationRate)
    {
        public string SegmentType { get; } = segmentType;
        public Dim Dim { get; } = dim;
        public double ReynoldsSupply { get; } = reynoldsSupply;
        public double ReynoldsReturn { get; } = reynoldsReturn;
        public double DimFlowSupply { get; } = dimFlowSupply;
        public double DimFlowReturn { get; } = dimFlowReturn;
        public double PressureGradientSupply { get; } = pressureGradientSupply;
        public double PressureGradientReturn { get; } = pressureGradientReturn;
        public double VelocitySupply { get; } = velocitySupply;
        public double VelocityReturn { get; } = velocityReturn;
        public double UtilizationRate { get; } = utilizationRate;
    }
}