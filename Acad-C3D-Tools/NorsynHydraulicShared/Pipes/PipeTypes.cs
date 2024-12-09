using System;
using System.Collections.Generic;
using System.Text;

namespace NorsynHydraulicCalc.Pipes
{
    public class PipeTypes
    {
        private HydraulicSettings? s;
        public PipeTypes(HydraulicSettings settings)
        {
            s = settings;
            _stål = new PipeSteel(s.RuhedSteel);
            _aluPex = new PipeAluPex(s.RuhedAluPEX);
            _pertFlextra = new PipePertFlextra(s.RuhedPertFlextra);
            _cu = new PipeCu(s.RuhedCu);
        }

        private PipeSteel _stål;
        public PipeSteel Stål => _stål;

        private PipeAluPex _aluPex;
        public PipeAluPex AluPex => _aluPex;

        private PipePertFlextra _pertFlextra;
        public PipePertFlextra PertFlextra => _pertFlextra;

        private PipeCu _cu;
        public PipeCu Cu => _cu;
    }
}
