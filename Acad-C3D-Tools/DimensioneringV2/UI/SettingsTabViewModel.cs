using Autodesk.AutoCAD.ApplicationServices;
using AcAp = Autodesk.AutoCAD.ApplicationServices.Application;

using CommunityToolkit.Mvvm.ComponentModel;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NorsynHydraulicCalc;

namespace DimensioneringV2.UI
{
    public partial class SettingsTabViewModel : ObservableObject
    {
        [ObservableProperty]
        private HydraulicSettings settings;

        public Array CalculationTypes => Enum.GetValues(typeof(CalcType));
        public Array PipeTypes => Enum.GetValues(typeof(PipeType));

        public SettingsTabViewModel()
        {
            settings = Services.HydraulicSettingsService.Instance.Settings;
        }
    }
}
