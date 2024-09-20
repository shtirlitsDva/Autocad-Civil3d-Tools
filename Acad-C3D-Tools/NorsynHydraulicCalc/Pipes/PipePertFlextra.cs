using System;
using System.Collections.Generic;
using System.Text;

namespace NorsynHydraulicCalc.Pipes
{
    internal class PipePertFlextra : PipeBase
    {
        protected override string Name => "PipePertFlextra";
        protected override PipeType PipeType => PipeType.PertFlextra;
        protected override string DimName => "PertFlextra ";
        protected override double Roughness_m => 0.00001;
    }
}
