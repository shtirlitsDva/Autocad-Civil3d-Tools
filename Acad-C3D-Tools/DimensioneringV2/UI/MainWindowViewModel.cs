using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Windows.Data;
using System.ComponentModel;
using AcAp = Autodesk.AutoCAD.ApplicationServices.Application;
using Mapsui.UI.Wpf;
using System.Collections.ObjectModel;
using DimensioneringV2.GraphFeatures;
using QuikGraph;
using DimensioneringV2.Services;
using Mapsui.Layers;
using Mapsui;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DimensioneringV2.UI
{
    internal partial class MainWindowViewModel : ObservableObject
    {
        public MainWindowViewModel()
        {

        }

        public RelayCommand CollectFeaturesCommand =>
            new RelayCommand((_) => CollectFeaturesExecute(), (_) => true);

        private async void CollectFeaturesExecute()
        {
            var docs = AcAp.DocumentManager;
            var ed = docs.MdiActiveDocument.Editor;

            await docs.ExecuteInCommandContextAsync(
                async (obj) =>
                {
                    await ed.CommandAsync("DIM2MAPCOLLECTFEATURES");
                }, null
                );
        }

        [ObservableProperty]
        private Map _map = new() { CRS = "EPSG:3857" };

        private readonly ProjectionService _projectionService

        public ObservableCollection<FeatureNode> Features { get; private set; } = new();

        private IDataService? _dataService;

        public void SetDataService(IDataService dataService)
        {
            _dataService = dataService;
            _dataService.DataUpdated += OnDataUpdated;
        }
        private void OnDataUpdated(object sender, EventArgs e)
        {
            // Update observable collections
            Features.Clear();
            foreach (var feature in _dataService.Features)
                Features.Add(feature);

            //Refresh the map
            UpdateMap();
        }

        private void UpdateMap()
        {
            if (Map == null) return;

            var mapLayer1 = new MemoryLayer
            {
                Features = Features
            };

            Map.Layers.Clear();
            Map.Layers.Add(Mapsui.Tiling.OpenStreetMap.CreateTileLayer());
            Map.Layers.Add(mapLayer1);
        }
    }
}