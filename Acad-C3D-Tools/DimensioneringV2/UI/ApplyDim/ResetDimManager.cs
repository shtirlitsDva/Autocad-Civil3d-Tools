using DimensioneringV2.GraphFeatures;
using DimensioneringV2.StateMachine;

using Mapsui;
using Mapsui.UI.Wpf;

using System;
using System.Windows.Input;

namespace DimensioneringV2.UI.ApplyDim
{
    internal class ResetDimManager : IDisposable
    {
        private readonly MapControl _mapControl;

        public event Action<AnalysisFeature>? Finalized;
        public event Action? Stopped;

        public ResetDimManager(MapControl mapControl)
        {
            _mapControl = mapControl;
        }

        public void Start()
        {
            _fsm = BuildFsm();

            _mapControl.MouseLeave += OnMouseLeave;
            _mapControl.MouseLeftButtonUp += OnMouseLeftButtonUp;
            _mapControl.PreviewKeyDown += OnPreviewKeyDown;

            _mapControl.Focusable = true;
            _mapControl.Focus();
        }

        public void Stop()
        {
            _mapControl.MouseLeave -= OnMouseLeave;
            _mapControl.MouseLeftButtonUp -= OnMouseLeftButtonUp;
            _mapControl.PreviewKeyDown -= OnPreviewKeyDown;

            _fsm = null;
            Stopped?.Invoke();
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                _fsm?.Fire(FsmEvent.Esc);
            }
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            _fsm?.Fire(FsmEvent.Leave);
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(_mapControl);
            var info = _mapControl.GetMapInfo(new MPoint(pos.X, pos.Y));
            var feature = info?.Feature as AnalysisFeature;
            //Null check here prevents the dimmanager from exiting
            //If null is passed to the FSM, it will transition to next state
            //but selected feature is null
            //then the second stage loop runs, but first feature is null
            //so the loop exits the applydim feature
            //SO null check here prevents that
            if (feature == null) return;
            _fsm?.Fire(FsmEvent.Click, feature);
        }

        // FSM glue
        private enum FsmState { Pick }
        private enum FsmEvent { Start, Click, Leave, Esc, Stop }

        private StateMachine<FsmState, FsmEvent>? _fsm;

        private StateMachine<FsmState, FsmEvent> BuildFsm()
        {
            var fsm = new StateMachine<FsmState, FsmEvent>(FsmState.Pick);

            // PickFirst → Click
            fsm.Configure(FsmState.Pick, FsmEvent.Click, FsmState.Pick, ctx =>
            {
                var feature = ctx.Payload as AnalysisFeature; if (feature == null) return;
                Finalized?.Invoke(feature);
            });

            // PickFirst → Esc → Stop
            fsm.Configure(FsmState.Pick, FsmEvent.Esc, FsmState.Pick, ctx => Stop());

            // Any → Stop
            fsm.Configure(FsmState.Pick, FsmEvent.Stop, FsmState.Pick, ctx => Stop());

            return fsm;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}


