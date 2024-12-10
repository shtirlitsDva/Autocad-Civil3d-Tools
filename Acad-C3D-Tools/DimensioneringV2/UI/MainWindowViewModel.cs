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
using DimensioneringV2;
using ProjNet.CoordinateSystems.Transformations;
using ProjNet.CoordinateSystems;
using Autodesk.AutoCAD.Geometry;
using IntersectUtilities.UtilsCommon;
using NorsynHydraulicCalc.Pipes;

namespace DimensioneringV2.UI
{
    internal partial class MainWindowViewModel : ObservableObject
    {
        public IEnumerable<MapPropertyWrapper> MapProperties => GetMapProperties(typeof(AnalysisFeature));
        [ObservableProperty]
        private MapPropertyWrapper selectedMapPropertyWrapper;
        [ObservableProperty]
        private bool isMapDropdownEnabled = false;
        [ObservableProperty]
        private bool isPriceCalcEnabled = false;

        public MainWindowViewModel()
        {
            _dataService = DataService.Instance;
            _dataService.DataLoaded += OnDataLoadedFirstTime;
            _dataService.CalculationDataReturned += OnCalculationsCompleted;
        }

        private IEnumerable<MapPropertyWrapper> GetMapProperties(Type type)
        {
            return type.GetProperties()
                .Where(prop => Attribute.IsDefined(prop, typeof(MapPropertyAttribute)))
                .Select(prop =>
                {
                    var attr = (MapPropertyAttribute)Attribute.GetCustomAttribute(prop, typeof(MapPropertyAttribute));
                    var description = attr.Property.GetDescription();
                    return new MapPropertyWrapper(attr.Property, description);
                });
        }

        partial void OnSelectedMapPropertyWrapperChanged(MapPropertyWrapper value)
        {
            if (value == null) return;
            _styleManager = new StyleManager(value.EnumValue);
            UpdateMap();
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
                await Task.Run(() => HydraulicCalculationsService.CalculateDijkstra(
                    new List<(Func<AnalysisFeature, dynamic> Getter, Action<AnalysisFeature, dynamic> Setter)>
                    {
                        (f => f.NumberOfBuildingsConnected, (f, v) => f.NumberOfBuildingsSupplied = v),
                        (f => f.NumberOfUnitsConnected, (f, v) => f.NumberOfUnitsSupplied = v),
                        (f => f.HeatingDemandConnected, (f, v) => f.HeatingDemandSupplied = v)
                    }
                    ));
            }
            catch (Exception ex)
            {
                utils.prdDbg($"An error occurred during calculations: {ex.Message}");
                utils.prdDbg(ex);
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

        public RelayCommand PerformPriceCalc =>
            new RelayCommand((_) => PriceCalc(), (_) => true);

        private void PriceCalc()
        {
            var afs = Features.Cast<AnalysisFeature>();
            //var stik = afs.Where(x => !x.PipeDim.Equals(default(Dim)) && x.NumberOfBuildingsConnected == 1);
            //var fls = afs.Where(x => !x.PipeDim.Equals(default(Dim)) && x.NumberOfBuildingsConnected == 0);
            // Calculate data for service lines (stik)
            var stikTable = afs
                .Where(x => !x.PipeDim.Equals(default(Dim)) && x.NumberOfBuildingsConnected == 1)
                .GroupBy(x => x.PipeDim.DimName)
                .Select(g => new
                {
                    DimName = g.Key,
                    TotalLength = g.Sum(x => x.Length),
                    Price = g.Sum(x => x.Length * x.PipeDim.Price_m),
                    ServiceCount = g.Count(),
                    ServicePrice = g.Count() * g.First().PipeDim.Price_stk(NorsynHydraulicCalc.SegmentType.Stikledning)
                })
                .ToList();

            var stikTotal = new
            {
                TotalPrice = stikTable.Sum(row => row.Price),
                TotalServicePrice = stikTable.Sum(row => row.ServicePrice)
            };

            // Calculate data for supply lines (fls)
            var flsTable = afs
                .Where(x => !x.PipeDim.Equals(default(Dim)) && x.NumberOfBuildingsConnected == 0)
                .GroupBy(x => x.PipeDim.DimName)
                .Select(g => new
                {
                    DimName = g.Key,
                    TotalLength = g.Sum(x => x.Length),
                    Price = g.Sum(x => x.Length * x.PipeDim.Price_m)
                })
                .ToList();

            var flsTotal = new
            {
                TotalPrice = flsTable.Sum(row => row.Price)
            };

            var grandTotal = stikTotal.TotalPrice + stikTotal.TotalServicePrice + flsTotal.TotalPrice;

            PriceSummaryWindow window;
            try
            {
                window = new PriceSummaryWindow(stikTable, flsTable, grandTotal);
                window.Show();
            }
            catch (Exception ex)
            {
                utils.prdDbg(ex);
            }
        }

        [ObservableProperty]
        private Map _mymap = new() { CRS = "EPSG:3857" };

        private StyleManager _styleManager;

        public ObservableCollection<IFeature> Features { get; private set; }

        private readonly DataService _dataService;

        private void OnDataLoadedFirstTime(object sender, EventArgs e)
        {
            // Update observable collections
            Features = new(_dataService!.Features.SelectMany(x => x));

            _styleManager = new StyleManager(MapPropertyEnum.Basic);
            CreateMapFirstTime();
        }

        private void OnCalculationsCompleted(object sender, EventArgs e)
        {
            Features = new(_dataService!.CalculatedFeatures.SelectMany(x => x));

            if (!IsMapDropdownEnabled)
            {
                IsMapDropdownEnabled = true;
                SelectedMapPropertyWrapper = null;
                SelectedMapPropertyWrapper = MapProperties.First();
            }

            if (!IsPriceCalcEnabled)
            {
                IsPriceCalcEnabled = true;
            }
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

        internal class MapPropertyWrapper
        {
            public MapPropertyEnum EnumValue { get; }
            public string Description { get; }
            public MapPropertyWrapper(MapPropertyEnum enumValue, string description)
            {
                EnumValue = enumValue;
                Description = description;
            }
        }
    }
}