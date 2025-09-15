using Mapsui;
using Mapsui.Layers;
using Mapsui.Providers;
using Mapsui.Styles;
using Mapsui.UI.Wpf;

using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

using DimensioneringV2.GraphFeatures;

namespace DimensioneringV2.UI.MapOverlay
{
    internal class ApplyDimOverlayManager
    {
        private readonly Map _map;

        private Layer? _angivLayer;
        private readonly HashSet<AnalysisFeature> _current = new(new RefComparer());

        public ApplyDimOverlayManager(Map map)
        {
            _map = map;
            EnsureLayerExists();
            BringToFront();
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

        private void EnsureLayerExists()
        {
            if (_angivLayer != null) return;
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

        public void SetFeatures(IEnumerable<AnalysisFeature> features)
        {
            EnsureLayerExists();
            var items = (features ?? Enumerable.Empty<AnalysisFeature>())
                .Where(f => f != null)
                .Distinct(new RefComparer())
                .ToList();

            // Guard: skip if unchanged
            if (_current.Count == items.Count && items.All(f => _current.Contains(f)))
                return;

            _current.Clear();
            foreach (var f in items) _current.Add(f);

            _angivLayer!.DataSource = new MemoryProvider(items.Cast<IFeature>());
            _map.RefreshData();
        }

        public void Clear()
        {
            EnsureLayerExists();
            if (_current.Count == 0) return;
            _current.Clear();
            _angivLayer!.DataSource = new MemoryProvider(Enumerable.Empty<IFeature>());
            _map.RefreshData();
        }

        public void BringToFront()
        {
            EnsureLayerExists();
            _map.Layers.Remove(_angivLayer!);
            _map.Layers.Add(_angivLayer!);
        }

        public void RemoveLayer()
        {
            if (_angivLayer == null) return;
            _map.Layers.Remove(_angivLayer);
            _angivLayer = null;
            _current.Clear();
        }

        private sealed class RefComparer : IEqualityComparer<AnalysisFeature>
        {
            public bool Equals(AnalysisFeature x, AnalysisFeature y) => ReferenceEquals(x, y);
            public int GetHashCode(AnalysisFeature obj) => RuntimeHelpers.GetHashCode(obj);
        }
    }
}


