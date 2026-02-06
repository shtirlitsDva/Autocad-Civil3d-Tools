using System;
using System.Collections.Generic;
using System.Linq;
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
            _aluPexSL = new PipeAluPexSL(_s.RuhedAluPEX);
            _aluPexFL = new PipeAluPexFL(_s.RuhedAluPEX);
            _pertFlextraFL = new PipePertFlextraFL(_s.RuhedPertFlextra);
            _pertFlextraSL = new PipePertFlextraSL(_s.RuhedPertFlextra);
            _cu = new PipeCu(_s.RuhedCu);
            _pe = new PipePe(_s.RuhedPe);
            _at11 = new PipeAquaTherm11(_s.RuhedAquaTherm11);
            _allTypes = new Dictionary<PipeType, IPipe>
            {
                { PipeType.Stål, _stål },
                { PipeType.AluPEXFL, _aluPexFL },
                { PipeType.AluPEXSL, _aluPexSL },
                { PipeType.PertFlextraFL, _pertFlextraFL },
                { PipeType.PertFlextraSL, _pertFlextraSL },
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

        /// <summary>
        /// Gets all pipe types that support the given segment type and medium.
        /// Ordered by their OrderingPriority.
        /// </summary>
        public IEnumerable<PipeType> GetPipeTypesFor(SegmentType segmentType, MediumTypeEnum medium)
        {
            if (_allTypes == null)
                throw new Exception("PipeTypes not initialized");

            return _allTypes
                .Where(kvp =>
                {
                    var pipe = kvp.Value as PipeBase;
                    return pipe != null
                        && pipe.SupportedSegmentTypes.Contains(segmentType)
                        && pipe.SupportedMediumTypes.Contains(medium);
                })
                .OrderBy(kvp => (kvp.Value as PipeBase)?.OrderingPriority ?? int.MaxValue)
                .Select(kvp => kvp.Key);
        }
        private PipeSteel _stål;
        public PipeSteel Stål => _stål;

        private PipeAluPexFL _aluPexFL;
        public PipeAluPexFL AluPexFL => _aluPexFL;

        private PipeAluPexSL _aluPexSL;
        public PipeAluPexSL AluPexSL => _aluPexSL;        

        private PipePertFlextraFL _pertFlextraFL;
        public PipePertFlextraFL PertFlextraFL => _pertFlextraFL;

        private PipePertFlextraSL _pertFlextraSL;
        public PipePertFlextraSL PertFlextraSL => _pertFlextraSL;

        private PipeCu _cu;
        public PipeCu Cu => _cu;

        public PipePe _pe;
        public PipePe Pe => _pe;

        public PipeAquaTherm11 _at11;
        public PipeAquaTherm11 AT11 => _at11;
    }
}