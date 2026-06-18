using IntersectUtilities.MPE.PipePlanDE.ViewModels;
using IntersectUtilities.MPE.PipePlanDE.Views;

namespace IntersectUtilities.MPE.PipePlanDE;

// PDSETTINGS palette: the editable Regel-Grabenprofil parameter table plus the
// reference diagram. Distinct from the PDDRAW size palette so the two roles —
// configuring the table vs. picking the DN to draw — live in their own windows.
internal sealed class PipePlanDESettingsPalette : PipePlanDEPaletteBase
{
    private static readonly Guid PaletteGuid = new("B7E9C4A2-3F61-4D88-9A0E-7C2D5B6F18A4");

    private readonly PipePlanDESettingsViewModel _viewModel;

    public PipePlanDESettingsPalette()
        : this(new PipePlanDESettingsViewModel())
    {
    }

    private PipePlanDESettingsPalette(PipePlanDESettingsViewModel viewModel)
        : base(viewModel, new PipePlanDESettingsView(viewModel),
               "PipePlanDE – Indstillinger", "PIPEPLANDESETTINGS", PaletteGuid, "Parametre")
    {
        _viewModel = viewModel;
    }

    // The table reads the active drawing's overrides — no per-document state to bind. It
    // is also expensive to rebuild (a NOD read + 17 rows), so only reload while visible;
    // Show() reloads when the palette is next opened.
    public override void RebindTo(PipePlanDEState state)
    {
        if (IsVisible)
        {
            _viewModel.Reload();
        }
    }
}
