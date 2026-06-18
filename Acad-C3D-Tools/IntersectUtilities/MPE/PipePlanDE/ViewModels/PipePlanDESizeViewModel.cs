using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using IntersectUtilities.MPE.PipePlan;

namespace IntersectUtilities.MPE.PipePlanDE.ViewModels;

/// <summary>
/// Drives the PDDRAW size palette: a single dropdown that picks the DN the next
/// PDDRAW will draw. The selection is mirrored into the per-document
/// <see cref="PipePlanDEState.ActiveDn"/>, which PDDRAW reads. The palette is
/// modeless and stays open across draws.
/// </summary>
internal sealed partial class PipePlanDESizeViewModel : PipePlanDEStatusViewModel
{
    // Nullable because the palette is constructed up front (process-wide) and may
    // briefly exist before the first DocumentActivated rebind.
    private PipePlanDEState? _state;
    private bool _suppressSelectionSideEffects;

    public PipePlanDESizeViewModel()
    {
        Status = "Vælg en dimension og kør PDDRAW.";
        foreach (int dn in PipePlanDEStandardTable.SelectableDns)
        {
            Dns.Add(new PipePlanDEDnVm(dn, $"DN {dn}"));
        }
    }

    public ObservableCollection<PipePlanDEDnVm> Dns { get; } = new();

    [ObservableProperty]
    private PipePlanDEDnVm? _selectedDn;

    /// <summary>Checked = depth &gt; 1.3 m → trench uses B1; unchecked = ≤ 1.3 m → B.</summary>
    [ObservableProperty]
    private bool _isDeep;

    public void Rebind(PipePlanDEState state)
    {
        _state = state;
        RestoreSelectionFromState();
    }

    public override void Reload() => RestoreSelectionFromState();

    partial void OnIsDeepChanged(bool value)
    {
        if (_state is not null)
        {
            _state.ActiveDepth = value ? PipePlanDETrenchDepth.Deep : PipePlanDETrenchDepth.Shallow;
        }

        if (!_suppressSelectionSideEffects)
        {
            SetStatus(
                value ? "Dybde > 1,3 m: graven bruger B1." : "Dybde ≤ 1,3 m: graven bruger B.",
                PipePlanStatusKind.Info);
        }
    }

    partial void OnSelectedDnChanged(PipePlanDEDnVm? value)
    {
        if (_state is not null)
        {
            _state.ActiveDn = value?.Dn;
        }

        if (_suppressSelectionSideEffects)
        {
            return;
        }

        if (value is not null)
        {
            SetStatus($"Aktiv dimension: DN {value.Dn}. Kør PDDRAW.", PipePlanStatusKind.Ok);
        }
    }

    private void RestoreSelectionFromState()
    {
        int? activeDn = _state?.ActiveDn;
        _suppressSelectionSideEffects = true;
        try
        {
            SelectedDn = activeDn is null
                ? null
                : Dns.FirstOrDefault(d => d.Dn == activeDn.Value);
            IsDeep = _state?.ActiveDepth == PipePlanDETrenchDepth.Deep;
        }
        finally
        {
            _suppressSelectionSideEffects = false;
        }
    }
}

internal sealed class PipePlanDEDnVm
{
    public PipePlanDEDnVm(int dn, string label)
    {
        Dn = dn;
        Label = label;
    }

    public int Dn { get; }

    public string Label { get; }
}
