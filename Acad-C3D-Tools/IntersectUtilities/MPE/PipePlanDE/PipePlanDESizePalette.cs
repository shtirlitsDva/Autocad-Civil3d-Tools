using IntersectUtilities.MPE.PipePlanDE.ViewModels;
using IntersectUtilities.MPE.PipePlanDE.Views;

namespace IntersectUtilities.MPE.PipePlanDE;

// PDDRAW size palette: a small modeless window for picking the DN (and depth band) the
// next PDDRAW will draw. Shown by PDDRAW; the selection feeds PipePlanDEState.
internal sealed class PipePlanDESizePalette : PipePlanDEPaletteBase
{
    private static readonly Guid PaletteGuid = new("3C8F1D55-7A24-4E91-B6D0-9E4F2A8C5071");

    private readonly PipePlanDESizeViewModel _viewModel;

    public PipePlanDESizePalette()
        : this(new PipePlanDESizeViewModel())
    {
    }

    private PipePlanDESizePalette(PipePlanDESizeViewModel viewModel)
        : base(viewModel, new PipePlanDESizeView(viewModel),
               "PipePlanDE – Tegn", "PIPEPLANDEDRAW", PaletteGuid, "Dimension",
               new System.Drawing.Size(220, 150))
    {
        _viewModel = viewModel;
    }

    // Cheap (no DB I/O) — always rebind so the picker reflects the active drawing's state.
    public override void RebindTo(PipePlanDEState state) => _viewModel.Rebind(state);
}
