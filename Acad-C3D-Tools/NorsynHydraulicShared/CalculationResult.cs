using NorsynHydraulicCalc.Pipes;

namespace NorsynHydraulicCalc
{
    public struct CalculationResult
    {
        public string SegmentType { get; }
        public Dim Dim { get; }
        public double ReynoldsSupply { get; }
        public double ReynoldsReturn { get; }
        public double FlowSupply { get; }
        public double FlowReturn { get; }
        public double PressureGradientSupply { get; }
        public double PressureGradientReturn { get; }
        public double VelocitySupply { get; }
        public double VelocityReturn { get; }
        public double UtilizationRate { get; }

        public CalculationResult(
            string segmentType, Dim dim, 
            double reynoldsSupply, double reynoldsReturn, double flowSupply, 
            double flowReturn, double pressureGradientSupply, double pressureGradientReturn, 
            double velocitySupply, double velocityReturn, double utilizationRate)
        {
            SegmentType = segmentType;
            Dim = dim;
            ReynoldsSupply = reynoldsSupply;
            ReynoldsReturn = reynoldsReturn;
            FlowSupply = flowSupply;
            FlowReturn = flowReturn;
            PressureGradientSupply = pressureGradientSupply;
            PressureGradientReturn = pressureGradientReturn;
            VelocitySupply = velocitySupply;
            VelocityReturn = velocityReturn;
            UtilizationRate = utilizationRate;
        }
    }
}