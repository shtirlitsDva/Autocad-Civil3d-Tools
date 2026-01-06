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
using DimensioneringV2.Services;
using DimensioneringV2.UI.Nyttetimer;

namespace DimensioneringV2.UI
{
    public partial class SettingsTabViewModel : ObservableObject
    {
        [ObservableProperty]
        private HydraulicSettings settings;

        public Array MedieTypes => Enum.GetValues(typeof(MediumTypeEnum));
        public Array CalculationTypes => Enum.GetValues(typeof(CalcType));
        public Array PipeTypes => Enum.GetValues(typeof(PipeType));

        /// <summary>
        /// The name of the currently selected Nyttetimer configuration.
        /// </summary>
        public string CurrentNyttetimerConfigName => NyttetimerService.Instance.CurrentConfiguration.Name;

        #region Implementation of medium switching
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

                    if (!rules.SupportsPertFlextra) value.UsePertFlextraFL = false;
                    OnPropertyChanged(nameof(IsPertFlextraSelectable));
                }
                else if (e.PropertyName == nameof(value.UseBrugsvandsprioritering))
                {
                    OnPropertyChanged(nameof(IsFactorTillægEditable));
                }
            };
        }
        public IEnumerable<PipeType> ValidPipeTypesFL
            => MediumRulesFactory.GetRules(Settings.MedieType).GetValidPipeTypesForSupply();

        public IEnumerable<PipeType> ValidPipeTypesSL
            => MediumRulesFactory.GetRules(Settings.MedieType).GetValidPipeTypesForService();

        public bool IsPipeTypeFLSelectable => ValidPipeTypesFL.Count() > 1;
        public bool IsPipeTypeSLSelectable => ValidPipeTypesSL.Count() > 1;
        public bool IsPertFlextraSelectable =>
            MediumRulesFactory.GetRules(Settings.MedieType).SupportsPertFlextra;

        public bool IsFactorTillægEditable => !Settings.UseBrugsvandsprioritering;
        #endregion

        public SettingsTabViewModel()
        {
            Settings = Services.HydraulicSettingsService.Instance.Settings;            
        }

        public AsyncRelayCommand SaveSettingsCommand => new AsyncRelayCommand(SaveSettings);

        private async Task SaveSettings()
        {   
            Utils.prtDbg($"Saving settings to {AcAp.DocumentManager.MdiActiveDocument.Name}");
            HydraulicSettingsSerializer.Save(
                AcAp.DocumentManager.MdiActiveDocument,
                Settings);
        }

        public RelayCommand ObserveSettingsCommand => new RelayCommand(ObserveSettings);
        private void ObserveSettings()
        {
            var w = new SettingsObserverWindow();
            w.Init(Settings);
            w.Show();
        }

        public RelayCommand EditRoughnessCommand => new RelayCommand(EditRoughness);
        private void EditRoughness()
        {
            var w = new RoughnessSettingsWindow();
            w.DataContext = this;
            w.ShowDialog();
        }

        public RelayCommand EditNyttetimerConfigCommand => new RelayCommand(EditNyttetimerConfig);
        private void EditNyttetimerConfig()
        {
            var w = new ConfigurationSelectorWindow();
            w.ShowDialog();
            // Update the displayed name after dialog closes
            OnPropertyChanged(nameof(CurrentNyttetimerConfigName));
        }
    }
}