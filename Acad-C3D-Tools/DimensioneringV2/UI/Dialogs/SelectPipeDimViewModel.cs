using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using DimensioneringV2.Services;

using NorsynHydraulicCalc;
using NorsynHydraulicCalc.Pipes;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace DimensioneringV2.UI.Dialogs
{
    public partial class SelectPipeDimViewModel : ObservableObject
    {
        public enum CloseReason { Ok, Retry, Cancel }
        public event EventHandler<CloseReason> CloseRequested;

        public ObservableCollection<PipeType> PipeTypes { get; } = new();
        public ObservableCollection<int> NominalDiameters { get; } = new();

        [ObservableProperty]
        private PipeType selectedPipeType;

        partial void OnSelectedPipeTypeChanged(PipeType value)
        {
            LoadNominals();
        }

        [ObservableProperty]
        private int selectedNominal;

        public SelectPipeDimViewModel()
        {
            LoadPipeTypes();
        }

        private void LoadPipeTypes()
        {
            PipeTypes.Clear();
            // Use settings-backed PipeTypes in shared library
            var settings = HydraulicSettingsService.Instance.Settings;
            var types = new NorsynHydraulicCalc.Pipes.PipeTypes(settings);
            PipeTypes.Add(PipeType.Stål);
            PipeTypes.Add(PipeType.AluPEX);
            PipeTypes.Add(PipeType.PertFlextra);
            PipeTypes.Add(PipeType.Kobber);
            PipeTypes.Add(PipeType.Pe);
            SelectedPipeType = PipeTypes.FirstOrDefault();
        }

        private void LoadNominals()
        {
            NominalDiameters.Clear();
            var settings = HydraulicSettingsService.Instance.Settings;
            var types = new NorsynHydraulicCalc.Pipes.PipeTypes(settings);
            IEnumerable<Dim> dims = SelectedPipeType switch
            {
                PipeType.Stål => types.Stål.GetAllDimsSorted(),
                PipeType.AluPEX => types.AluPex.GetAllDimsSorted(),
                PipeType.PertFlextra => types.PertFlextra.GetAllDimsSorted(),
                PipeType.Kobber => types.Cu.GetAllDimsSorted(),
                PipeType.Pe => types.Pe.GetAllDimsSorted(),
                _ => Enumerable.Empty<Dim>()
            };
            foreach (var d in dims) NominalDiameters.Add(d.NominalDiameter);
            SelectedNominal = NominalDiameters.FirstOrDefault();
        }

        public IRelayCommand OkCommand => new RelayCommand(() => CloseRequested?.Invoke(this, CloseReason.Ok));
        public IRelayCommand RetryCommand => new RelayCommand(() => CloseRequested?.Invoke(this, CloseReason.Retry));
    }
}


