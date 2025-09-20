using System;
using System.Collections.Generic;
using System.Text;

namespace NorsynHydraulicCalc.Pipes
{
    public static class PipeTypes
    {
        private static Lazy<PipeSteel> _stål = new Lazy<PipeSteel>(() => new PipeSteel());
        public static PipeSteel Stål => _stål.Value;

        private static Lazy<PipeAluPex> _aluPex = new Lazy<PipeAluPex>(() => new PipeAluPex());
        public static PipeAluPex AluPex => _aluPex.Value;

        private static Lazy<PipePertFlextra> _pertFlextra = new Lazy<PipePertFlextra>(() => new PipePertFlextra());
        public static PipePertFlextra PertFlextra => _pertFlextra.Value;

        private static Lazy<PipeCu> _cu = new Lazy<PipeCu>(() => new PipeCu());
        public static PipeCu Cu => _cu.Value;
    }
}
