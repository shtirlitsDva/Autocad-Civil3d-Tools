using System;
using System.Collections.Generic;
using System.Text;

namespace NorsynHydraulicCalc.Pipes
{
    public class PipeAluPex : PipeBase
    {
        public PipeAluPex(double roughness_mm) : base(roughness_mm) { }
        protected override string Name => "PipeAluPex";
        protected override PipeType PipeType => PipeType.AluPEX;
        protected override string DimName => "AluPEX ";
        protected override int OrderingPriority => 0;
    }
}
