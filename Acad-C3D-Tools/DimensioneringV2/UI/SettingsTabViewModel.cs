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
            BlockTypeFilters.Add(CreateFilterItem("El", "El", "El.svg", () => Settings.FilterEl, v => Settings.FilterEl = v));
            BlockTypeFilters.Add(CreateFilterItem("Naturgas", "Naturgas", "Naturgas.svg", () => Settings.FilterNaturgas, v => Settings.FilterNaturgas = v));
            BlockTypeFilters.Add(CreateFilterItem("Varmepumpe", "Varmepumpe", "Varmepumpe.svg", () => Settings.FilterVarmepumpe, v => Settings.FilterVarmepumpe = v));
            BlockTypeFilters.Add(CreateFilterItem("Fast brændsel", "Fast brændsel", "Fast brændsel.svg", () => Settings.FilterFastBrændsel, v => Settings.FilterFastBrændsel = v));
            BlockTypeFilters.Add(CreateFilterItem("Olie", "Olie", "Olie.svg", () => Settings.FilterOlie, v => Settings.FilterOlie = v));
            BlockTypeFilters.Add(CreateFilterItem("Fjernvarme", "Fjernvarme", "Fjernvarme.svg", () => Settings.FilterFjernvarme, v => Settings.FilterFjernvarme = v));
            BlockTypeFilters.Add(CreateFilterItem(
                new[] { "Andet", "Ingen", "UDGÅR" },
                "Andet / Ingen / UDGÅR",
                "Ingen.svg",
                () => Settings.FilterAndetIngenUdgår,
                v => Settings.FilterAndetIngenUdgår = v));
        }

        private BlockTypeFilterItem CreateFilterItem(string typeName, string displayName, string svgFileName,
            Func<bool> getter, Action<bool> setter)
        {
            var item = new BlockTypeFilterItem(typeName, displayName, svgFileName, getter());
            item.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(BlockTypeFilterItem.IsActive))
                    setter(item.IsActive);
            };
            return item;
        }

        private BlockTypeFilterItem CreateFilterItem(string[] typeNames, string displayName, string svgFileName,
            Func<bool> getter, Action<bool> setter)
        {
            var item = new BlockTypeFilterItem(typeNames, displayName, svgFileName, getter());
            item.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(BlockTypeFilterItem.IsActive))
                    setter(item.IsActive);
            };
            return item;
        }

        public RelayCommand<BlockTypeFilterItem> ToggleBlockTypeCommand => new RelayCommand<BlockTypeFilterItem>(ToggleBlockType);
        private void ToggleBlockType(BlockTypeFilterItem? item)
        {
            item?.Toggle();
        }

        public IEnumerable<string> GetActiveBlockTypes() => Settings.GetAcceptedBlockTypes();
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
