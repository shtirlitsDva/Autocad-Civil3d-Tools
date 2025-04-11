using NorsynHydraulicCalc;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.NorsynHydraulic
{
    class Water75Ipa25Rules : IMediumRules
    {
        public void ApplyDefaults(HydraulicSettings settings)
        {
            settings.PipeTypeFL = PipeType.Pe;
            settings.PipeTypeSL = PipeType.Pe;
        }

        public IEnumerable<PipeType> GetValidPipeTypesForService()
        {
            yield return PipeType.Pe;
        }

        public IEnumerable<PipeType> GetValidPipeTypesForSupply()
        {
            yield return PipeType.Pe;
        }
    }
}
