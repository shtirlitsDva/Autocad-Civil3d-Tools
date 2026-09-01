using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace IntersectUtilities.MPE.MatchBBR.ViewModels
{
    // Status line shared by both tabs. Kept on a base class rather than on the root view model
    // because each tab is a separate UserControl with its own DataContext — AutoCAD's PaletteSet
    // gives us the tab strip, so there is no common visual parent to bind through.
    internal abstract partial class MatchBbrTabViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _status = "Klar.";

        [ObservableProperty]
        private string _statusColor = "#A0AEC0";

        [ObservableProperty]
        private bool _isBusy;

        public void SetStatus(string message, MatchBbrStatusKind kind)
        {
            Status = message;
            StatusColor = kind switch
            {
                MatchBbrStatusKind.Ok => "#48BB78",
                MatchBbrStatusKind.Warning => "#ED8936",
                MatchBbrStatusKind.Error => "#E53E3E",
                _ => "#A0AEC0",
            };
        }
    }

    // Owns the two tab view models and the state they share. One instance lives for the life of
    // the palette; Rebind swaps in the state of whichever document is active.
    internal sealed class MatchBbrViewModel
    {
        public MatchBbrViewModel()
        {
            Excel = new ExcelTabViewModel(this);
            Compare = new CompareTabViewModel(this);
        }

        public ExcelTabViewModel Excel { get; }

        public CompareTabViewModel Compare { get; }

        public MatchBbrState? State { get; private set; }

        // Raised when the rule set changes so the comparison grid can rebuild its dynamic columns.
        public event Action? RulesChanged;

        public void Rebind(MatchBbrState state)
        {
            if (State is not null)
            {
                State.StatusSink = null;
            }

            State = state;
            state.StatusSink = SetStatus;

            Excel.Rebind(state);
            Compare.Rebind(state);
        }

        public void NotifyRulesChanged()
        {
            State?.ReplaceRules(Excel.BuildRules());
            Compare.OnRulesChanged();
            RulesChanged?.Invoke();
        }

        public void SetStatus(string message, MatchBbrStatusKind kind)
        {
            Excel.SetStatus(message, kind);
            Compare.SetStatus(message, kind);
        }
    }
}
