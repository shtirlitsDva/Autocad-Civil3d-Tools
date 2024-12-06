using Autodesk.AutoCAD.ApplicationServices;

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
    internal class HydraulicSettingsService
    {
        private static HydraulicSettingsService? _instance;
        public static HydraulicSettingsService Instance => _instance ??= new HydraulicSettingsService();
        public HydraulicSettings Settings => _settings;
        private HydraulicSettings _settings;
        private HydraulicSettingsService() 
        {
            _settings = HydraulicSettings.Load(
                AcAp.DocumentManager.MdiActiveDocument.Database);

            // Subscribe to DocumentManager events
            AcAp.DocumentManager.DocumentActivated += DocumentManager_DocumentActivated;
            AcAp.DocumentManager.DocumentToBeDeactivated += DocumentManager_DocumentToBeDeactivated;
            AcAp.DocumentManager.DocumentToBeDestroyed += DocumentManager_DocumentToBeDestroyed;
        }

        private void DocumentManager_DocumentActivated(object sender, DocumentCollectionEventArgs e)
        {
            _settings = HydraulicSettings.Load(e.Document.Database);
        }

        private void DocumentManager_DocumentToBeDeactivated(object sender, DocumentCollectionEventArgs e)
        {
            _settings.Save(e.Document.Database);
        }

        private void DocumentManager_DocumentToBeDestroyed(object sender, DocumentCollectionEventArgs e)
        {
            _settings.Save(e.Document.Database);
        }
    }
}
