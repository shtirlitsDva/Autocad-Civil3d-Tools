using NorsynHydraulicCalc;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.NorsynHydraulic
{
    class WaterRules : IMediumRules
    {
        public bool SupportsPertFlextra => true;

        public void ApplyDefaults(HydraulicSettings settings)
        {
            settings.PipeTypeFL = PipeType.Stål;
            settings.PipeTypeSL = PipeType.AluPEX;
        }

        public IEnumerable<PipeType> GetValidPipeTypesForService()
        {
            return Enum.GetValues(typeof(PipeType))
                .Cast<PipeType>().Where(p => p != PipeType.Pe);
        }

        public IEnumerable<PipeType> GetValidPipeTypesForSupply()
        {
            yield return PipeType.Stål;
        }
    }
}