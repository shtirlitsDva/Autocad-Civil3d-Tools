using Autodesk.AutoCAD.Windows;
using IntersectUtilities.MPE.PipePlan;
using IntersectUtilities.MPE.PipePlanDE.ViewModels;
using IntersectUtilities.MPE.PipePlanDE.Views;

namespace IntersectUtilities.MPE.PipePlanDE;

// PDSETTINGS palette: the editable Regel-Grabenprofil parameter table plus the
// reference diagram. Distinct from the PDDRAW size palette so the two roles —
// configuring the table vs. picking the DN to draw — live in their own windows.
internal sealed class PipePlanDESettingsPalette : IDisposable
{
    private static readonly Guid PaletteGuid = new("B7E9C4A2-3F61-4D88-9A0E-7C2D5B6F18A4");

    private readonly PaletteSet _paletteSet;
    private readonly PipePlanDESettingsViewModel _viewModel;

    public PipePlanDESettingsPalette()
    {
        _viewModel = new PipePlanDESettingsViewModel();
        var view = new PipePlanDESettingsView(_viewModel);

        _paletteSet = new PaletteSet("PipePlanDE – Indstillinger", "PIPEPLANDESETTINGS", PaletteGuid)
        {
            Style = PaletteSetStyles.ShowCloseButton
                  | PaletteSetStyles.ShowAutoHideButton
                  | PaletteSetStyles.ShowTabForSingle,
            KeepFocus = false
        };

        _paletteSet.AddVisual("Parametre", view);
    }

    public void Show()
    {
        _paletteSet.Visible = true;
        _viewModel.ReloadCommand.Execute(null);
    }

    public void RebindTo(PipePlanDEState state) => _viewModel.Rebind();

    public void SetStatus(string message, PipePlanStatusKind kind) => _viewModel.SetStatus(message, kind);

    public void Dispose()
    {
        _paletteSet.Visible = false;
        _paletteSet.Dispose();
    }
}
