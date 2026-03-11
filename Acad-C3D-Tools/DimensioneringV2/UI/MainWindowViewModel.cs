using CommunityToolkit.Mvvm.ComponentModel;

using DimensioneringV2.GraphFeatures;
using DimensioneringV2.Services;
using DimensioneringV2.Themes;
using DimensioneringV2.UI.MapProperty;

using Mapsui;
using Mapsui.Providers;
using Mapsui.UI.Wpf;

using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace DimensioneringV2.UI
{
    internal partial class MainWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        private Map _mymap = new() { CRS = "EPSG:3857" };
        private MapControl _mapControl;

        internal void SetMapControl(MapControl mapControl)
        {
            _mapControl = mapControl;
            _widgetManager = new MapWidgetManager(Mymap, _mapControl);
            _widgetManager.Register(
                new Legend.LegendWidget { Enabled = false },
                new Legend.LegendWidgetSkiaRenderer());
            _widgetManager.Register(
                new Legend.StatusWidget { Enabled = false },
                new Legend.StatusWidgetSkiaRenderer());
        }

        private MapWidgetManager _widgetManager;
        private ThemeManager _themeManager;
        private MapPropertyWrapper _prevSelectedProperty;

        public ObservableCollection<IFeature> Features { get; private set; }

        private readonly HydraulicNetworkManager _manager;

        public MainWindowViewModel()
        {
            _selectedBaseMap = BaseMapOptions[0];

            _manager = HydraulicNetworkManager.Instance;
            _manager.NetworkLoaded += OnDataLoaded;
            _manager.CalculationsFinished += OnCalculationsCompleted;
            _manager.ActiveNetworkChanged += OnActiveNetworkChanged;

            BBRLayerService.Instance.BBRDataLoaded += OnBBRDataLoaded;

            //HydraulicSettingsService.Instance.Settings.PropertyChanged += OnSettingsPropertyChanged;
        }

        private void OnDataLoaded(object sender, EventArgs e)
        {
            Features = new(_manager.AllFeatures);

            _themeManager = new ThemeManager(_manager.AllFeatures);
            CreateMapFirstTime();
        }

        private void OnCalculationsCompleted(object sender, EventArgs e)
        {
            Features = new(_manager.AllFeatures);
            OnPropertyChanged(nameof(MapProperties));

            SelectedMapPropertyWrapper = null;
            SelectedMapPropertyWrapper = MapProperties.First();
        }

        private void OnActiveNetworkChanged(object? sender, EventArgs e)
        {
            OnPropertyChanged(nameof(MapProperties));
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
