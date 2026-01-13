using System;
using System.Collections.Generic;
using System.Text;

namespace NorsynHydraulicCalc.Pipes
{
    public class PipeAquaTherm11 : PipeBase
    {
        public PipeAquaTherm11(double roughness_mm) : base(roughness_mm) { }
        protected override string Name => "PipeAquaTherm11";
        protected override PipeType PipeType => PipeType.AquaTherm11;
        protected override string DimName => "AT";
        protected override int OrderingPriority => 1;
        protected override double PricePerStk => 13000;
    }
}
