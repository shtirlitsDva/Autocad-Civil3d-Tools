using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Windows;

using CommunityToolkit.Mvvm.ComponentModel;

using DimensioneringV2.GraphFeatures;

using QuikGraph;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using AcAp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace DimensioneringV2.Services
{
    internal partial class HydraulicSettingsService : ObservableObject
    {
        private static HydraulicSettingsService? _instance;
        public static HydraulicSettingsService Instance => _instance ??= new HydraulicSettingsService();
        [ObservableProperty]
        private HydraulicSettings settings;
        private HydraulicSettingsService()
        {
            settings = HydraulicSettings.Load(
                AcAp.DocumentManager.MdiActiveDocument);

            // Subscribe to DocumentManager events
            AcAp.DocumentManager.DocumentActivated += DocumentManager_DocumentActivated;
            AcAp.DocumentManager.DocumentToBeDeactivated += DocumentManager_DocumentToBeDeactivated;
            AcAp.DocumentManager.DocumentToBeDestroyed += DocumentManager_DocumentToBeDestroyed;
            //Saving of settings when the palette is made invisible
            //must be handled by the PaletteSet as this service does not know
            //when the palette is instatiated
            
        }
        private void DocumentManager_DocumentActivated(object sender, DocumentCollectionEventArgs e)
        {
            Settings = HydraulicSettings.Load(e.Document);
        }
        private void DocumentManager_DocumentToBeDeactivated(object sender, DocumentCollectionEventArgs e)
        {
            Settings.Save(e.Document);
        }
        private void DocumentManager_DocumentToBeDestroyed(object sender, DocumentCollectionEventArgs e)
        {
            if (e.Document != null)
            {
                Services.HydraulicSettingsService.Instance.Settings.Save(e.Document);
            }
        }
    }
}
