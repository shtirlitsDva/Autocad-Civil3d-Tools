using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Windows;

using CommunityToolkit.Mvvm.ComponentModel;

using DimensioneringV2.AutoCAD;
using DimensioneringV2.GraphFeatures;
using DimensioneringV2.UI.Graph;

using QuikGraph;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using AcAp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace DimensioneringV2.Services
{
    internal partial class GraphSettingsService : ObservableObject
    {
        private static GraphSettingsService? _instance;
        public static GraphSettingsService Instance => _instance ??= new GraphSettingsService();
        [ObservableProperty]
        private GraphSettings settings;
        private GraphSettingsService()
        {
            settings = SettingsSerializer<GraphSettings>.Load(
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
            if (e.Document == null) return;
            //Utils.prtDbg($"{DateTime.Now} Document activated event: {e.Document.Name}");
            var loaded = SettingsSerializer<GraphSettings>.Load(e.Document);
            //To avoid having OLD instances of the settings still bound to the UI
            //Do not create new instance of the settings, but copy the loaded settings
            Settings.CopyFrom(loaded);
        }
        private void DocumentManager_DocumentToBeDeactivated(object sender, DocumentCollectionEventArgs e)
        {
            if (e.Document == null) return;
            //Utils.prtDbg($"{DateTime.Now} Document To Be Deactivated event: {e.Document.Name}");
            SettingsSerializer<GraphSettings>.Save(e.Document, Settings);
        }
        private void DocumentManager_DocumentToBeDestroyed(object sender, DocumentCollectionEventArgs e)
        {
            if (e.Document == null) return;
            //Utils.prtDbg($"Document To Be Destroyed event: {e.Document.Name}");            
            SettingsSerializer<GraphSettings>.Save(e.Document, Instance.Settings);
        }
    }
}
