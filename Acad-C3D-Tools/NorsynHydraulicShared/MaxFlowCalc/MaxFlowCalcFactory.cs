using System;
using System.Collections.Generic;
using System.Text;

namespace NorsynHydraulicCalc.MaxFlowCalc
{
    static class MaxFlowCalcFactory
    {
        public static IMaxFlowCalc GetMaxFlowCalc(MediumTypeEnum medium, IHydraulicSettings s)
        {
            return medium switch
            {
                MediumTypeEnum.Water => new MaxFlowCalcWater(s),
                MediumTypeEnum.Water72Ipa28 => new MaxFlowCalcWater72Ipa28(s),
                _ => throw new NotSupportedException($"Unknown medium: {medium}")
            };
        }
    }
}