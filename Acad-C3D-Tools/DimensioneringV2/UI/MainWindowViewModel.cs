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
using Mapsui.Extensions;
using Mapsui.Providers;
using Mapsui.Widgets;

using utils = IntersectUtilities.UtilsCommon.Utils;
using DimensioneringV2.MapStyles;
using ProjNet.CoordinateSystems.Transformations;
using ProjNet.CoordinateSystems;
using Autodesk.AutoCAD.Geometry;
using IntersectUtilities.UtilsCommon;

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

        #region ZoomToExtents
        public RelayCommand PerformZoomToExtents =>
            new RelayCommand((_) => ZoomToExtents(), (_) => true);

        private void ZoomToExtents()
        {
            var map = Mymap;
            if (map == null) return;

            var layer = map.Layers.FirstOrDefault(x => x.Name == "Features");
            if (layer == null) return;

            var extent = layer.Extent!.Grow(100);

            map.Navigator.ZoomToBox(extent);
        }
        #endregion

        #region SyncACWindow
        public RelayCommand SyncACWindowCommand =>
            new RelayCommand((_) => SyncACWindow(), (_) => true);
        private void SyncACWindow()
        {
            var vp = Mymap.Navigator.Viewport;
            var mapExtent = vp.ToExtent();
            var minX = mapExtent.MinX;
            var minY = mapExtent.MinY;
            var maxX = mapExtent.MaxX;
            var maxY = mapExtent.MaxY;

            var trans = new CoordinateTransformationFactory().CreateFromCoordinateSystems(
                ProjectedCoordinateSystem.WebMercator,
                ProjectedCoordinateSystem.WGS84_UTM(32, true));

            var minPT = trans.MathTransform.Transform(new double[] { minX, minY });
            var maxPT = trans.MathTransform.Transform(new double[] { maxX, maxY });

            var minPt = new Point3d(minPT[0], minPT[1], 0);
            var maxPt = new Point3d(maxPT[0], maxPT[1], 0);

            var docs = AcAp.DocumentManager;
            var ed = docs.MdiActiveDocument.Editor;

            ed.Zoom(new Autodesk.AutoCAD.DatabaseServices.Extents3d(minPt, maxPt));
        }
        #endregion

        #region Toggle Labels
        public RelayCommand PerformLabelToggle =>
            new RelayCommand((_) => ToggleLabelStyles(), (_) => true);
        private void ToggleLabelStyles()
        {
            _styleManager.Switch();
            UpdateMap();
        }
        #endregion

        [ObservableProperty]
        private Map _mymap = new() { CRS = "EPSG:3857" };

        private StyleManager _styleManager;

        public ObservableCollection<IFeature> Features { get; private set; }

        private readonly DataService _dataService;

        private void OnDataLoadedFirstTime(object sender, EventArgs e)
        {
            // Update observable collections
            Features = new(_dataService!.Features.SelectMany(x => x));

            _styleManager = new StyleManager(new StyleBasic(), new StyleBasic());
            CreateMapFirstTime();
        }

        private void OnCalculationsCompleted(object sender, EventArgs e)
        {
            Features = new(_dataService!.CalculatedFeatures.SelectMany(x => x));
            _styleManager = new StyleManager(
                new StyleNumberOfBuildingsSupplied_WithLabels(),
                new StyleNumberOfBuildingsSupplied_NoLabels());
            UpdateMap();
        }

        private void CreateMapFirstTime()
        {
            if (Mymap == null) return;

            _styleManager.CurrentStyle.ApplyStyle(Features);

            var provider = new MemoryProvider(Features)
            {
                CRS = "EPSG:3857"
            };

            Layer layer = new Layer
            {
                DataSource = provider,
                Name = "Features",
            };

            var extent = layer.Extent!.Grow(100);

            Mymap.Layers.Clear();
            Mymap.Layers.Add(Mapsui.Tiling.OpenStreetMap.CreateTileLayer());
            Mymap.Layers.Add(layer);

            Mymap.Navigator.ZoomToBox(extent);
        }

        private void UpdateMap()
        {
            if (Mymap == null) return;

            _styleManager.CurrentStyle.ApplyStyle(Features);

            var provider = new MemoryProvider(Features)
            {
                CRS = "EPSG:3857"
            };

            Layer layer = new Layer
            {
                DataSource = provider,
                Name = "Features",
            };

            var exLayer = Mymap.Layers.FirstOrDefault(x => x.Name == "Features");
            if (exLayer != null)
            {
                Mymap.Layers.Remove(exLayer);
            }

            Mymap.Layers.Add(layer);
        }
    }
}