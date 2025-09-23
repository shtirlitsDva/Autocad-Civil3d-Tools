using Autodesk.AutoCAD.ApplicationServices;
using AcAp = Autodesk.AutoCAD.ApplicationServices.Application;

using CommunityToolkit.Mvvm.ComponentModel;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.Input;
using DimensioneringV2.UI.Graph;
using DimensioneringV2.Services;

namespace DimensioneringV2.UI
{
    public partial class GraphSettingsTabViewModel : ObservableObject
    {
        [ObservableProperty]
        private GraphSettings settings;

        public GraphSettingsTabViewModel()
        {
            Settings = Services.GraphSettingsService.Instance.Settings;            
        }

        public AsyncRelayCommand SaveSettingsCommand => new AsyncRelayCommand(SaveSettings);

        private async Task SaveSettings()
        {   
            Utils.prtDbg($"Saving settings to {AcAp.DocumentManager.MdiActiveDocument.Name}");
            SettingsSerializer<GraphSettings>.Save(
                AcAp.DocumentManager.MdiActiveDocument,
                Settings);
        }        
    }
}