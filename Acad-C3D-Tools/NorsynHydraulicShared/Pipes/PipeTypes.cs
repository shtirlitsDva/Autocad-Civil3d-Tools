using System;
using System.Collections.Generic;
using System.Text;

namespace NorsynHydraulicCalc.Pipes
{
    public class PipeTypes
    {
        private IHydraulicSettings? _s;
        private Dictionary<PipeType, IPipe>? _allTypes;
        public PipeTypes(IHydraulicSettings settings)
        {
            _s = settings;
            _stål = new PipeSteel(_s.RuhedSteel);            
            _aluPex = new PipeAluPex(_s.RuhedAluPEX);
            _pertFlextra = new PipePertFlextra(_s.RuhedPertFlextra);
            _cu = new PipeCu(_s.RuhedCu);
            _pe = new PipePe(_s.RuhedPe);
            _at11 = new PipeAquaTherm11(_s.RuhedAquaTherm11);
            _allTypes = new Dictionary<PipeType, IPipe>
            {
                { PipeType.Stål, _stål },
                { PipeType.AluPEX, _aluPex },
                { PipeType.PertFlextra, _pertFlextra },
                { PipeType.Kobber, _cu },
                { PipeType.Pe, _pe },
                { PipeType.AquaTherm11, _at11 }
            };
        }
        public IPipe GetPipeType(PipeType type)
        {
            if (_allTypes == null)
                throw new Exception("PipeTypes not initialized");
            return _allTypes[type];
        }
        
        /// <summary>
        /// Gets available DN values for a specific pipe type (from loaded CSV data).
        /// </summary>
        public int[] GetAvailableDnValues(PipeType type)
        {
            var pipe = GetPipeType(type);
            if (pipe is PipeBase pipeBase)
                return pipeBase.GetAvailableDnValues();
            return Array.Empty<int>();
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

        public PipeAquaTherm11 _at11;
        public PipeAquaTherm11 AT11 => _at11;
    }
}