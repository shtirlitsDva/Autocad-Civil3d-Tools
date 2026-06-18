using CommunityToolkit.Mvvm.ComponentModel;
using IntersectUtilities.MPE.PipePlan;

namespace IntersectUtilities.MPE.PipePlanDE.ViewModels;

/// <summary>
/// Shared base for the PipePlanDE palette view models. Owns the status line and the
/// single source of truth for the <see cref="PipePlanStatusKind"/> → colour mapping,
/// plus a <see cref="Reload"/> hook the hosting palette calls when it becomes visible.
/// </summary>
internal abstract partial class PipePlanDEStatusViewModel : ObservableObject
{
    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private string _statusColor = "#A0AEC0";

    public void SetStatus(string message, PipePlanStatusKind kind)
    {
        Status = message;
        StatusColor = kind switch
        {
            PipePlanStatusKind.Ok => "#48BB78",
            PipePlanStatusKind.Snap => "#63B3ED",
            PipePlanStatusKind.Warning => "#ED8936",
            PipePlanStatusKind.Error => "#E53E3E",
            _ => "#A0AEC0"
        };
    }

    /// <summary>Refresh the view from the active drawing. Called when the palette is shown.</summary>
    public abstract void Reload();
}
