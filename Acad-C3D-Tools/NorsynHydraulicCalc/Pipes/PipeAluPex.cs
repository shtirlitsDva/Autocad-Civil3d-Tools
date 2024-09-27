using System;
using System.Collections.Generic;
using System.Text;

namespace NorsynHydraulicCalc.Pipes
{
    public class PipeAluPex : PipeBase
    {
        protected override string Name => "PipeAluPex";
        protected override PipeType PipeType => PipeType.AluPEX;
        protected override string DimName => "AluPEX ";
        protected override double Roughness_m => 0.00001;
    }
}
