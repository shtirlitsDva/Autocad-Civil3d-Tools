using DimensioneringV2.AutoCAD;

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
        internal static void Initialize() =>
            Initialize(HydraulicSettingsService.Instance.Settings);

        internal static void Initialize(HydraulicSettings settings)
        {
            hc = settings.ReportToConsole
                ? new HydraulicCalc(settings, new LoggerFile())
                : new HydraulicCalc(settings, new LoggerAcConsole());
        }
            
        internal static HydraulicCalc Calc => hc;
    }
}
