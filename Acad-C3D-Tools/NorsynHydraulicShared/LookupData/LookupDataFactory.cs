using System;
using System.Collections.Generic;
using System.Text;

namespace NorsynHydraulicCalc.LookupData
{
    static class LookupDataFactory
    {
        public static ILookupData GetLookupData(MediumTypeEnum medium)
        {
            return medium switch
            {
                MediumTypeEnum.Water => new LookupDataWater(),
                MediumTypeEnum.Water72Ipa28 => new LookupDataWater72Ipa28(),
                _ => throw new NotSupportedException($"Unknown medium: {medium}")
            };
        }
    }
}
