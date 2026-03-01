using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Windows;

using CommunityToolkit.Mvvm.ComponentModel;

using DimensioneringV2.AutoCAD;
using DimensioneringV2.GraphFeatures;

using QuikGraph;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using EventManager;

using AcAp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace DimensioneringV2.Services
{
    internal partial class HydraulicSettingsService : ObservableObject
    {
        private static HydraulicSettingsService? _instance;
        public static HydraulicSettingsService Instance => _instance ??= new HydraulicSettingsService(Commands.Events!);
        internal static void Reset() => _instance = null;
        [ObservableProperty]
        private HydraulicSettings settings;
        private HydraulicSettingsService(AcadEventManager events)
        {
            settings = HydraulicSettingsSerializer.Load(
                AcAp.DocumentManager.MdiActiveDocument);

            events.DocumentActivated += DocumentManager_DocumentActivated;
            events.DocumentToBeDeactivated += DocumentManager_DocumentToBeDeactivated;
            events.DocumentToBeDestroyed += DocumentManager_DocumentToBeDestroyed;
            //Saving of settings when the palette is made invisible
            //must be handled by the PaletteSet as this service does not know
            //when the palette is instatiated

        }
        private void DocumentManager_DocumentActivated(object sender, DocumentCollectionEventArgs e)
        {
            if (e.Document == null) return;
            //Utils.prtDbg($"{DateTime.Now} Document activated event: {e.Document.Name}");
            var loaded = HydraulicSettingsSerializer.Load(e.Document);
            //To avoid having OLD instances of the settings still bound to the UI
            //Do not create new instance of the settings, but copy the loaded settings
            Settings.CopyFrom(loaded);
        }
        private void DocumentManager_DocumentToBeDeactivated(object sender, DocumentCollectionEventArgs e)
        {
            if (e.Document == null) return;
            //Utils.prtDbg($"{DateTime.Now} Document To Be Deactivated event: {e.Document.Name}");
            HydraulicSettingsSerializer.Save(e.Document, Settings);
        }
        private void DocumentManager_DocumentToBeDestroyed(object sender, DocumentCollectionEventArgs e)
        {
            if (e.Document == null) return;
            //Utils.prtDbg($"Document To Be Destroyed event: {e.Document.Name}");            
            HydraulicSettingsSerializer.Save(e.Document, Services.HydraulicSettingsService.Instance.Settings);
        }
    }
}
