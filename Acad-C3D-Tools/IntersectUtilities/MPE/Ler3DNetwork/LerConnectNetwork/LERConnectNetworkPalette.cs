using System;
using Autodesk.AutoCAD.Windows;
using IntersectUtilities.MPE.Ler3DNetwork.LerConnectNetwork.ViewModels;
using IntersectUtilities.MPE.Ler3DNetwork.LerConnectNetwork.Views;

namespace IntersectUtilities.MPE.Ler3DNetwork.LerConnectNetwork
{
    // Single app-scoped palette hosting the WPF view. The view model is created
    // once and rebound to the active document's state on each command run.
    internal sealed class LERConnectNetworkPalette : IDisposable
    {
        private static readonly Guid PaletteGuid = new("B6F2A4D1-3C8E-4A57-9E2D-7F1C0A9B8E64");
        private static readonly System.Drawing.Size DefaultPaletteSize = new(1000, 800);

        private readonly PaletteSet _paletteSet;
        private readonly LERConnectNetworkViewModel _viewModel;
        private bool _sizedOnce;

        public LERConnectNetworkPalette()
        {
            _viewModel = new LERConnectNetworkViewModel();
            LERConnectNetworkView view = new(_viewModel);

            _paletteSet = new PaletteSet("LER Connect", "LERCONNECTNETWORK", PaletteGuid)
            {
                Style = PaletteSetStyles.ShowCloseButton
                      | PaletteSetStyles.ShowAutoHideButton
                      | PaletteSetStyles.ShowTabForSingle,
                KeepFocus = false
            };

            _paletteSet.AddVisual("Connect", view);
            _paletteSet.StateChanged += OnStateChanged;
        }

        public void Show()
        {
            _paletteSet.Visible = true;

            // First show only, so a size the user later drags is respected; see PaletteSizing.
            if (!_sizedOnce)
            {
                _sizedOnce = true;
                IntersectUtilities.MPE.Shared.PaletteSizing.ApplyDefault(_paletteSet, DefaultPaletteSize);
            }
        }

        public void RebindTo(LERConnectNetworkState state)
        {
            _viewModel.Rebind(state);
        }

        public void SetStatus(string message, LerStatusKind kind)
        {
            _viewModel.SetStatus(message, kind);
        }

        private void OnStateChanged(object? sender, PaletteSetStateEventArgs e)
        {
            if (!_paletteSet.Visible)
            {
                _viewModel.OnPaletteHidden();
            }
        }

        public void Dispose()
        {
            _paletteSet.StateChanged -= OnStateChanged;
            _paletteSet.Visible = false;
            _paletteSet.Dispose();
        }
    }
}
