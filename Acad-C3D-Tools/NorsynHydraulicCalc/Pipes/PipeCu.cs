using System;
using System.Collections.Generic;
using System.Text;

namespace NorsynHydraulicCalc.Pipes
{
    internal class PipeCu : PipeBase
    {
        protected override string Name => "PipeCu";
        protected override PipeType PipeType => PipeType.Kobber;
        protected override string DimName => "Cu";
        protected override double Roughness_m => 0.00015;
    }
}
