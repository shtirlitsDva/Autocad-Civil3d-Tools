using DimensioneringV2.Legend;
using DimensioneringV2.Themes;

using DotSpatial.Projections.Transforms;

using Mapsui;
using Mapsui.Layers;
using Mapsui.Providers;
using Mapsui.Styles;

using System;
using System.Collections.Generic;
using System.Linq;

namespace DimensioneringV2.UI.MapOverlay
{
    /// <summary>
    /// Manages a single overlay layer for AngivDim interaction with three roles: Hover, Preview, Final.
    /// </summary>
    internal class AngivOverlayManager
    {
        private const string OverlayLayerName = "AngivOverlay";
        private const string RoleKey = "Role";
        private const string RoleHover = "Hover";
        private const string RolePreview = "Preview";
        private const string RoleFinal = "Final";

        private readonly Map _map;
        private readonly MemoryProvider _provider;
        private readonly Layer _layer;

        public AngivOverlayManager(Map map)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _provider = new MemoryProvider()
            {
                CRS = "EPSG:3857"
            };
            _layer = new Layer
            {
                Name = OverlayLayerName,
                DataSource = _provider,
                IsMapInfoLayer = false,
                Style = BuildCategoryStyle()
            };

            EnsureLayerOnTop();
        }

        private IStyle BuildCategoryStyle()
        {
            var dict = new Dictionary<string, IStyle>
            {
                [RoleHover] = new VectorStyle { Line = new Pen(Color.Cyan, 6) },
                [RolePreview] = new VectorStyle { Line = new Pen(Color.Orange, 6) },
                [RoleFinal] = new VectorStyle { Line = new Pen(Color.Magenta, 6) }
            };
            List<LegendItem> legendList = [
                new LegendItem() { Label = "Hover", SymbolColor = Color.Cyan, SymbolLineWidth = 4 },
                new LegendItem() { Label = "Preview", SymbolColor = Color.Cyan, SymbolLineWidth = 4 },
                new LegendItem() { Label = "Final", SymbolColor = Color.Cyan, SymbolLineWidth = 4 }];
            return new CategoryTheme<string>(
                f => (string)(f[RoleKey] ?? string.Empty),
                dict,
                legendList, "Overlay");
        }

        private void EnsureLayerOnTop()
        {
            var existing = _map.Layers.FirstOrDefault(l => l.Name == OverlayLayerName);
            if (existing != null) _map.Layers.Remove(existing);
            _map.Layers.Add(_layer);
        }

        public void ClearAll()
        {
            _provider.Clear();
            _map.RefreshData();
        }

        public void SetHover(IFeature? feature)
        {
            // remove old hover entries
            RemoveByRole(RoleHover);
            if (feature != null)
            {
                _provider.Add(CloneWithRole(feature, RoleHover));
            }
            _map.RefreshData();
        }

        public void SetPreview(IEnumerable<IFeature> features)
        {
            RemoveByRole(RolePreview);
            foreach (var f in features)
                _provider.Features.Add(CloneWithRole(f, RolePreview));
            _map.RefreshData();
        }

        public void SetFinal(IEnumerable<IFeature> features)
        {
            RemoveByRole(RoleFinal);
            foreach (var f in features)
                _provider.Features.Add(CloneWithRole(f, RoleFinal));
            _map.RefreshData();
        }

        private Mapsui.Features.Feature CloneWithRole(IFeature source, string role)
        {
            var clone = new Mapsui.Features.Feature
            {
                Geometry = source.Geometry
            };
            clone[RoleKey] = role;
            return clone;
        }

        private void RemoveByRole(string role)
        {
            var list = _provider.Features;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var f = list[i];
                if ((string)(f[RoleKey] ?? string.Empty) == role)
                    list.RemoveAt(i);
            }
        }
    }
}


