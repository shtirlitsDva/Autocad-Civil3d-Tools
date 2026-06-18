using Autodesk.AutoCAD.Windows;
using IntersectUtilities.MPE.PipePlan;
using IntersectUtilities.MPE.PipePlanDE.ViewModels;

namespace IntersectUtilities.MPE.PipePlanDE;

/// <summary>
/// Shared shell for the two PipePlanDE palettes. Holds the identical PaletteSet
/// construction, <see cref="Show"/> (which refreshes the view model), <see cref="SetStatus"/>
/// and disposal. Subclasses supply their identity (guid/title/tab/min size), the view +
/// view model, and how they rebind to a document's state (<see cref="RebindTo"/>).
/// </summary>
internal abstract class PipePlanDEPaletteBase : IDisposable
{
    private readonly PipePlanDEStatusViewModel _viewModel;

    protected PaletteSet PaletteSet { get; }

    protected PipePlanDEPaletteBase(
        PipePlanDEStatusViewModel viewModel,
        System.Windows.Media.Visual view,
        string title,
        string name,
        Guid guid,
        string tabName,
        System.Drawing.Size? minimumSize = null)
    {
        _viewModel = viewModel;
        PaletteSet = new PaletteSet(title, name, guid)
        {
            Style = PaletteSetStyles.ShowCloseButton
                  | PaletteSetStyles.ShowAutoHideButton
                  | PaletteSetStyles.ShowTabForSingle,
            KeepFocus = false
        };

        if (minimumSize is { } size)
        {
            PaletteSet.MinimumSize = size;
        }

        PaletteSet.AddVisual(tabName, view);
    }

    protected bool IsVisible => PaletteSet.Visible;

    public void Show()
    {
        PaletteSet.Visible = true;
        _viewModel.Reload();
    }

    public void SetStatus(string message, PipePlanStatusKind kind) => _viewModel.SetStatus(message, kind);

    /// <summary>Bind the palette to the newly-active document's state.</summary>
    public abstract void RebindTo(PipePlanDEState state);

    public void Dispose()
    {
        PaletteSet.Visible = false;
        PaletteSet.Dispose();
    }
}
