using System;
using System.Collections.Generic;
using System.Text;

namespace NorsynHydraulicCalc.Pipes
{
    internal class PipeSteel : PipeBase
    {
        protected override string Name => "PipeSteel";
        protected override PipeType PipeType => PipeType.Stål;
        protected override string DimName => "DN";
        protected override double Roughness_m => 0.0001;
    }
}
