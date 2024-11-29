using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Windows.Data;
using System.ComponentModel;
using AcAp = Autodesk.AutoCAD.ApplicationServices.Application;
using System.Windows;
using Autodesk.AutoCAD.DatabaseServices.Internal;

namespace DimensioneringV2.UI
{
    internal class MainWindowViewModel : ObservableObject
    {
        public RelayCommand CollectFeatures =>
            new RelayCommand((_) => CollectFeaturesExecute(), (_) => true);

        private async void CollectFeaturesExecute()
        {
            var docs = AcAp.DocumentManager;
            var ed = docs.MdiActiveDocument.Editor;

            //await docs.ExecuteInCommandContextAsync(
            //    async (obj) =>
            //    {
            //        await ed.CommandAsync();
            //    }
            //    );
            
        }
    }
}