using Autodesk.AutoCAD.Windows;
using IntersectUtilities.MPE.PipePlan;
using IntersectUtilities.MPE.PipePlanDE.ViewModels;
using IntersectUtilities.MPE.PipePlanDE.Views;

namespace IntersectUtilities.MPE.PipePlanDE;

// PDDRAW size palette: a small modeless window for picking the DN the next PDDRAW
// will draw. Shown by PDDRAW; the selection feeds PipePlanDEState.ActiveDn.
internal sealed class PipePlanDESizePalette : IDisposable
{
    private static readonly Guid PaletteGuid = new("3C8F1D55-7A24-4E91-B6D0-9E4F2A8C5071");

    private readonly PaletteSet _paletteSet;
    private readonly PipePlanDESizeViewModel _viewModel;

    public PipePlanDESizePalette()
    {
        _viewModel = new PipePlanDESizeViewModel();
        var view = new PipePlanDESizeView(_viewModel);

        _paletteSet = new PaletteSet("PipePlanDE – Tegn", "PIPEPLANDEDRAW", PaletteGuid)
        {
            Style = PaletteSetStyles.ShowCloseButton
                  | PaletteSetStyles.ShowAutoHideButton
                  | PaletteSetStyles.ShowTabForSingle,
            KeepFocus = false,
            MinimumSize = new System.Drawing.Size(220, 150)
        };

        _paletteSet.AddVisual("Dimension", view);
    }

    public void Show()
    {
        _paletteSet.Visible = true;
        _viewModel.ReloadCommand.Execute(null);
    }

    public void RebindTo(PipePlanDEState state) => _viewModel.Rebind(state);

    public void SetStatus(string message, PipePlanStatusKind kind) => _viewModel.SetStatus(message, kind);

    public void Dispose()
    {
        _paletteSet.Visible = false;
        _paletteSet.Dispose();
    }
}
