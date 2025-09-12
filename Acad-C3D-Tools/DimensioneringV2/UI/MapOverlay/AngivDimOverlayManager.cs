using Mapsui;
using Mapsui.Layers;
using Mapsui.Providers;
using Mapsui.Styles;
using Mapsui.UI.Wpf;

using System.Collections.Generic;
using System.Linq;

using DimensioneringV2.GraphFeatures;

namespace DimensioneringV2.UI.MapOverlay
{
    internal class AngivDimOverlayManager
    {
        private readonly Map _map;

        private Layer _angivLayer;

        public AngivDimOverlayManager(Map map)
        {
            _map = map;

            _angivLayer = _map.Layers.FirstOrDefault(l => l.Name == "Angiv") as Layer;
            if (_angivLayer == null)
            {
                _angivLayer = new Layer
                {
                    Name = "Angiv",
                    DataSource = new MemoryProvider(Enumerable.Empty<IFeature>()),
                    Style = BuildStyle()
                };
                _map.Layers.Add(_angivLayer);
            }
        }        

        private static IStyle BuildStyle()
        {
            
            var line = new VectorStyle
            {
                Line = new Pen(Color.Cyan, 4f),
                Outline = new Pen(Color.Red, 0.5f),                
            };

            var sc = new StyleCollection();
            sc.Styles.Add(line);
            return sc;
        }

        public void SetFeatures(IEnumerable<AnalysisFeature> features)
        {
            var list = (features ?? Enumerable.Empty<AnalysisFeature>()).Cast<IFeature>();
            _angivLayer.DataSource = new MemoryProvider(list);
            _map.RefreshData();
        }

        public void Clear()
        {
            _angivLayer.DataSource = new MemoryProvider(Enumerable.Empty<IFeature>());
            _map.RefreshData();
        }
    }
}


