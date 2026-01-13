using Autodesk.AutoCAD.ApplicationServices;
using AcAp = Autodesk.AutoCAD.ApplicationServices.Application;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using DimensioneringV2.AutoCAD;
using DimensioneringV2.Services;
using DimensioneringV2.UI.Nyttetimer;
using DimensioneringV2.UI.PipeSettings;

using NorsynHydraulicCalc;

using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace DimensioneringV2.UI
{
    public partial class SettingsTabViewModel : ObservableObject
    {
        [ObservableProperty]
        private HydraulicSettings settings;

        public Array MedieTypes => Enum.GetValues(typeof(MediumTypeEnum));
        public Array CalculationTypes => Enum.GetValues(typeof(CalcType));

        /// <summary>
        /// The name of the currently selected Nyttetimer configuration.
        /// </summary>
        public string CurrentNyttetimerConfigName => NyttetimerService.Instance.CurrentConfiguration.Name;

        #region Settings Property Change Handling
        partial void OnSettingsChanged(HydraulicSettings value)
        {
            // Ensure pipe configurations are initialized
            value.EnsureInitialized();

            value.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(value.UseBrugsvandsprioritering))
                {
                    OnPropertyChanged(nameof(IsFactorTillægEditable));
                }
            };
        }

        public bool IsFactorTillægEditable => !Settings.UseBrugsvandsprioritering;
        #endregion

        public SettingsTabViewModel()
        {
            Settings = Services.HydraulicSettingsService.Instance.Settings;
        }

        #region Save/Observe Commands
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
        #endregion

        #region Roughness Command
        public RelayCommand EditRoughnessCommand => new RelayCommand(EditRoughness);
        private void EditRoughness()
        {
            var w = new RoughnessSettingsWindow();
            w.DataContext = this;
            w.ShowDialog();
        }
        #endregion

        #region Nyttetimer Command
        public RelayCommand EditNyttetimerConfigCommand => new RelayCommand(EditNyttetimerConfig);
        private void EditNyttetimerConfig()
        {
            var w = new ConfigurationSelectorWindow();
            w.ShowDialog();
            // Update the displayed name after dialog closes
            OnPropertyChanged(nameof(CurrentNyttetimerConfigName));
        }
        #endregion

        #region Pipe Settings Commands
        public RelayCommand EditPipeSettingsFLCommand => new RelayCommand(EditPipeSettingsFL);
        private void EditPipeSettingsFL()
        {
            // Ensure configuration exists
            if (Settings.PipeConfigFL == null)
            {
                Settings.PipeConfigFL = DefaultPipeConfigFactory.CreateDefaultFL(Settings.MedieType, Settings.GetPipeTypes());
            }

            var window = new PipeSettingsWindow();
            var viewModel = new PipeSettingsViewModel(Settings.PipeConfigFL, Settings.MedieType, Settings.GetPipeTypes());
            window.DataContext = viewModel;
            window.ShowDialog();
        }

        public RelayCommand EditPipeSettingsSLCommand => new RelayCommand(EditPipeSettingsSL);
        private void EditPipeSettingsSL()
        {
            // Ensure configuration exists
            if (Settings.PipeConfigSL == null)
            {
                Settings.PipeConfigSL = DefaultPipeConfigFactory.CreateDefaultSL(Settings.MedieType, Settings.GetPipeTypes());
            }

            var window = new PipeSettingsWindow();
            var viewModel = new PipeSettingsViewModel(Settings.PipeConfigSL, Settings.MedieType, Settings.GetPipeTypes());
            window.DataContext = viewModel;
            window.ShowDialog();
        }
        #endregion
    }
}
