using System;
using Autodesk.AutoCAD.Windows;
using IntersectUtilities.MPE.Ler3DNetwork.LerAnalyseNetwork.ViewModels;
using IntersectUtilities.MPE.Ler3DNetwork.LerAnalyseNetwork.Views;

namespace IntersectUtilities.MPE.Ler3DNetwork.LerAnalyseNetwork
{
    // Single app-scoped palette hosting the WPF view. The view model is created
    // once and rebound to the active document's state on each command run.
    internal sealed class LerAnalyseNetworkPalette
    {
        private static readonly Guid PaletteGuid = new("C7A3B5E2-9D14-4F68-8A2B-1E0C9F7D6B53");

        private readonly PaletteSet _paletteSet;
        private readonly LerAnalyseNetworkViewModel _viewModel;

        public LerAnalyseNetworkPalette()
        {
            _viewModel = new LerAnalyseNetworkViewModel();
            LerAnalyseNetworkView view = new(_viewModel);

            _paletteSet = new PaletteSet("LER Analyse", "LERANALYSENETWORK", PaletteGuid)
            {
                Style = PaletteSetStyles.ShowCloseButton
                      | PaletteSetStyles.ShowAutoHideButton
                      | PaletteSetStyles.ShowTabForSingle,
                KeepFocus = false
            };

            _paletteSet.AddVisual("Analyse", view);
            _paletteSet.StateChanged += OnStateChanged;
        }

        public void Show()
        {
            _paletteSet.Visible = true;
        }

        public void RebindTo(LerAnalyseNetworkState state)
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
    }
}
