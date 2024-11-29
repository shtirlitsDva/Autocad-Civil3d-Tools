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
using DimensioneringV2.DataService;
using Mapsui.Layers;

namespace DimensioneringV2.UI
{
    internal class MainWindowViewModel : ObservableObject
    {
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

        // Mapsui MapControl property (binding in XAML)
        private MapControl _mapControl;
        public MapControl MapControl
        {
            get => _mapControl;
            set
            {
                _mapControl = value;
                OnPropertyChanged(nameof(MapControl));
            }
        }

        public ObservableCollection<FeatureNode> Features { get; private set; } = new();
        public ObservableCollection<UndirectedGraph<FeatureNode, Edge<FeatureNode>>> Graphs { get; private set; } = new();

        private readonly IDataService _dataService;

        public MainWindowViewModel(IDataService dataService)
        {
            _dataService = dataService;

            // Subscribe to DataService updates
            _dataService.DataUpdated += OnDataUpdated;
        }
        private void OnDataUpdated(object sender, EventArgs e)
        {
            // Update observable collections
            Features.Clear();
            foreach (var feature in _dataService.Features)
                Features.Add(feature);

            Graphs.Clear();
            foreach (var graph in _dataService.Graphs)
                Graphs.Add(graph);

            // Refresh the map
            UpdateMap();
        }

        private void UpdateMap()
        {
            if (MapControl == null)
                return;

            var mapLayer1 = new MemoryLayer
            {
                Features = Features
            };

            MapControl.Map.Layers.Clear();
            MapControl.Map.Layers.Add(mapLayer1);
        }
    }
}