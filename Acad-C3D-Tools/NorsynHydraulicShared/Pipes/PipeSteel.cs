using System;
using System.Collections.Generic;
using System.Text;

namespace NorsynHydraulicCalc.Pipes
{
    public class PipeSteel : PipeBase
    {
        public PipeSteel(double roughness_mm) : base(roughness_mm) {}
        protected override string Name => "PipeSteel";
        protected override PipeType PipeType => PipeType.Stål;
        protected override string DimName => "DN ";
        protected override int OrderingPriority => 2;
    }
}
