using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using DimensioneringV2.Legend;
using DimensioneringV2.Services;
using DimensioneringV2.Themes;
using DimensioneringV2.UI.Infrastructure;
using DimensioneringV2.UI.MapProperty;

using Mapsui;
using Mapsui.Layers;
using Mapsui.Providers;

using System.Collections.Generic;
using System.Linq;

namespace DimensioneringV2.UI
{
    internal partial class MainWindowViewModel
    {
        #region Map property selection
        public IEnumerable<MapPropertyWrapper> MapProperties => GetMapProperties();

        [ObservableProperty]
        private MapPropertyWrapper selectedMapPropertyWrapper;

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
        #endregion

        #region Base map selection
        public IReadOnlyList<BaseMapOption> BaseMapOptions { get; } = new[]
        {
            new BaseMapOption(BaseMapType.OpenStreetMap, "OpenStreetMap"),
            new BaseMapOption(BaseMapType.Dark, "Dark"),
            new BaseMapOption(BaseMapType.Ortofoto, "Ortofoto"),
            new BaseMapOption(BaseMapType.Hybrid, "Hybrid"),
            new BaseMapOption(BaseMapType.Off, "Off"),
        };

        [ObservableProperty]
        private BaseMapOption _selectedBaseMap;

        partial void OnSelectedBaseMapChanged(BaseMapOption value)
        {
            if (value == null || Mymap == null) return;
            BaseMapLayerFactory.ApplyBaseMap(Mymap, value.Type);
            Mymap.RefreshData();
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

        #region Map lifecycle
        private void CreateMapFirstTime()
        {
            if (Mymap == null) return;

            MapSuiSvgIconCache.Initialize();

            selectedMapPropertyWrapper = new MapPropertyWrapper(MapPropertyEnum.Default, "Default");
            OnPropertyChanged(nameof(SelectedMapPropertyWrapper));

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

            BaseMapLayerFactory.ApplyBaseMap(Mymap, SelectedBaseMap.Type);

            Mymap.Layers.Add(layer);

            Mymap.Navigator.ZoomToBox(extent);

            var legend = _widgetManager.Get<LegendWidget>();
            legend.Content = _themeManager.GetLegendContent();
            legend.Enabled = IsLegendVisible;

            Mymap.RefreshData();
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

            var legend = _widgetManager.Get<LegendWidget>();
            legend.Content = _themeManager.GetLegendContent();
            legend.Enabled = IsLegendVisible;
            Mymap.RefreshData();
        }
        #endregion
    }
}
