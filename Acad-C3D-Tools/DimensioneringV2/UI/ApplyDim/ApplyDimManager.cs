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

using DimensioneringV2.StateMachine;

namespace DimensioneringV2.UI.ApplyDim
{
    internal class ApplyDimManager : IDisposable
    {
        private readonly MapControl _mapControl;
        private readonly IEnumerable<UndirectedGraph<NodeJunction, EdgePipeSegment>> _graphs;
        private readonly ApplyDimOverlayManager _overlay;
        private ApplyDimGraphManager _lineGraphManager;

        private readonly DispatcherTimer _hoverTimer;
        private MPoint _lastMousePos = new MPoint(0, 0);
        private bool _isActive;

        private AnalysisFeature? _startFeature;
        // No per-graph state here; all graph-related operations are delegated to ApplyDimGraphManager

        public event Action<IEnumerable<AnalysisFeature>>? PathFinalized;
        public event Action? Stopped;

        public ApplyDimManager(
            MapControl mapControl,
            IEnumerable<UndirectedGraph<NodeJunction, EdgePipeSegment>> graphs,
            ApplyDimOverlayManager overlay)
        {
            _mapControl = mapControl;
            _graphs = graphs;
            _overlay = overlay;

            _hoverTimer = new DispatcherTimer(
                TimeSpan.FromMilliseconds(200), DispatcherPriority.Normal, 
                OnHoverTimerTick, Dispatcher.CurrentDispatcher);
            _hoverTimer.Stop();

            _lineGraphManager = new ApplyDimGraphManager(_graphs);
        }        

        public void Start()
        {
            if (_isActive) return;
            _isActive = true;

            // Ensure overlay renders above features
            _overlay.BringToFront();
            _overlay.Clear();
            _startFeature = null;
            _fsm = BuildFsm();

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

            _overlay.RemoveLayer();
            _startFeature = null;
            _fsm = null;
            Stopped?.Invoke();
        }

        public void ResetToFirstSelection()
        {
            // Keep mode active but clear selections
            _overlay.Clear();
            // After VM UpdateMap(), the overlay layer may fall under the Features layer.
            // Ensure it's on top again for stage-1 hovering.
            _overlay.BringToFront();
            _startFeature = null;
            _fsm = BuildFsm();
        }

        public void RetryKeepFirst()
        {
            // Keep first selection visible, clear transient path by re-setting only the start feature
            if (_startFeature != null)
                _overlay.SetFeatures([_startFeature]);
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!_isActive) return;
            if (e.Key == Key.Escape)
            {
                _fsm?.Fire(FsmEvent.Esc);
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
            _fsm?.Fire(FsmEvent.Leave);
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isActive) return;

            var pos = e.GetPosition(_mapControl);
            var info = _mapControl.GetMapInfo(new MPoint(pos.X, pos.Y));
            var feature = info?.Feature as AnalysisFeature;
            _fsm?.Fire(FsmEvent.Click, feature);
        }

        private void OnHoverTimerTick(object sender, EventArgs e)
        {
            _hoverTimer.Stop();
            if (!_isActive) return;

            var info = _mapControl.GetMapInfo(_lastMousePos);
            var current = info?.Feature as AnalysisFeature;
            _fsm?.Fire(FsmEvent.Hover, current);
        }

        // FSM glue
        private enum FsmState { PickFirst, PickSecond }
        private enum FsmEvent { Start, Hover, Click, Leave, Esc, Stop }

        private StateMachine<FsmState, FsmEvent>? _fsm;

        private StateMachine<FsmState, FsmEvent> BuildFsm()
        {
            var fsm = new StateMachine<FsmState, FsmEvent>(FsmState.PickFirst);

            // PickFirst → Hover
            fsm.Configure(FsmState.PickFirst, FsmEvent.Hover, FsmState.PickFirst, ctx =>
            {
                var hovered = ctx.Payload as AnalysisFeature;
                if (hovered != null) _overlay.SetFeatures([hovered]); else _overlay.Clear();
            });

            // PickFirst → Click
            fsm.Configure(FsmState.PickFirst, FsmEvent.Click, FsmState.PickSecond, ctx =>
            {
                var feature = ctx.Payload as AnalysisFeature; if (feature == null) return;
                _startFeature = feature; _overlay.SetFeatures([feature]);
            });

            // PickFirst → Esc → Stop
            fsm.Configure(FsmState.PickFirst, FsmEvent.Esc, FsmState.PickFirst, ctx => Stop());

            // PickFirst → Leave
            fsm.Configure(FsmState.PickFirst, FsmEvent.Leave, FsmState.PickFirst, ctx => _overlay.Clear());

            // PickSecond → Hover (preview)
            fsm.Configure(FsmState.PickSecond, FsmEvent.Hover, FsmState.PickSecond, ctx =>
            {
                var hovered = ctx.Payload as AnalysisFeature;
                var start = _startFeature;
                if (start == null) { fsm.Fire(FsmEvent.Stop); return; }
                if (hovered == null) { _overlay.SetFeatures([start]); return; }
                if (!_lineGraphManager.AreInSameGraph(start, hovered)) { _overlay.SetFeatures([start]); return; }
                var path = _lineGraphManager.TryGetShortestPath(start, hovered);
                if (path == null) { _overlay.SetFeatures([start]); return; }
                var union = new List<AnalysisFeature> { start }; union.AddRange(path);
                if (!union.Contains(hovered)) union.Add(hovered);
                _overlay.SetFeatures(union);
            });

            // PickSecond → Click (finalize)
            fsm.Configure(FsmState.PickSecond, FsmEvent.Click, FsmState.PickSecond, ctx =>
            {
                var clicked = ctx.Payload as AnalysisFeature; var start = _startFeature;
                if (start == null || clicked == null) return;
                if (ReferenceEquals(clicked, start)) { _overlay.SetFeatures([start]); PathFinalized?.Invoke([start]); return; }
                if (!_lineGraphManager.AreInSameGraph(start, clicked)) return;
                var path = _lineGraphManager.TryGetShortestPath(start, clicked);
                if (path != null) { var list = path.ToList(); _overlay.SetFeatures(list); PathFinalized?.Invoke(list); }
            });

            // PickSecond → Esc (back)
            fsm.Configure(FsmState.PickSecond, FsmEvent.Esc, FsmState.PickFirst, ctx => { _startFeature = null; _overlay.Clear(); });

            // PickSecond → Leave
            fsm.Configure(FsmState.PickSecond, FsmEvent.Leave, FsmState.PickSecond, ctx =>
            {
                var start = _startFeature; if (start != null) _overlay.SetFeatures([start]); else _overlay.Clear();
            });

            // Any → Stop
            fsm.Configure(FsmState.PickFirst, FsmEvent.Stop, FsmState.PickFirst, ctx => Stop());
            fsm.Configure(FsmState.PickSecond, FsmEvent.Stop, FsmState.PickFirst, ctx => Stop());

            return fsm;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}


