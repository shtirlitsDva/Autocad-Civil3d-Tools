using NorsynHydraulicCalc;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.NorsynHydraulic
{
    class MediumRulesFactory
    {
        public static IMediumRules GetRules(MedieTypeEnum medium)
        {
            return medium switch
            {
                MedieTypeEnum.Water => new WaterRules(),
                MedieTypeEnum.Water75Ipa25 => new Water75Ipa25Rules(),
                _ => throw new NotSupportedException($"Unknown medium: {medium}")
            };
        }
    }
}
