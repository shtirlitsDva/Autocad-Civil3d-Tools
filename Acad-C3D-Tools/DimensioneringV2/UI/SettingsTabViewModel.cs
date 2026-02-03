using Autodesk.AutoCAD.ApplicationServices;
using AcAp = Autodesk.AutoCAD.ApplicationServices.Application;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using DimensioneringV2.AutoCAD;
using DimensioneringV2.Services;
using DimensioneringV2.UI.Nyttetimer;
using DimensioneringV2.UI.PipeSettings;

using NorsynHydraulicCalc;
using NorsynHydraulicCalc.Rules;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
            InitializeBlockTypeFilters();
        }

        #region Block Type Filter
        public ObservableCollection<BlockTypeFilterItem> BlockTypeFilters { get; } = new();

        private void InitializeBlockTypeFilters()
        {
            BlockTypeFilters.Add(new BlockTypeFilterItem("El", "El", "_El"));
            BlockTypeFilters.Add(new BlockTypeFilterItem("Naturgas", "Naturgas", "_Naturgas"));
            BlockTypeFilters.Add(new BlockTypeFilterItem("Varmepumpe", "Varmepumpe", "_Varmepumpe"));
            BlockTypeFilters.Add(new BlockTypeFilterItem("Fast brændsel", "Fast brændsel", "_Fast brændsel"));
            BlockTypeFilters.Add(new BlockTypeFilterItem("Olie", "Olie", "_Olie"));
            BlockTypeFilters.Add(new BlockTypeFilterItem("Andet", "Andet", "_Ingen"));
            BlockTypeFilters.Add(new BlockTypeFilterItem("Fjernvarme", "Fjernvarme", "_Fjernvarme"));
            BlockTypeFilters.Add(new BlockTypeFilterItem("Ingen", "Ingen", "_Ingen"));
            BlockTypeFilters.Add(new BlockTypeFilterItem("UDGÅR", "UDGÅR", "_Ingen"));
        }

        public RelayCommand<BlockTypeFilterItem> ToggleBlockTypeCommand => new RelayCommand<BlockTypeFilterItem>(ToggleBlockType);
        private void ToggleBlockType(BlockTypeFilterItem? item)
        {
            item?.Toggle();
        }

        public IEnumerable<string> GetActiveBlockTypes() => BlockTypeFilters.Where(f => f.IsActive).Select(f => f.TypeName);
        #endregion

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

            // Remember FL pipe types before editing
            var flPipeTypesBefore = Settings.PipeConfigFL.GetConfiguredPipeTypes().ToHashSet();

            var window = new PipeSettingsWindow();
            var viewModel = new PipeSettingsViewModel(Settings.PipeConfigFL, Settings.MedieType, Settings.GetPipeTypes());
            window.DataContext = viewModel;

            if (window.ShowDialog() == true)
            {
                // Check if any FL pipe types were removed that are used in SL rules
                ValidateAndCleanupSlRulesAfterFlChange(flPipeTypesBefore);
            }
        }

        /// <summary>
        /// Validates SL rules after FL configuration changes.
        /// Removes any ParentPipeRules that reference removed FL pipe types.
        /// </summary>
        private void ValidateAndCleanupSlRulesAfterFlChange(HashSet<PipeType> flPipeTypesBefore)
        {
            if (Settings.PipeConfigSL == null) return;

            var flPipeTypesAfter = Settings.PipeConfigFL?.GetConfiguredPipeTypes().ToHashSet()
                ?? new HashSet<PipeType>();

            var removedFlTypes = flPipeTypesBefore.Except(flPipeTypesAfter).ToList();
            if (removedFlTypes.Count == 0) return;

            var invalidatedRules = new List<string>();

            foreach (var slPriority in Settings.PipeConfigSL.Priorities)
            {
                var rulesToRemove = slPriority.Rules
                    .OfType<ParentPipeRule>()
                    .Where(r => removedFlTypes.Contains(r.ParentPipeType))
                    .ToList();

                foreach (var rule in rulesToRemove)
                {
                    invalidatedRules.Add($"{slPriority.PipeType}: Forældrerør {rule.ParentPipeType}");
                    slPriority.Rules.Remove(rule);
                }
            }

            if (invalidatedRules.Count > 0)
            {
                MessageBox.Show(
                    $"Følgende SL-regler er blevet fjernet, fordi deres forældrerørtype ikke længere findes i FL:\n\n" +
                    string.Join("\n", invalidatedRules),
                    "Regler fjernet",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        public RelayCommand EditPipeSettingsSLCommand => new RelayCommand(EditPipeSettingsSL);
        private void EditPipeSettingsSL()
        {
            // Ensure configurations exist
            if (Settings.PipeConfigFL == null)
            {
                Settings.PipeConfigFL = DefaultPipeConfigFactory.CreateDefaultFL(Settings.MedieType, Settings.GetPipeTypes());
            }
            if (Settings.PipeConfigSL == null)
            {
                Settings.PipeConfigSL = DefaultPipeConfigFactory.CreateDefaultSL(Settings.MedieType, Settings.GetPipeTypes());
            }

            // Get FL pipe types for parent pipe rules
            var flPipeTypes = Settings.PipeConfigFL.GetConfiguredPipeTypes();

            var window = new PipeSettingsWindow();
            var viewModel = new PipeSettingsViewModel(Settings.PipeConfigSL, Settings.MedieType, Settings.GetPipeTypes(), flPipeTypes);
            window.DataContext = viewModel;
            window.ShowDialog();
        }
        #endregion
    }
}
