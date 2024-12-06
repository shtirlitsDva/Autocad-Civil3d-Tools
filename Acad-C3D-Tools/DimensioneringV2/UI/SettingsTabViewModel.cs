using Autodesk.AutoCAD.ApplicationServices;
using AcAp = Autodesk.AutoCAD.ApplicationServices.Application;

using CommunityToolkit.Mvvm.ComponentModel;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.UI
{
    public partial class SettingsTabViewModel : ObservableObject
    {
        [ObservableProperty]
        private HydraulicSettings settings = new();

        public SettingsTabViewModel()
        {
            var dwgFileName = AcAp.DocumentManager.MdiActiveDocument.Database.Filename;

            Settings = HydraulicSettings.Load(
                AcAp.DocumentManager.MdiActiveDocument.Database);

            // Subscribe to DocumentManager events
            AcAp.DocumentManager.DocumentActivated += DocumentManager_DocumentActivated;
            AcAp.DocumentManager.DocumentToBeDeactivated += DocumentManager_DocumentToBeDeactivated;
            AcAp.DocumentManager.DocumentToBeDestroyed += DocumentManager_DocumentToBeDestroyed;
        }

        private void DocumentManager_DocumentActivated(object sender, DocumentCollectionEventArgs e)
        {
            Settings = HydraulicSettings.Load(e.Document.Database);
        }

        private void DocumentManager_DocumentToBeDeactivated(object sender, DocumentCollectionEventArgs e)
        {
            Settings.Save(e.Document.Database);
        }

        private void DocumentManager_DocumentToBeDestroyed(object sender, DocumentCollectionEventArgs e)
        {
            Settings.Save(e.Document.Database);
        }
    }
}
