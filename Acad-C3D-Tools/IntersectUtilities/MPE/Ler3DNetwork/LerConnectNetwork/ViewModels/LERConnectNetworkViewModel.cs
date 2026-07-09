using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace IntersectUtilities.MPE.Ler3DNetwork.LerConnectNetwork.ViewModels
{
    internal sealed partial class LERConnectNetworkViewModel : ObservableObject
    {
        // Nullable: the palette is constructed up front and the view model exists
        // before the first Rebind. Commands early-out gracefully when null.
        private LERConnectNetworkState? _state;

        // ---- Parameters ------------------------------------------------------

        [ObservableProperty]
        private string _checkDistance = "0.1";

        [ObservableProperty]
        private string _minSlope = "20";

        // ---- Status / result -------------------------------------------------

        [ObservableProperty]
        private string _status = "Netværk ikke indlæst.\nKør LERCONNECTNETWORK eller klik knappen nedenfor.";

        [ObservableProperty]
        private string _statusColor = "#A0AEC0";

        [ObservableProperty]
        private string _result = "Ingen forhåndsvisning endnu.";

        [ObservableProperty]
        private string _resultColor = "#A0AEC0";

        // Error checkpoint line (bold): red with a count when flagged connectors
        // exist, otherwise a muted "Ingen fejl".
        [ObservableProperty]
        private string _errorText = "Ingen fejl";

        [ObservableProperty]
        private string _errorColor = "#A0AEC0";

        // ---- Preview visibility ----------------------------------------------

        [ObservableProperty]
        private bool _show2D;

        [ObservableProperty]
        private bool _show3D;

        [ObservableProperty]
        private bool _showMains;

        [ObservableProperty]
        private bool _showChildren;

        [ObservableProperty]
        private bool _showGrouped;

        [ObservableProperty]
        private bool _showUngrouped;

        [ObservableProperty]
        private bool _showErrors;

        // ---- Apply button ----------------------------------------------------

        [ObservableProperty]
        private string _applyButtonText = "Anvend tilslutninger";

        [ObservableProperty]
        private bool _canApply;

        public void Rebind(LERConnectNetworkState state)
        {
            _state = state;
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
            _state?.GatherVisible();
            PushVisibility();
            RefreshResult();
        }

        [RelayCommand]
        public void SelectObjects()
        {
            _state?.SelectAndGather();
            PushVisibility();
            RefreshResult();
        }

        [RelayCommand]
        public void UpdatePreview()
        {
            if (_state is null) return;
            if (!TryGetDistance(out double distance)) return;
            if (!TryGetSlope(out double slope)) return;
            PushVisibility();
            _state.UpdatePreview(distance, slope);
            RefreshResult();
        }

        [RelayCommand]
        public void ApplyConnections()
        {
            _state?.ApplyConnections();
            RefreshResult();
        }

        // Toggle changes flow straight to the renderer.
        partial void OnShow2DChanged(bool value) => PushVisibility();
        partial void OnShow3DChanged(bool value) => PushVisibility();
        partial void OnShowMainsChanged(bool value) => PushVisibility();
        partial void OnShowChildrenChanged(bool value) => PushVisibility();
        partial void OnShowGroupedChanged(bool value) => PushVisibility();
        partial void OnShowUngroupedChanged(bool value) => PushVisibility();
        partial void OnShowErrorsChanged(bool value) => PushVisibility();

        private void PushVisibility()
        {
            _state?.SetVisibility(Show2D, Show3D, ShowMains, ShowChildren, ShowGrouped, ShowUngrouped, ShowErrors);
        }

        private void RefreshResult()
        {
            if (_state is null || !_state.HasComputed)
            {
                Result = "Ingen forhåndsvisning endnu.";
                ResultColor = ColorFor(LerStatusKind.Info);
                ErrorText = "Ingen fejl";
                ErrorColor = ColorFor(LerStatusKind.Info);
                ApplyButtonText = "Anvend tilslutninger";
                CanApply = false;
                return;
            }

            int connected = _state.ConnectedCount;
            string text = $"{connected} tilslutninger fundet.";
            if (_state.ConflictCount > 0)
            {
                text += $"\n{_state.ConflictCount} konflikter kræver manuel kontrol.";
            }
            if (_state.NoMainCount > 0)
            {
                text += $"\n{_state.NoMainCount} 2D-linjer uden hovedledning.";
            }

            Result = text;
            ResultColor = ColorFor(connected > 0 ? LerStatusKind.Ok : LerStatusKind.Warning);
            ApplyButtonText = connected > 0 ? $"Anvend {connected} tilslutninger" : "Anvend tilslutninger";
            CanApply = connected > 0;

            int errors = _state.ErrorCount;
            ErrorText = errors > 0
                ? $"{errors} fejl: krydser flere steder / rammer ikke / for lange"
                : "Ingen fejl";
            ErrorColor = ColorFor(errors > 0 ? LerStatusKind.Error : LerStatusKind.Info);
        }

        private static string ColorFor(LerStatusKind kind) => kind switch
        {
            LerStatusKind.Ok => "#48BB78",
            LerStatusKind.Warning => "#ED8936",
            LerStatusKind.Error => "#E53E3E",
            _ => "#A0AEC0"
        };

        private bool TryGetDistance(out double distance)
        {
            if (TryParsePositive(CheckDistance, out distance))
            {
                return true;
            }
            SetStatus("Tjekafstand (m) skal være et positivt tal.", LerStatusKind.Error);
            return false;
        }

        private bool TryGetSlope(out double slope)
        {
            if (TryParsePositive(MinSlope, out slope))
            {
                return true;
            }
            SetStatus("Minimumshældning (‰) skal være et positivt tal.", LerStatusKind.Error);
            return false;
        }

        private static bool TryParsePositive(string text, out double value)
        {
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && value > 0.0)
            {
                return true;
            }
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) && value > 0.0)
            {
                return true;
            }
            value = 0.0;
            return false;
        }
    }
}
