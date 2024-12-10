using System;
using System.Collections.Generic;
using System.Text;

namespace NorsynHydraulicCalc.Pipes
{
    public class PipePertFlextra : PipeBase
    {
        public PipePertFlextra(double roughness_mm) : base(roughness_mm) { }
        protected override string Name => "PipePertFlextra";
        protected override PipeType PipeType => PipeType.PertFlextra;
        protected override string DimName => "PertFlextra ";
        protected override int OrderingPriority => 1;
    }
}
