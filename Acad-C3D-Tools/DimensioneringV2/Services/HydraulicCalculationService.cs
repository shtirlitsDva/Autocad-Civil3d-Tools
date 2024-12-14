using NorsynHydraulicCalc;

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.Services
{
    internal class HydraulicCalculationService
    {
        private static HydraulicCalc hc;
        internal static void Initialize() => hc = new HydraulicCalc(HydraulicSettingsService.Instance.Settings);
        internal static HydraulicCalc Calc => hc;
    }
}
