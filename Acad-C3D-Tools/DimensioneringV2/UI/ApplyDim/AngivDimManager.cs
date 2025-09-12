using CommunityToolkit.Mvvm.Input;

using DimensioneringV2.GraphFeatures;
using DimensioneringV2.Services;
using DimensioneringV2.Themes;
using DimensioneringV2.UI.MapOverlay;

using Mapsui;
using Mapsui.Layers;
using Mapsui.Providers;
using Mapsui.UI;
using Mapsui.UI.Wpf;

using QuikGraph;
using QuikGraph.Algorithms;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using System.Windows.Threading;

namespace DimensioneringV2.UI.ApplyDim
{
    internal class AngivDimManager : IDisposable
    {
        private readonly MapControl _mapControl;
        private readonly IEnumerable<UndirectedGraph<NodeJunction, EdgePipeSegment>> _graphs;
        private readonly AngivDimOverlayManager _overlay;
        private AngivGraphManager _lineGraphManager;

        private readonly DispatcherTimer _hoverTimer;
        private MPoint _lastMousePos = new MPoint(0, 0);
        private bool _isActive;

        private AnalysisFeature? _startFeature;
        private UndirectedGraph<NodeJunction, EdgePipeSegment>? _activeGraph;

        private readonly Dictionary<EdgePipeSegment, (NodeJunction a, NodeJunction b)> _edgeEndpoints = new();
        private readonly Dictionary<AnalysisFeature, UndirectedGraph<NodeJunction, EdgePipeSegment>> _featureToGraph = new();

        public event Action<IEnumerable<AnalysisFeature>>? PathFinalized;

        public AngivDimManager(
            MapControl mapControl,
            IEnumerable<UndirectedGraph<NodeJunction, EdgePipeSegment>> graphs,
            AngivDimOverlayManager overlay)
        {
            _mapControl = mapControl;
            _graphs = graphs;
            _overlay = overlay;

            _hoverTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(200), DispatcherPriority.Normal, OnHoverTimerTick, Dispatcher.CurrentDispatcher);
            _hoverTimer.Stop();

            BuildMembershipLookups();
            _lineGraphManager = new AngivGraphManager(_graphs);
        }

        private void BuildMembershipLookups()
        {
            _featureToGraph.Clear();
            _edgeEndpoints.Clear();

            foreach (var g in _graphs)
            {
                foreach (var e in g.Edges)
                {
                    _featureToGraph[e.PipeSegment] = g;
                    _edgeEndpoints[e] = (e.Source, e.Target);
                }
            }
        }

        public void Start()
        {
            if (_isActive) return;
            _isActive = true;

            _mapControl.MouseMove += OnMouseMove;
            _mapControl.MouseLeave += OnMouseLeave;
            _mapControl.MouseLeftButtonUp += OnMouseLeftButtonUp;
            _mapControl.PreviewKeyDown += OnPreviewKeyDown;
            // Ensure we receive keyboard events (ESC)
            _mapControl.Focusable = true;
            _mapControl.Focus();
        }

        public void Stop()
        {
            if (!_isActive) return;
            _isActive = false;

            _hoverTimer.Stop();
            _mapControl.MouseMove -= OnMouseMove;
            _mapControl.MouseLeave -= OnMouseLeave;
            _mapControl.MouseLeftButtonUp -= OnMouseLeftButtonUp;
            _mapControl.PreviewKeyDown -= OnPreviewKeyDown;

            _overlay.Clear();
            _startFeature = null;
            _activeGraph = null;
        }

        public void ResetToFirstSelection()
        {
            // Keep mode active but clear selections
            _overlay.Clear();
            _startFeature = null;
            _activeGraph = null;
        }

        public void RetryKeepFirst()
        {
            // Keep first selection visible, clear transient path by re-setting only the start feature
            if (_startFeature != null)
                _overlay.SetFeatures(new[] { _startFeature });
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!_isActive) return;
            if (e.Key == Key.Escape)
            {
                Stop();
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isActive) return;
            if (e.LeftButton == MouseButtonState.Pressed || e.RightButton == MouseButtonState.Pressed)
            {
                _hoverTimer.Stop();
                return;
            }

            var pos = e.GetPosition(_mapControl);
            _lastMousePos = new MPoint(pos.X, pos.Y);
            _hoverTimer.Stop();
            _hoverTimer.Start();
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            if (!_isActive) return;
            _hoverTimer.Stop();
            // Keep first selection visible (already set in angiv layer), clear transient path by re-setting only the start feature
            if (_startFeature != null)
                _overlay.SetFeatures(new[] { _startFeature });
            // Keep first selection visible (already set), clear transient path/hover
            if (_startFeature != null)
                _overlay.SetFeatures(new[] { _startFeature });
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isActive) return;

            var pos = e.GetPosition(_mapControl);
            var info = _mapControl.GetMapInfo(new MPoint(pos.X, pos.Y));
            var feature = info?.Feature as AnalysisFeature;
            if (feature == null) return;

            if (_startFeature == null)
            {
                // First selection
                _startFeature = feature;
                _activeGraph = _featureToGraph.TryGetValue(_startFeature, out var g) ? g : null;
                if (_activeGraph == null) return; // no graph -> ignore

                _overlay.SetFeatures(new[] { _startFeature });
            }
            else
            {
                // Second selection
                if (!_featureToGraph.TryGetValue(feature, out var g) || !ReferenceEquals(g, _activeGraph))
                {
                    return; // other graph, ignore
                }

                var pathFeatures = _lineGraphManager.TryGetShortestPath(_startFeature, feature);
                var path = pathFeatures?.Select(f => _activeGraph!.Edges.First(e => ReferenceEquals(e.PipeSegment, f)));
                if (path != null)
                {
                    var featuresOnPath = path.Select(e2 => e2.PipeSegment).ToList();
                    // Show full path
                    _overlay.SetFeatures(featuresOnPath);
                    PathFinalized?.Invoke(featuresOnPath);
                }

                // Remain in mode or stop will be decided by caller
            }
        }

        private void OnHoverTimerTick(object sender, EventArgs e)
        {
            _hoverTimer.Stop();
            if (!_isActive) return;

            var info = _mapControl.GetMapInfo(_lastMousePos);
            var current = info?.Feature as AnalysisFeature;

            // Stage 0: before first selection – highlight hovered feature only
            if (_startFeature == null)
            {
                if (current != null)
                    _overlay.SetFeatures(new[] { current });
                else
                    _overlay.Clear();
                return;
            }

            // Stage 1: after first selection – union of first + preview path to hovered
            if (current == null)
            {
                if (_startFeature != null)
                    _overlay.SetFeatures(new[] { _startFeature });
                else
                    _overlay.Clear();
                return;
            }

            if (!_featureToGraph.TryGetValue(current, out var g) || !ReferenceEquals(g, _activeGraph))
            {
                if (_startFeature != null)
                    _overlay.SetFeatures(new[] { _startFeature });
                else
                    _overlay.Clear();
                return;
            }

            var pathFeatures = _lineGraphManager.TryGetShortestPath(_startFeature!, current);
            var path = pathFeatures?.Select(f => _activeGraph!.Edges.First(e => ReferenceEquals(e.PipeSegment, f)));
            if (path == null)
            {
                _overlay.SetFeatures(new[] { _startFeature! });
                return;
            }

            // Union: first + path
            var union = new List<AnalysisFeature> { _startFeature! };
            union.AddRange(path.Select(e2 => e2.PipeSegment));
            // Ensure hovered feature is included even if path missed it
            if (!union.Contains(current)) union.Add(current);
            _overlay.SetFeatures(union);
        }

        public void Dispose()
        {
            Stop();
        }
    }
}


