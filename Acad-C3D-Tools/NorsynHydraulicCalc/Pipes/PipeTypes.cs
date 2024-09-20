using System;
using System.Collections.Generic;
using System.Text;

namespace NorsynHydraulicCalc.Pipes
{
    internal static class PipeTypes
    {
        public static PipeSteel Stål = new PipeSteel();
        public static PipeAluPex AluPex = new PipeAluPex();
        public static PipePertFlextra PertFlextra = new PipePertFlextra();
        public static PipeCu Cu = new PipeCu();
    }
}
