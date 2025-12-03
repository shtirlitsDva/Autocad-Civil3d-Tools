using NorsynHydraulicCalc.Pipes;

namespace NorsynHydraulicCalc
{
    public interface ICalculationResult
    {
        string SegmentType { get; }
        Dim Dim { get; }
        double ReynoldsSupply { get; }
        double ReynoldsReturn { get; }
        double KarFlowSupply { get; }
        double KarFlowReturn { get; }
        double DimFlowSupply { get; }
        double DimFlowReturn { get; }
        double PressureGradientSupply { get; }
        double PressureGradientReturn { get; }
        double VelocitySupply { get; }
        double VelocityReturn { get; }
        double UtilizationRate { get; }
    }

    public readonly struct CalculationResult(
        string segmentType, Dim dim,
        double reynoldsSupply, double reynoldsReturn, double karFlowSupply,
        double karFlowReturn, double dimFlowSupply, double dimFlowReturn,
        double pressureGradientSupply, double pressureGradientReturn,
        double velocitySupply, double velocityReturn, double utilizationRate) : ICalculationResult
    {
        public string SegmentType { get; } = segmentType;
        public Dim Dim { get; } = dim;
        public double ReynoldsSupply { get; } = reynoldsSupply;
        public double ReynoldsReturn { get; } = reynoldsReturn;
        public double KarFlowSupply { get; } = karFlowSupply;
        public double KarFlowReturn { get; } = karFlowReturn;
        public double DimFlowSupply { get; } = dimFlowSupply;
        public double DimFlowReturn { get; } = dimFlowReturn;
        public double PressureGradientSupply { get; } = pressureGradientSupply;
        public double PressureGradientReturn { get; } = pressureGradientReturn;
        public double VelocitySupply { get; } = velocitySupply;
        public double VelocityReturn { get; } = velocityReturn;
        public double UtilizationRate { get; } = utilizationRate;
    }
}