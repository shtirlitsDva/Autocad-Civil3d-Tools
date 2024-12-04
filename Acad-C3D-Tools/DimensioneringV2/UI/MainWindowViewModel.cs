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
using Mapsui.Nts;
using System.Diagnostics.CodeAnalysis;
using Mapsui.Extensions;
using Mapsui.Providers;
using Mapsui.Widgets;

using utils = IntersectUtilities.UtilsCommon.Utils;
using DimensioneringV2.MapStyles;

namespace DimensioneringV2.UI
{
    internal partial class MainWindowViewModel : ObservableObject
    {
        public MainWindowViewModel()
        {
            _dataService = DataService.Instance;
            _dataService.DataLoaded += OnDataLoadedFirstTime;
            _dataService.CalculationDataReturned += OnCalculationsCompleted;
        }

        #region CollectFeaturesFromACCommand
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
        #endregion

        #region PerformCalculationsCommand
        public RelayCommand PerformCalculationsCommand =>
            new(async (_) => await PerformCalculationsExecuteAsync(), (_) => true);

        private async Task PerformCalculationsExecuteAsync()
        {
            try
            {
                await Task.Run(HydraulicCalculationsService.PerformCalculations);
            }
            catch (Exception ex)
            {
                utils.prdDbg($"An error occurred during calculations: {ex.Message}");
            }
        } 
        #endregion

        [ObservableProperty]
        private Map _map = new() { CRS = "EPSG:3857" };

        [ObservableProperty]
        private IStyleManager currentStyle;

        public ObservableCollection<IFeature> Features { get; private set; }

        private readonly DataService _dataService;
        
        private void OnDataLoadedFirstTime(object sender, EventArgs e)
        {
            // Update observable collections
            Features = new(_dataService!.Features.SelectMany(x => x));

            CurrentStyle = new StyleBasic(Features);
            UpdateMap();
        }

        private void OnCalculationsCompleted(object sender, EventArgs e)
        {
            Features = new(_dataService!.CalculatedFeatures.SelectMany(x => x));
            CurrentStyle = new StyleCalculatedNumberOfBuildingsSupplied(Features);
            UpdateMap();
        }

        private void UpdateMap()
        {
            if (Map == null) return;

            var provider = new MemoryProvider(CurrentStyle.ApplyStyle())
            {
                CRS  = "EPSG:3857"
            };

            Layer layer = new Layer
            {
                DataSource = provider,
                Name = "Features",
            };

            var extent = layer.Extent!.Grow(100);

            Map.Layers.Clear();
            Map.Layers.Add(Mapsui.Tiling.OpenStreetMap.CreateTileLayer());
            Map.Layers.Add(layer);

            Map.Navigator.ZoomToBox(extent);
        }
    }
}