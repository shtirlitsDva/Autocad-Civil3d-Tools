using Autodesk.AutoCAD.Windows;
using IntersectUtilities.MPE.PipePlan.ViewModels;
using IntersectUtilities.MPE.PipePlan.Views;

namespace IntersectUtilities.MPE.PipePlan;

internal sealed class PipePlanPalette : IDisposable
{
    private static readonly Guid PaletteGuid = new("F93D92E8-1C4B-4D12-8E89-2F8D5A7B3E91");

    private readonly PaletteSet _paletteSet;
    private readonly PipePlanSettingsViewModel _viewModel;

    public PipePlanPalette()
    {
        // The palette is process-wide and outlives any single document. Construct
        // the view model without a binding; PipePlanRuntime rebinds it via
        // RebindTo on each DocumentActivated event.
        _viewModel = new PipePlanSettingsViewModel();
        var view = new PipePlanSettingsView(_viewModel);

        _paletteSet = new PaletteSet("PipePlan", "PIPEPLAN", PaletteGuid)
        {
            Style = PaletteSetStyles.ShowCloseButton
                  | PaletteSetStyles.ShowAutoHideButton
                  | PaletteSetStyles.ShowTabForSingle,
            KeepFocus = false
        };

        _paletteSet.AddVisual("Radier", view);
    }

    public void Show()
    {
        _paletteSet.Visible = true;
        _viewModel.ReloadCommand.Execute(null);
    }

    public void RebindTo(PipePlanState state)
    {
        _viewModel.Rebind(state);
    }

    public void SetStatus(string message, PipePlanStatusKind kind)
    {
        _viewModel.SetStatus(message, kind);
    }

    public void Dispose()
    {
        _paletteSet.Visible = false;
        _paletteSet.Dispose();
    }
}
