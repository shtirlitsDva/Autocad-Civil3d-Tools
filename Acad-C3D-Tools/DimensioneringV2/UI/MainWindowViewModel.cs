using Autodesk.AutoCAD.Geometry;

using BruTile.Cache;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using DimensioneringV2.AutoCAD;
using DimensioneringV2.GraphFeatures;
using DimensioneringV2.Legend;
using DimensioneringV2.MapCommands;
using DimensioneringV2.Services;
using DimensioneringV2.Services.SubGraphs;
using DimensioneringV2.Themes;
using DimensioneringV2.UI.ApplyDim;
using DimensioneringV2.UI.MapOverlay;

using IntersectUtilities.UtilsCommon;

using Mapsui;
using Mapsui.Extensions;
using Mapsui.Layers;
using Mapsui.Providers;
using Mapsui.UI.Wpf;

using NorsynHydraulicCalc;

using ProjNet.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

using AcAp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace DimensioneringV2.UI
{
    internal partial class MainWindowViewModel : ObservableObject
    {
        public IEnumerable<MapPropertyWrapper> MapProperties => GetMapProperties();
        [ObservableProperty]
        private MapPropertyWrapper selectedMapPropertyWrapper;

        public MainWindowViewModel()
        {
            _dataService = DataService.Instance;
            _dataService.DataLoaded += OnDataLoadedFirstTime;
            _dataService.CalculationsFinishedEvent += OnCalculationsCompleted;

            BBRLayerService.Instance.BBRDataLoaded += OnBBRDataLoaded;

            //HydraulicSettingsService.Instance.Settings.PropertyChanged += OnSettingsPropertyChanged;
        }

        private void OnBBRDataLoaded(object? sender, EventArgs e)
        {
            RefreshBBRLayers();
        }

        private void OnSettingsPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName != null && e.PropertyName.StartsWith("Filter"))
            {
                if (BBRLayerService.Instance.ActiveFeatures.Any() || BBRLayerService.Instance.InactiveFeatures.Any())
                {
                    BBRLayerService.Instance.RefreshFiltering();
                }
            }
        }

        private void RefreshBBRLayers()
        {
            Utils.prtDbg("RefreshBBRLayers called");
            if (Mymap == null)
            {
                Utils.prtDbg("RefreshBBRLayers: Mymap is null, returning");
                return;
            }

            var bbrService = BBRLayerService.Instance;
            Utils.prtDbg($"RefreshBBRLayers: Active={bbrService.ActiveFeatures.Count()}, Inactive={bbrService.InactiveFeatures.Count()}");

            var activeLayer = Mymap.Layers.FirstOrDefault(x => x.Name == "BBR_Active");
            var inactiveLayer = Mymap.Layers.FirstOrDefault(x => x.Name == "BBR_Inactive");

            if (activeLayer != null) Mymap.Layers.Remove(activeLayer);
            if (inactiveLayer != null) Mymap.Layers.Remove(inactiveLayer);

            if (!bbrService.ActiveFeatures.Any() && !bbrService.InactiveFeatures.Any())
            {
                Utils.prtDbg("RefreshBBRLayers: No features, returning");
                return;
            }

            try
            {
                var activeFeatures = bbrService.ActiveFeatures.ToList();
                var inactiveFeatures = bbrService.InactiveFeatures.ToList();

                Utils.prtDbg($"RefreshBBRLayers: Materialized - Active={activeFeatures.Count}, Inactive={inactiveFeatures.Count}");

                if (activeFeatures.Count > 0)
                {
                    var first = activeFeatures[0];
                    var geom = first.Geometry;
                    Utils.prtDbg($"RefreshBBRLayers: First feature geometry: {geom?.GetType().Name ?? "null"}, Coords: {geom?.Coordinate?.X ?? 0}, {geom?.Coordinate?.Y ?? 0}");
                    Utils.prtDbg($"RefreshBBRLayers: Original coords: {first.OriginalX}, {first.OriginalY}");
                }

                if (activeFeatures.Count == 0 && inactiveFeatures.Count == 0)
                {
                    Utils.prtDbg("RefreshBBRLayers: No features after materialization");
                    return;
                }

                var inactiveProvider = new MemoryProvider(inactiveFeatures.Cast<IFeature>())
                {
                    CRS = "EPSG:3857"
                };
                Func<double> getResolution = () => Mymap.Navigator.Viewport.Resolution;

                var newInactiveLayer = new Layer
                {
                    DataSource = inactiveProvider,
                    Name = "BBR_Inactive",
                    Style = new Themes.BBRTheme(isActive: false, getResolution)
                };

                var activeProvider = new MemoryProvider(activeFeatures.Cast<IFeature>())
                {
                    CRS = "EPSG:3857"
                };
                var newActiveLayer = new Layer
                {
                    DataSource = activeProvider,
                    Name = "BBR_Active",
                    IsMapInfoLayer = false,
                    Style = new Themes.BBRTheme(isActive: true, getResolution)
                };

                Mymap.Layers.Add(newInactiveLayer);
                Mymap.Layers.Add(newActiveLayer);

                Utils.prtDbg($"RefreshBBRLayers: Done, layer count now: {Mymap.Layers.Count}");
            }
            catch (System.Exception ex)
            {
                Utils.prtDbg($"RefreshBBRLayers ERROR: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private IEnumerable<MapPropertyWrapper> GetMapProperties()
        {
            return MapPropertyMetadata.Themed
                .Select(m => new MapPropertyWrapper(m.Enum, m.Description));
        }

        partial void OnSelectedMapPropertyWrapperChanged(MapPropertyWrapper value)
        {
            if (value == null) return;
            UpdateMap();
        }

        #region MapCommands
        public RelayCommand CollectFeaturesCommand => new RelayCommand(CollectFeatures.Execute);
        public RelayCommand LoadElevationsCommand => new RelayCommand(LoadElevations.Execute);
        public RelayCommand PerformCalculationsSPDCommand => new(async () => await new CalculateSPD().Execute());
        public RelayCommand PerformCalculationsBFCommand => new(async () => await new CalculateBF().Execute());
        public AsyncRelayCommand PerformCalculationsGAOptimizedCommand => new(new CalculateGA().Execute);
        public RelayCommand PerformPriceCalc => new RelayCommand(() => new CalculatePrice().Execute(Features));
        public RelayCommand ShowForbrugereCommand => new RelayCommand(() => new ShowForbrugere().Execute(Features));
        public RelayCommand Dim2ImportDimsCommand => new RelayCommand(() => new Dim2ImportDims().Execute());
        public RelayCommand SaveResultCommand => new RelayCommand(() => new SaveResult().Execute());
        public RelayCommand LoadResultCommand => new RelayCommand(() => new LoadResult().Execute());
        public RelayCommand WriteToDwgCommand => new RelayCommand(() => new MapCommands.Write2Dwg().Execute());
        public RelayCommand WriteStikOgVejklasserCommand => new RelayCommand(() => new WriteStikOgVejklasser().Execute());
        public AsyncRelayCommand TestElevationsCommand => new AsyncRelayCommand(new TestElevations().Execute);
        public AsyncRelayCommand SampleGridCommand => new AsyncRelayCommand(new SampleGrid().Execute);
        public AsyncRelayCommand TestCacheCommand => new AsyncRelayCommand(new TestCache().Execute);
        public RelayCommand OpenBbrDataCommand => new RelayCommand(() =>
        {
            var window = new BBRData.Views.BbrDataWindow();
            window.Show();
        });
        #endregion

        #region ZoomToExtents
        public RelayCommand PerformZoomToExtents => new RelayCommand(ZoomToExtents, () => true);
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
        public RelayCommand SyncACWindowCommand => new RelayCommand(SyncACWindow, () => true);
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

            var minPT = trans.MathTransform.Transform([minX, minY]);
            var maxPT = trans.MathTransform.Transform([maxX, maxY]);

            var minPt = new Point3d(minPT[0], minPT[1], 0);
            var maxPt = new Point3d(maxPT[0], maxPT[1], 0);

            var docs = AcAp.DocumentManager;
            var ed = docs.MdiActiveDocument.Editor;

            ed.Zoom(new Autodesk.AutoCAD.DatabaseServices.Extents3d(minPt, maxPt));
        }
        #endregion

        #region Toggle Labels
        private bool _showLabels = false;
        public RelayCommand PerformLabelToggle =>
            new RelayCommand(ToggleLabelStyles, () => true);
        private void ToggleLabelStyles()
        {
            _showLabels = !_showLabels;
            UpdateMap();
        }
        #endregion        

        [ObservableProperty]
        private Map _mymap = new() { CRS = "EPSG:3857" };
        private MapControl _mapControl;
        internal void SetMapControl(MapControl mapControl)
        {
            _mapControl = mapControl;
        }

        private ThemeManager _themeManager;
        private MapPropertyWrapper _prevSelectedProperty;

        public ObservableCollection<IFeature> Features { get; private set; }

        private readonly DataService _dataService;

        private void OnDataLoadedFirstTime(object sender, EventArgs e)
        {
            // Update observable collections
            Features = new(_dataService!.Features.SelectMany(x => x));

            _themeManager = new ThemeManager(_dataService.Features.SelectMany(x => x));
            CreateMapFirstTime();
        }

        private void OnCalculationsCompleted(object sender, EventArgs e)
        {
            Features = new(_dataService!.Features.SelectMany(x => x));

            SelectedMapPropertyWrapper = null;
            SelectedMapPropertyWrapper = MapProperties.First();
        }

        private static IPersistentCache<byte[]>? _defaultCache;
        private static BruTile.Attribution _stadiaAttribution = new("© Stadia Maps", "https://stadiamaps.com/");
        private void CreateMapFirstTime()
        {
            if (Mymap == null) return;

            MapSuiSvgIconCache.Initialize();

            SelectedMapPropertyWrapper = new MapPropertyWrapper(MapPropertyEnum.Default, "Default");
            _themeManager.SetTheme(MapPropertyEnum.Default);

            var provider = new MemoryProvider(Features)
            {
                CRS = "EPSG:3857"
            };

            Layer layer = new Layer
            {
                DataSource = provider,
                Name = "Features",
                IsMapInfoLayer = true,
                Style = _themeManager.CurrentTheme
            };

            var extent = layer.Extent!.Grow(100);

            Mymap.Layers.Clear();

            //OSM map
            Mymap.Layers.Add(Mapsui.Tiling.OpenStreetMap.CreateTileLayer());

            #region Attempt to add Stadia map tiles -> success
            ////Stadia maps tiles
            //string userAgent =
            //    $"user-agent-of-{Path.GetFileNameWithoutExtension(
            //        System.AppDomain.CurrentDomain.FriendlyName)}";

            //var httpTileSource = new HttpTileSource(
            //    new GlobalSphericalMercator(),
            //    "https://tiles.stadiamaps.com/tiles/alidade_smooth_dark/{z}/{x}/{y}@2x.png",
            //    ["a", "b", "c"],
            //    name: "Stadia Maps",
            //    attribution: _stadiaAttribution,
            //    //configureHttpRequestMessage: (r) => r.Headers.TryAddWithoutValidation("User-Agent", userAgent),
            //    persistentCache: _defaultCache
            //    );

            //httpTileSource.AddHeader("User-Agent", userAgent);
            //httpTileSource.AddHeader("Stadia-Auth", "enter api key here");

            //var stadiaLayer = new TileLayer(httpTileSource) { Name = "Stadia" };

            //// Add the custom tile layer to the map
            //Mymap.Layers.Add(stadiaLayer);
            #endregion

            //Add the features layer
            Mymap.Layers.Add(layer);

            //RefreshBBRLayers();

            Mymap.Navigator.ZoomToBox(extent);

            //Legends
            _legendWidget = new LegendWidget()
            {
                Content = _themeManager.GetLegendContent(),
                Enabled = IsLegendVisible
            };
            Mymap.Widgets.Enqueue(_legendWidget);
            _mapControl.Renderer.WidgetRenders[typeof(LegendWidget)] = new LegendWidgetSkiaRenderer();
        }
        private void UpdateMap()
        {
            if (Mymap == null) return;

            _themeManager.SetTheme(
                SelectedMapPropertyWrapper.EnumValue, _showLabels);

            var provider = new MemoryProvider(Features)
            {
                CRS = "EPSG:3857"
            };

            Layer layer = new Layer
            {
                DataSource = provider,
                Name = "Features",
                IsMapInfoLayer = true,
                Style = _themeManager.CurrentTheme
            };

            var exLayer = Mymap.Layers.FirstOrDefault(x => x.Name == "Features");
            if (exLayer != null)
            {
                Mymap.Layers.Remove(exLayer);
            }

            Mymap.Layers.Add(layer);

            //RefreshBBRLayers();

            if (_legendWidget != null)
            {
                _legendWidget.Content = _themeManager.GetLegendContent();
                Mymap.RefreshData();
            }
        }

        #region Popup setup
        private FeaturePopupWindow? _popupWindow;
        private readonly FeaturePopupViewModel _popupVm = new();

        public void OnMapInfo(object? sender, MapInfoEventArgs e)
        {
            if (_angivDim != null || _resetDim != null)
            {
                _popupWindow?.HidePopup();
                return;
            }

            if (e.MapInfo?.Feature is not AnalysisFeature feature)
            {
                _popupWindow?.HidePopup();
                return;
            }

            _popupVm.Update(feature);

            if (_popupWindow == null)
            {
                _popupWindow = new FeaturePopupWindow();
            }

            _popupWindow.ShowPopup(_popupVm);
        }
        #endregion

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

        #region Legend
        [ObservableProperty]
        private bool isLegendVisible;
        private LegendWidget? _legendWidget;
        partial void OnIsLegendVisibleChanged(bool value)
        {
            UpdateLegendVisibility();
        }
        private void UpdateLegendVisibility()
        {
            if (_legendWidget == null) return;
            _legendWidget.Enabled = IsLegendVisible;
            _mymap.RefreshData();
        }
        #endregion

        #region Angiv dim command
        public RelayCommand AngivDimCommand => new(AngivDim);
        private ApplyDimManager? _angivDim;
        private ApplyDimOverlayManager _overlayManager;
        private NorsynHydraulicCalc.Pipes.PipeTypes? _pipes;
        private void AngivDim()
        {
            if (_angivDim != null)
            {
                // Toggle off if already active
                _angivDim.Stop();
                _angivDim = null;
                SelectedMapPropertyWrapper = _prevSelectedProperty;
                UpdateMap();
                return;
            }

            if (_dataService?.Graphs == null || !_dataService.Graphs.Any()) return;

            _pipes = new NorsynHydraulicCalc.Pipes.PipeTypes(HydraulicSettingsService.Instance.Settings);

            _prevSelectedProperty = SelectedMapPropertyWrapper;
            SelectedMapPropertyWrapper = new MapPropertyWrapper(MapPropertyEnum.Pipe, "Rørdimension");
            UpdateMap();

            _overlayManager ??= new ApplyDimOverlayManager(Mymap);
            _angivDim = new ApplyDimManager(_mapControl, _dataService.Graphs, _overlayManager);
            _angivDim.PathFinalized += OnAngivPathFinalized;
            _angivDim.Stopped += OnAngivDimStopped;
            _angivDim.Start();
        }

        #region AngivDim handlers
        private void OnAngivDimStopped()
        {
            _pipes = null;
            _angivDim = null;
            SelectedMapPropertyWrapper = _prevSelectedProperty;

            var graphs = DataService.Instance.Graphs;
            HydraulicCalc hc = new HydraulicCalc(
                HydraulicSettingsService.Instance.Settings,
                new LoggerFile());

            foreach (var ograph in graphs)
            {
                var graph = ograph.CopyToBF();

                foreach (var edge in graph.Edges)
                {
                    switch (edge.SegmentType)
                    {
                        case SegmentType.Fordelingsledning:
                            var res1 = hc.CalculateDistributionSegment(edge);
                            edge.ApplyResult(res1);
                            break;
                        case SegmentType.Stikledning:
                            var res2 = hc.CalculateClientSegment(edge);
                            edge.ApplyResult(res2);
                            break;
                        default:
                            break;
                    }
                }
            }

            foreach (var graph in DataService.Instance.Graphs)
                PressureAnalysisService.CalculateDifferentialLossAtClient(graph);

            UpdateMap();
        }
        private void OnAngivPathFinalized(IEnumerable<AnalysisFeature> features)
        {
            var dlg = new DimensioneringV2.UI.Dialogs.SelectPipeDimDialog();
            dlg.Owner = System.Windows.Application.Current?.MainWindow;
            var r = dlg.ShowDialog();

            if (dlg.ResultReason == DimensioneringV2.UI.Dialogs.SelectPipeDimViewModel.CloseReason.Retry)
            {
                _angivDim?.RetryKeepFirst();
                return;
            }
            if (dlg.ResultReason != DimensioneringV2.UI.Dialogs.SelectPipeDimViewModel.CloseReason.Ok)
            {
                // Cancel: exit mode and restore theme
                _angivDim?.Stop();
                _angivDim = null;
                SelectedMapPropertyWrapper = _prevSelectedProperty;
                UpdateMap();
                return;
            }

            // Apply selected dim to features
            var settings = Services.HydraulicSettingsService.Instance.Settings;
            var types = new NorsynHydraulicCalc.Pipes.PipeTypes(settings);
            var selectedType = dlg.ViewModel.SelectedPipeType;
            var selectedNominal = dlg.ViewModel.SelectedNominal;

            if (_pipes == null)
                _pipes = new NorsynHydraulicCalc.Pipes.PipeTypes(settings);
            var dim = _pipes.GetPipeType(selectedType).GetDim(selectedNominal);

            foreach (var f in features)
            {
                if (f.ManualDim)
                {//Case: ManualDim already set
                    f.Dim = dim;
                }
                else
                {//Case: first time setting manual dim
                    f.PreviousDim = f.Dim;
                    f.Dim = dim;
                    f.ManualDim = true;
                }
            }

            // Refresh theme and reset to first selection
            _themeManager.SetTheme(MapPropertyEnum.Pipe);
            UpdateMap();
            _angivDim?.ResetToFirstSelection();
        }
        #endregion
        #endregion

        #region Reset dim command
        public RelayCommand ResetDimCommand => new(ResetDim);
        private ResetDimManager? _resetDim;
        private void ResetDim()
        {
            if (_resetDim != null)
            {
                // Toggle off if already active
                _resetDim.Stop();
                _resetDim = null;
                SelectedMapPropertyWrapper = _prevSelectedProperty;
                UpdateMap();
                return;
            }

            if (_dataService?.Graphs == null ||
                !_dataService.Graphs.Any() ||
                !_dataService.Graphs.SelectMany(x => x.Edges).Any(x => x.PipeSegment.ManualDim)) return;

            _prevSelectedProperty = SelectedMapPropertyWrapper;
            SelectedMapPropertyWrapper = new MapPropertyWrapper(MapPropertyEnum.ManualDim, "Manuel dimension");
            UpdateMap();

            _resetDim = new ResetDimManager(_mapControl);
            _resetDim.Finalized += OnResetDimFinalized;
            _resetDim.Stopped += OnResetDimStopped;
            _resetDim.Start();
        }

        private void OnResetDimStopped()
        {
            _resetDim = null;
            SelectedMapPropertyWrapper = _prevSelectedProperty;

            var graphs = DataService.Instance.Graphs;
            HydraulicCalc hc = new HydraulicCalc(
                HydraulicSettingsService.Instance.Settings,
                new LoggerFile());

            foreach (var ograph in graphs)
            {
                var graph = ograph.CopyToBF();

                foreach (var edge in graph.Edges)
                {
                    switch (edge.SegmentType)
                    {
                        case SegmentType.Fordelingsledning:
                            var res1 = hc.CalculateDistributionSegment(edge);
                            edge.ApplyResult(res1);
                            break;
                        case SegmentType.Stikledning:
                            var res2 = hc.CalculateClientSegment(edge);
                            edge.ApplyResult(res2);
                            break;
                        default:
                            break;
                    }
                }
            }

            foreach (var graph in DataService.Instance.Graphs)
                PressureAnalysisService.CalculateDifferentialLossAtClient(graph);

            UpdateMap();
        }
        private void OnResetDimFinalized(AnalysisFeature feature)
        {
            if (!feature.ManualDim) return;
            feature.ManualDim = false;
            feature.Dim = feature.PreviousDim;
            UpdateMap();

            //Handle none manual dims left
            if (!_dataService.Graphs.SelectMany(x => x.Edges).Any(x => x.PipeSegment.ManualDim))
            {
                _resetDim?.Stop();
            }
        }
        #endregion
    }
}