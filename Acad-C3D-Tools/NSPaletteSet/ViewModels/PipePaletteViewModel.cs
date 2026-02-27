using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using IntersectUtilities.PipeScheduleV2;
using IntersectUtilities.UtilsCommon.Enums;

using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace NSPaletteSet.ViewModels
{
    public partial class PipePaletteViewModel : ObservableObject
    {
        private readonly Properties.Settings _settings;

        public ObservableCollection<PipeSystemTypeCombination> PipeSystemTypes { get; } = new();
        public ObservableCollection<int> AvailableDns { get; } = new();
        public ObservableCollection<PipeSeriesEnum> AvailableSeries { get; } = new();

        [ObservableProperty]
        private PipeSystemTypeCombination? _selectedPipeSystemType;

        [ObservableProperty]
        private int? _selectedDn;

        [ObservableProperty]
        private PipeSeriesEnum? _selectedSeries;

        [ObservableProperty]
        private bool _hasMultipleSeries;

        [ObservableProperty]
        private bool _hasFremRetur;

        [ObservableProperty]
        private bool _isFremSelected = true;

        [ObservableProperty]
        private string _dnGroupHeader = string.Empty;

        public PipePaletteViewModel()
        {
            _settings = Properties.Settings.Default;

            PaletteUtils.CurrentSeries = PipeSeriesEnum.S3;

            PopulatePipeSystemTypes();
            RestoreSelectedIndex();
            RebuildForCurrentSelection();
        }

        private void PopulatePipeSystemTypes()
        {
            foreach (PipeSystemEnum system in Enum.GetValues(typeof(PipeSystemEnum))
                .Cast<PipeSystemEnum>().Skip(1))
            {
                foreach (PipeTypeEnum type in PipeScheduleV2.GetPipeSystemAvailableTypes(system))
                {
                    PipeSystemTypes.Add(new PipeSystemTypeCombination(system, type));
                }
            }
        }

        private void RestoreSelectedIndex()
        {
            int savedIndex = _settings.pipePalette_PipeSystemTypeIndex;
            if (savedIndex < 0 || savedIndex >= PipeSystemTypes.Count)
                savedIndex = 0;

            if (PipeSystemTypes.Count > 0)
                SelectedPipeSystemType = PipeSystemTypes[savedIndex];
        }

        partial void OnSelectedPipeSystemTypeChanged(PipeSystemTypeCombination? value)
        {
            if (value == null) return;

            int index = PipeSystemTypes.IndexOf(value);
            _settings.pipePalette_PipeSystemTypeIndex = index;
            _settings.Save();

            RebuildForCurrentSelection();
        }

        private void RebuildForCurrentSelection()
        {
            var comb = SelectedPipeSystemType;
            if (comb == null) return;

            // Reset selections before repopulating
            SelectedDn = null;
            SelectedSeries = null;

            // Populate series
            var seriesList = PipeScheduleV2.ListAllSeriesForPipeSystemType(comb.System, comb.Type).ToList();
            AvailableSeries.Clear();
            foreach (var s in seriesList)
                AvailableSeries.Add(s);

            HasMultipleSeries = seriesList.Count > 1;

            if (HasMultipleSeries)
            {
                // Select highest series (S3 > S2 > S1), matching old behavior
                SelectedSeries = seriesList.OrderBy(x => x).Last();
            }
            else
            {
                PaletteUtils.CurrentSeries = PipeSeriesEnum.S3;
            }

            // Determine Frem/Retur availability
            HasFremRetur = comb.Type == PipeTypeEnum.Frem
                        || comb.Type == PipeTypeEnum.Retur
                        || comb.Type == PipeTypeEnum.Enkelt;

            if (HasFremRetur)
            {
                IsFremSelected = true;
            }

            // Populate DN values
            var dnList = PipeScheduleV2.ListAllDnsForPipeSystemType(comb.System, comb.Type)
                .OrderBy(x => x).ToList();
            AvailableDns.Clear();
            foreach (var dn in dnList)
                AvailableDns.Add(dn);

            DnGroupHeader = comb.ToString();
        }

        partial void OnSelectedDnChanged(int? value)
        {
            if (value == null || SelectedPipeSystemType == null) return;

            var comb = SelectedPipeSystemType;
            PaletteUtils.ActivateLayer(comb.System, comb.Type, value.Value.ToString());
        }

        partial void OnSelectedSeriesChanged(PipeSeriesEnum? value)
        {
            if (value == null) return;
            PaletteUtils.CurrentSeries = value.Value;
        }

        partial void OnIsFremSelectedChanged(bool value)
        {
            if (SelectedPipeSystemType == null) return;

            SelectedPipeSystemType.Type = value ? PipeTypeEnum.Frem : PipeTypeEnum.Retur;
        }

        [RelayCommand]
        private void UpdateWidths() => PaletteUtils.UpdateWidths();

        [RelayCommand]
        private void ResetWidths() => PaletteUtils.ResetWidths();

        [RelayCommand]
        private void SetLabel() => PaletteUtils.labelpipe();
    }
}
