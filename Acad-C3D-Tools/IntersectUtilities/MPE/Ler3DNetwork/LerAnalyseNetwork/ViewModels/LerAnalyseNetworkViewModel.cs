using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace IntersectUtilities.MPE.Ler3DNetwork.LerAnalyseNetwork.ViewModels
{
    internal sealed partial class LerAnalyseNetworkViewModel : ObservableObject
    {
        // Nullable: the palette is constructed up front and the view model exists
        // before the first Rebind. Commands early-out gracefully when null.
        private LerAnalyseNetworkState? _state;

        // "Fiks alle broer" is a two-click arm/confirm: the first click previews the
        // mutation, the second performs it. Any other interaction disarms it.
        private bool _fixAllArmed;

        // ---- Status / action feedback ----------------------------------------

        [ObservableProperty]
        private string _status = "Netværk ikke analyseret.\nKør LERANALYSENETWORK eller klik knappen nedenfor.";

        [ObservableProperty]
        private string _statusColor = "#A0AEC0";

        // The line under Handlinger: confirm prompts, skip warnings, fix results.
        [ObservableProperty]
        private string _result = "Ingen analyse endnu.";

        [ObservableProperty]
        private string _resultColor = "#A0AEC0";

        // ---- Result metrics (the scannable table) ----------------------------

        [ObservableProperty]
        private int _pivotCount;

        [ObservableProperty]
        private int _lineCount;

        [ObservableProperty]
        private int _bridgeCount;

        [ObservableProperty]
        private int _floatingCount;

        [ObservableProperty]
        private int _outOfRangeCount;

        // ---- Display filters --------------------------------------------------

        [ObservableProperty]
        private bool _showBridges = true;

        [ObservableProperty]
        private bool _showFloating = true;

        [ObservableProperty]
        private bool _showOutOfRange;

        // Downhill slope arrows on the 3D pipes.
        [ObservableProperty]
        private bool _showSlope;

        // The 3D pivots (drainage pipes) drawn in blue.
        [ObservableProperty]
        private bool _showPivots;

        // Inspect (lift vectors on hover) is now a display filter alongside the rest.
        [ObservableProperty]
        private bool _inspectMode;

        // ---- Scan depth -------------------------------------------------------

        // How many polylines out from each pivot the scanner reaches.
        [ObservableProperty]
        private int _scanDepth = 1;

        // The slider's upper bound — editable on the right of the slider, defaulted
        // to the deepest bridge found.
        [ObservableProperty]
        private int _scanDepthMax = 1;

        // ---- Action button (Fiks alle broer) ---------------------------------

        // The button is styled DangerButton (outlined red); the arm state is carried
        // by the text alone so the template's hover triggers can't override it.
        [ObservableProperty]
        private string _fixAllButtonText = "Fiks alle broer…";

        public void Rebind(LerAnalyseNetworkState state)
        {
            _state = state;
            _state.SetInspect(InspectMode);
            RefreshResult();
        }

        public void SetStatus(string message, LerStatusKind kind)
        {
            Status = message;
            StatusColor = ColorFor(kind);
        }

        public void OnPaletteHidden()
        {
            _state?.ClearAllPreview();
        }

        // ---- Commands --------------------------------------------------------

        [RelayCommand]
        public void LoadDrawing()
        {
            DisarmFixAll();
            _state?.Gather();
            PushVisibility();
            SyncDepthBounds();
            RefreshResult();
        }

        [RelayCommand]
        public void FixIslands()
        {
            DisarmFixAll();
            _state?.FixSelectedIslands();
            SyncDepthBounds();
            RefreshResult();
        }

        [RelayCommand]
        public void FixAllGreen()
        {
            if (_state is null) return;

            // First click: preview against real numbers and arm.
            if (!_fixAllArmed)
            {
                LerGreenFixPreview pv = _state.PreviewAllGreen();
                if (pv.Polylines == 0)
                {
                    Result = pv.Skipped > 0
                        ? $"{pv.Skipped} grønne broer kunne ikke forankres til to pivoter."
                        : $"Ingen broer at hæve ved scanningsdybde {ScanDepth}.";
                    ResultColor = ColorFor(LerStatusKind.Warning);
                    return;
                }

                string msg =
                    $"{pv.Polylines} polylinjer hæves · Z ∈ [{pv.ZMin:0.00}, {pv.ZMax:0.00}] m.";
                if (pv.Skipped > 0) msg += $" {pv.Skipped} springes over (kan ikke forankres).";
                msg += " Klik igen for at udføre.";
                Result = msg;
                ResultColor = ColorFor(LerStatusKind.Warning);

                _fixAllArmed = true;
                FixAllButtonText = $"Bekræft: hæv {pv.Polylines}";
                return;
            }

            // Second click: perform the mutation.
            DisarmFixAll();
            _state.FixAllGreen();
            SyncDepthBounds();
            RefreshResult();
        }

        // Inspect on/off → start/stop hover previews.
        partial void OnInspectModeChanged(bool value) => _state?.SetInspect(value);

        // Toggle changes flow straight to the renderer.
        partial void OnShowBridgesChanged(bool value) => PushVisibility();
        partial void OnShowFloatingChanged(bool value) => PushVisibility();
        partial void OnShowOutOfRangeChanged(bool value) => PushVisibility();
        partial void OnShowSlopeChanged(bool value) => PushVisibility();
        partial void OnShowPivotsChanged(bool value) => PushVisibility();

        // Slider moved → re-scan-classify + re-render live, refresh counts, disarm.
        partial void OnScanDepthChanged(int value)
        {
            DisarmFixAll();
            _state?.SetScanDepth(value);
            RefreshResult();
        }

        partial void OnScanDepthMaxChanged(int value)
        {
            if (value < 1) { ScanDepthMax = 1; return; }
            if (ScanDepth > value) ScanDepth = value;
        }

        // After each (re)scan, refresh the slider's upper bound from the data (max =
        // the deepest reachable bridge) but KEEP the operator's current depth where
        // they left it — only clamping it down if the new max is shallower. Pressing
        // a button must not snap the slider back to the start.
        private void SyncDepthBounds()
        {
            int max = _state?.MaxBridgeDepth ?? 1;
            if (max < 1) max = 1;

            int keep = ScanDepth;
            if (keep < 1) keep = 1;
            if (keep > max) keep = max;

            ScanDepthMax = max;
            ScanDepth = keep;
            _state?.SetScanDepth(ScanDepth);
        }

        private void PushVisibility()
        {
            _state?.SetVisibility(ShowBridges, ShowFloating, ShowOutOfRange, ShowSlope, ShowPivots);
        }

        private void DisarmFixAll()
        {
            _fixAllArmed = false;
            FixAllButtonText = "Fiks alle broer…";
        }

        private void RefreshResult()
        {
            if (_state is null || !_state.HasComputed)
            {
                PivotCount = LineCount = BridgeCount = FloatingCount = OutOfRangeCount = 0;
                Result = "Ingen analyse endnu.";
                ResultColor = ColorFor(LerStatusKind.Info);
                return;
            }

            PivotCount = _state.PivotCount;
            LineCount = _state.PolylineCount;
            BridgeCount = _state.BridgeCount;
            FloatingCount = _state.FloatingCount;
            OutOfRangeCount = _state.OutOfRangeCount;

            // Don't stomp the arm/confirm prompt if the operator is mid-confirm.
            if (_fixAllArmed) return;

            Result = BridgeCount > 0
                ? $"{BridgeCount} broer kan hæves ved scanningsdybde {ScanDepth}."
                : $"Ingen broer ved scanningsdybde {ScanDepth}.";
            ResultColor = ColorFor(BridgeCount > 0 ? LerStatusKind.Ok : LerStatusKind.Warning);
        }

        private static string ColorFor(LerStatusKind kind) => kind switch
        {
            LerStatusKind.Ok => "#48BB78",
            LerStatusKind.Warning => "#ED8936",
            LerStatusKind.Error => "#E53E3E",
            _ => "#A0AEC0"
        };
    }
}
