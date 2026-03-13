using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using DimensioneringV2.Legend;
using DimensioneringV2.Models;
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
        private static readonly HashSet<MapPropertyEnum> _nascentProperties = new()
        {
            MapPropertyEnum.Default, MapPropertyEnum.Basic, MapPropertyEnum.Bygninger
        };

        #region Map property selection
        public IEnumerable<MapPropertyWrapper> MapProperties => GetMapProperties();

        [ObservableProperty]
        private MapPropertyWrapper selectedMapPropertyWrapper;

        private IEnumerable<MapPropertyWrapper> GetMapProperties()
        {
            var state = _manager.CurrentState;
            if (state == HnState.Empty) return Enumerable.Empty<MapPropertyWrapper>();

            var source = MapPropertyMetadata.Themed;
            if (state == HnState.Nascent || state == HnState.Calculating)
                source = source.Where(m => _nascentProperties.Contains(m.Enum));

            return source.Select(m => new MapPropertyWrapper(m.Enum, m.Description));
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
            new BaseMapOption(BaseMapType.Skaermkort, "Skærmkort"),
            new BaseMapOption(BaseMapType.SkaermkortDaempet, "Skærmkort dæmpet"),
            new BaseMapOption(BaseMapType.SkaermkortDark, "Skærmkort dark"),
            new BaseMapOption(BaseMapType.Ortofoto, "Ortofoto"),
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
            if (Mymap == null || _themeManager == null) return;

            try
            {
                MapSuiSvgIconCache.Initialize();

                selectedMapPropertyWrapper = new MapPropertyWrapper(MapPropertyEnum.Default, "Default");
                OnPropertyChanged(nameof(SelectedMapPropertyWrapper));

                _themeManager.SetTheme(MapPropertyEnum.Default);

                var provider = new MemoryProvider(Features)
                {
                    CRS = "EPSG:25832"
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
            catch (System.Exception ex)
            {
                Utils.prtDbg($"CreateMapFirstTime ERROR: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void UpdateMap()
        {
            if (Mymap == null || _themeManager == null) return;

            try
            {
                _themeManager.SetTheme(
                    SelectedMapPropertyWrapper.EnumValue, _showLabels);

                var provider = new MemoryProvider(Features)
                {
                    CRS = "EPSG:25832"
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
            catch (System.Exception ex)
            {
                Utils.prtDbg($"UpdateMap ERROR: {ex.Message}\n{ex.StackTrace}");
            }
        }
        #endregion
    }
}
