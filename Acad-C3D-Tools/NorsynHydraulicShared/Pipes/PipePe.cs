using System;
using System.Collections.Generic;
using System.Text;

namespace NorsynHydraulicCalc.Pipes
{
    public class PipePe : PipeBase
    {
        public PipePe(double roughness_mm) : base(roughness_mm) { }
        protected override string Name => "PipePe";
        protected override PipeType PipeType => PipeType.Pe;
        protected override string DimName => "PE";
        protected override int OrderingPriority => 1;
        protected override double PricePerStk => 13000;
    }
}
