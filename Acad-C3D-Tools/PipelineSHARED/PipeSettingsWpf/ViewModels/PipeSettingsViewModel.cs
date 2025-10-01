using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntersectUtilities.PipelineNetworkSystem;
using static IntersectUtilities.UtilsCommon.Utils;
using IntersectUtilities.UtilsCommon.Enums;

namespace IntersectUtilities.Forms.PipeSettingsWpf.ViewModels
{
    internal partial class PipeSettingsViewModel : ObservableObject
    {
        private PipeSettings _model;

        [ObservableProperty]
        private string title;

        // The top-level collection: each "system" gets a child VM
        public ObservableCollection<PipeSettingSystemViewModel> Systems { get; }
            = new ObservableCollection<PipeSettingSystemViewModel>();

        public PipeSettingsViewModel()
        {
            Title = "No model loaded yet";
        }

        public void LoadModel(PipeSettings model)
        {
            _model = model;
            Title = model?.Name ?? "Unnamed PipeSettings";

            Systems.Clear();
            if (model == null) return;

            // Convert each system in the model
            foreach (var system in model.Settings.Values)
            {
                var sysVm = new PipeSettingSystemViewModel
                {
                    SystemName = system.Name,
                    PipeTypeSystem = system.PipeTypeSystem
                };

                // Each pipe type in that system
                foreach (var kvpType in system.Settings)
                {
                    var pipeTypeEnum = kvpType.Key; // e.g. PipeTypeEnum.Twin
                    var pipeSettingType = kvpType.Value; // e.g. { Name=Twin, Settings=[DN->length] }

                    var typeVm = new PipeSettingTypeViewModel
                    {
                        TypeName = pipeTypeEnum.ToString()
                    };

                    // For each DN size in that type
                    foreach (var sizeEntry in pipeSettingType.Settings)
                    {
                        int sizeDn = sizeEntry.Key;
                        double currentLength = sizeEntry.Value;

                        // Retrieve possible options from your static method 
                        // (substitute your actual class/namespace)
                        var possibleOptions = PipeScheduleV2.PipeScheduleV2
                            .GetStdLengthsForSystem(system.PipeTypeSystem);

                        // If only 1 option, you can skip or automatically assign, 
                        // just like your WinForms logic
                        if (possibleOptions.Length <= 1) continue;

                        var sizeVm = new PipeSettingSizeViewModel
                        {
                            Size = sizeDn,
                            SelectedOption = currentLength,
                        };

                        // Fill the Options collection
                        foreach (var lengthOpt in possibleOptions)
                            sizeVm.Options.Add(new OptionItemViewModel(lengthOpt, sizeVm));

                        typeVm.Sizes.Add(sizeVm);
                    }

                    sysVm.Types.Add(typeVm);
                }

                Systems.Add(sysVm);
            }
        }

        // "OK" button → Save changes to the original model, then close
        [RelayCommand]
        private void SaveAndClose(Window window)
        {
            // Write back user changes from the VMs → the _model
            foreach (var systemVm in Systems)
            {
                if (!_model.Settings.ContainsKey(systemVm.SystemName))
                    continue;

                var systemModel = _model.Settings[systemVm.SystemName];

                foreach (var typeVm in systemVm.Types)
                {
                    if (!Enum.TryParse(typeVm.TypeName, out PipeTypeEnum typeEnum))
                        continue;

                    var typeModel = systemModel.Settings[typeEnum];

                    foreach (var sizeVm in typeVm.Sizes)
                    {
                        // Overwrite the DN’s default length
                        typeModel.Settings[sizeVm.Size] = sizeVm.SelectedOption;
                    }
                }
            }

            // If you want to persist to disk, do it here:
            // _model.Save(...);

            window?.Close();
        }

        // "Cancel" button → close without saving
        [RelayCommand]
        private void Cancel(Window window)
        {
            window?.Close();
        }
    }

}