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
        public static IMediumRules GetRules(MediumTypeEnum medium)
        {
            return medium switch
            {
                MediumTypeEnum.Water => new WaterRules(),
                MediumTypeEnum.Water72Ipa28 => new Water72Ipa28Rules(),
                _ => throw new NotSupportedException($"Unknown medium: {medium}")
            };
        }
    }
}
