using System;
using System.Collections.Generic;
using System.Text;

namespace NorsynHydraulicCalc.Pipes
{
    public class PipeCu : PipeBase
    {
        public PipeCu(double roughness_mm) : base(roughness_mm) { }
        protected override string Name => "PipeCu";
        protected override PipeType PipeType => PipeType.Kobber;
        protected override string DimName => "Cu ";
        protected override int OrderingPriority => 0;
    }
}
