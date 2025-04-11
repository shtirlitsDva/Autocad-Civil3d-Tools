using System;
using System.Collections.Generic;
using System.Text;

namespace NorsynHydraulicCalc.Pipes
{
    public class PipeTypes
    {
        private IHydraulicSettings? s;
        public PipeTypes(IHydraulicSettings settings)
        {
            s = settings;
            _stål = new PipeSteel(s.RuhedSteel);
            _aluPex = new PipeAluPex(s.RuhedAluPEX);
            _pertFlextra = new PipePertFlextra(s.RuhedPertFlextra);
            _cu = new PipeCu(s.RuhedCu);
            _pe = new PipePe(s.RuhedPe);
        }

        private PipeSteel _stål;
        public PipeSteel Stål => _stål;

        private PipeAluPex _aluPex;
        public PipeAluPex AluPex => _aluPex;

        private PipePertFlextra _pertFlextra;
        public PipePertFlextra PertFlextra => _pertFlextra;

        private PipeCu _cu;
        public PipeCu Cu => _cu;

        public PipePe _pe;
        public PipePe Pe => _pe;
    }
}