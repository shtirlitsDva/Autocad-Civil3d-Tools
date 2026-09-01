using System;
using Autodesk.AutoCAD.Windows;
using IntersectUtilities.MPE.MatchBBR.ViewModels;
using IntersectUtilities.MPE.MatchBBR.Views;
using IntersectUtilities.MPE.Shared;

namespace IntersectUtilities.MPE.MatchBBR
{
    // App-scoped palette hosting the two MatchBBR tabs.
    //
    // The tabs are two AddVisual calls rather than a WPF TabControl. That is the house pattern
    // (NsCmdPaletteSet, SheetAutomationPaletteSet, GraphViewPalette all do it) and it is also the
    // better fit: AutoCAD's own tab strip docks and auto-hides with the palette, so a TabControl
    // inside one would be a tab strip nested in a tab strip.
    internal sealed class MatchBbrPalette : IDisposable
    {
        private static readonly Guid PaletteGuid = new("6D2F41B7-58C3-4E0A-9F1D-3C7A4B8E5D62");
        private static readonly System.Drawing.Size DefaultPaletteSize = new(1200, 800);

        private readonly PaletteSet _paletteSet;
        private readonly MatchBbrViewModel _viewModel;
        private bool _sizedOnce;

        public MatchBbrPalette()
        {
            _viewModel = new MatchBbrViewModel();

            MatchBbrExcelView excelView = new(_viewModel.Excel);
            MatchBbrCompareView compareView = new(_viewModel.Compare);

            _paletteSet = new PaletteSet("BBR match", MatchBbrConstants.CommandName, PaletteGuid)
            {
                Style = PaletteSetStyles.ShowCloseButton
                        | PaletteSetStyles.ShowAutoHideButton
                        | PaletteSetStyles.ShowTabForSingle,
                KeepFocus = false,
            };

            _paletteSet.AddVisual("Excel", excelView);
            _paletteSet.AddVisual("Sammenlign", compareView);
            _paletteSet.StateChanged += OnStateChanged;
        }

        // Closing or auto-hiding the palette drops the preview graphics: a preview that outlives
        // the window it belongs to is just litter on the screen with no way to turn it off.
        private void OnStateChanged(object? sender, PaletteSetStateEventArgs e)
        {
            if (!_paletteSet.Visible)
            {
                _viewModel.Compare.OnPaletteHidden();
            }
        }

        public void Show()
        {
            _paletteSet.Visible = true;

            // First show only, so a size the user has since dragged is respected; see PaletteSizing.
            if (!_sizedOnce)
            {
                _sizedOnce = true;
                PaletteSizing.ApplyDefault(_paletteSet, DefaultPaletteSize);
            }
        }

        public void RebindTo(MatchBbrState state) => _viewModel.Rebind(state);

        public void SetStatus(string message, MatchBbrStatusKind kind) =>
            _viewModel.SetStatus(message, kind);

        public void Dispose()
        {
            _paletteSet.StateChanged -= OnStateChanged;
            _paletteSet.Visible = false;
            _paletteSet.Dispose();
        }
    }
}
