using DimensioneringV2.Services;
using DimensioneringV2.Themes;

using IntersectUtilities.UtilsCommon;

using Mapsui;
using Mapsui.Layers;
using Mapsui.Providers;

using System;
using System.Linq;

namespace DimensioneringV2.UI
{
    internal partial class MainWindowViewModel
    {
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
            if (Mymap == null) return;

            var bbrService = BBRLayerService.Instance;

            var activeLayer = Mymap.Layers.FirstOrDefault(x => x.Name == "BBR_Active");
            var inactiveLayer = Mymap.Layers.FirstOrDefault(x => x.Name == "BBR_Inactive");

            if (activeLayer != null) Mymap.Layers.Remove(activeLayer);
            if (inactiveLayer != null) Mymap.Layers.Remove(inactiveLayer);

            if (!bbrService.ActiveFeatures.Any() && !bbrService.InactiveFeatures.Any())
                return;

            try
            {
                var activeFeatures = bbrService.ActiveFeatures.ToList();
                var inactiveFeatures = bbrService.InactiveFeatures.ToList();

                if (activeFeatures.Count == 0 && inactiveFeatures.Count == 0)
                    return;

                Func<double> getResolution = () => Mymap.Navigator.Viewport.Resolution;

                var inactiveProvider = new MemoryProvider(inactiveFeatures.Cast<IFeature>())
                {
                    CRS = "EPSG:3857"
                };
                var newInactiveLayer = new Layer
                {
                    DataSource = inactiveProvider,
                    Name = "BBR_Inactive",
                    Style = new BBRTheme(isActive: false, getResolution)
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
                    Style = new BBRTheme(isActive: true, getResolution)
                };

                Mymap.Layers.Add(newInactiveLayer);
                Mymap.Layers.Add(newActiveLayer);
            }
            catch (Exception ex)
            {
                Utils.prtDbg($"RefreshBBRLayers ERROR: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}
