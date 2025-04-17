using Autodesk.AutoCAD.ApplicationServices;
using AcAp = Autodesk.AutoCAD.ApplicationServices.Application;

using CommunityToolkit.Mvvm.ComponentModel;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NorsynHydraulicCalc;
using DimensioneringV2.GraphFeatures;
using CommunityToolkit.Mvvm.Input;
using DimensioneringV2.AutoCAD;
using DimensioneringV2.NorsynHydraulic;

namespace DimensioneringV2.UI
{
    public partial class SettingsTabViewModel : ObservableObject
    {
        [ObservableProperty]
        private HydraulicSettings settings;

        public Array MedieTypes => Enum.GetValues(typeof(MedieTypeEnum));
        public Array CalculationTypes => Enum.GetValues(typeof(CalcType));
        public Array PipeTypes => Enum.GetValues(typeof(PipeType));

        #region Implementation of system switching
        partial void OnSettingsChanged(HydraulicSettings value)
        {
            value.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(value.MedieType))
                {
                    var rules = MediumRulesFactory.GetRules(value.MedieType);
                    rules.ApplyDefaults(value);

                    OnPropertyChanged(nameof(ValidPipeTypesFL));
                    OnPropertyChanged(nameof(ValidPipeTypesSL));
                    OnPropertyChanged(nameof(IsPipeTypeFLSelectable));
                    OnPropertyChanged(nameof(IsPipeTypeSLSelectable));
                }
            };
        }
        public IEnumerable<PipeType> ValidPipeTypesFL
        => MediumRulesFactory.GetRules(Settings.MedieType).GetValidPipeTypesForSupply();

        public IEnumerable<PipeType> ValidPipeTypesSL
            => MediumRulesFactory.GetRules(Settings.MedieType).GetValidPipeTypesForService();

        public bool IsPipeTypeFLSelectable => ValidPipeTypesFL.Count() > 1;
        public bool IsPipeTypeSLSelectable => ValidPipeTypesSL.Count() > 1;
        #endregion

        public SettingsTabViewModel()
        {
            settings = Services.HydraulicSettingsService.Instance.Settings;
            Services.HydraulicSettingsService.Instance.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(Services.HydraulicSettingsService.Settings))
                {
                    Settings = Services.HydraulicSettingsService.Instance.Settings;
                    OnPropertyChanged(nameof(Settings));
                }
            };
        }

        public AsyncRelayCommand SaveSettingsCommand => new AsyncRelayCommand(SaveSettings);

        private async Task SaveSettings()
        {
            Services.HydraulicSettingsService.Instance.Settings = Settings;
            HydraulicSettingsSerializer.Save(
                AcAp.DocumentManager.MdiActiveDocument,
                Services.HydraulicSettingsService.Instance.Settings);
        }
    }
}