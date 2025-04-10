﻿using Autodesk.AutoCAD.ApplicationServices;
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