using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace IntersectUtilities.MPE.MatchBBR.ViewModels
{
    // One worksheet column taking part in a rule's Excel source. A rule owns an ordered list of
    // these; their values are joined with spaces to form the text the rule compares.
    internal sealed partial class ExcelColumnSlotViewModel : ObservableObject
    {
        private readonly CompareRuleViewModel _owner;

        public ExcelColumnSlotViewModel(CompareRuleViewModel owner, ColumnOption? selected)
        {
            _owner = owner;
            _selected = selected;
        }

        public ObservableCollection<ColumnOption> Columns => _owner.Columns;

        [ObservableProperty]
        private ColumnOption? _selected;

        // The last remaining slot cannot be removed: a rule with no Excel source is not a rule.
        public bool CanRemove => _owner.ExcelColumns.Count > 1;

        partial void OnSelectedChanged(ColumnOption? value) => _owner.OnRuleEdited();

        [RelayCommand]
        private void Remove() => _owner.RemoveColumn(this);

        public void RaiseCanRemoveChanged() => OnPropertyChanged(nameof(CanRemove));
    }

    // One row of the comparison-rule table: an Excel source on the left, a BBR property on the
    // right, and how to compare them. Exactly one rule in the table is the key, which is what the
    // two sides are joined on; the rest are evaluated on the pairs the key produced.
    //
    // Adding a rule is the intended way to grow the comparison — Areal, Type, Anvendelse — and
    // adding a column to a rule is how an address that spans more than Vejnavn + Husnummer is
    // expressed. Neither needs a code change.
    internal sealed partial class CompareRuleViewModel : ObservableObject
    {
        private readonly ExcelTabViewModel _owner;
        private bool _suppressNotify;

        public CompareRuleViewModel(ExcelTabViewModel owner, CompareRule model)
        {
            _owner = owner;
            Id = model.Id;

            _suppressNotify = true;
            _isKey = model.IsKey;
            _bbrProperty = string.IsNullOrWhiteSpace(model.BbrPropertyName) ? null : model.BbrPropertyName;
            _kind = CompareRuleKindOption.All.FirstOrDefault(x => x.Kind == model.Kind)
                    ?? CompareRuleKindOption.All[0];
            _toleranceText = model.Tolerance.ToString("0.###", CultureInfo.CurrentCulture);
            _toleranceIsPercent = model.ToleranceIsPercent;

            foreach (string column in model.ExcelColumns)
            {
                ExcelColumns.Add(new ExcelColumnSlotViewModel(this, owner.FindColumn(column)));
            }

            if (ExcelColumns.Count == 0)
            {
                ExcelColumns.Add(new ExcelColumnSlotViewModel(this, null));
            }

            _suppressNotify = false;
        }

        public string Id { get; }

        public ObservableCollection<ExcelColumnSlotViewModel> ExcelColumns { get; } = new();

        public ObservableCollection<ColumnOption> Columns => _owner.Columns;

        public IReadOnlyList<string> BbrProperties => _owner.BbrProperties;

        public IReadOnlyList<CompareRuleKindOption> Kinds => CompareRuleKindOption.All;

        [ObservableProperty]
        private bool _isKey;

        [ObservableProperty]
        private string? _bbrProperty;

        [ObservableProperty]
        private CompareRuleKindOption _kind;

        [ObservableProperty]
        private string _toleranceText = "5";

        [ObservableProperty]
        private bool _toleranceIsPercent = true;

        public bool IsNumeric => Kind.Kind == CompareRuleKind.Numeric;

        // Joining several columns produces text, so there is no number for a numeric rule to read.
        // Surfaced rather than silently producing "ikke sammenlignelig" on every row.
        public bool IsNumericWithJoinedColumns => IsNumeric && ExcelColumns.Count > 1;

        [RelayCommand]
        private void AddColumn()
        {
            ExcelColumns.Add(new ExcelColumnSlotViewModel(this, null));
            RefreshColumnState();
            OnRuleEdited();
        }

        public void RemoveColumn(ExcelColumnSlotViewModel slot)
        {
            if (ExcelColumns.Count <= 1)
            {
                return;
            }

            ExcelColumns.Remove(slot);
            RefreshColumnState();
            OnRuleEdited();
        }

        [RelayCommand]
        private void Remove() => _owner.RemoveRule(this);

        partial void OnIsKeyChanged(bool value)
        {
            if (value)
            {
                _owner.MakeKey(this);
            }

            OnRuleEdited();
        }

        partial void OnBbrPropertyChanged(string? value) => OnRuleEdited();

        partial void OnKindChanged(CompareRuleKindOption value)
        {
            OnPropertyChanged(nameof(IsNumeric));
            OnPropertyChanged(nameof(IsNumericWithJoinedColumns));
            OnRuleEdited();
        }

        partial void OnToleranceTextChanged(string value) => OnRuleEdited();

        partial void OnToleranceIsPercentChanged(bool value) => OnRuleEdited();

        // Clears the key flag without re-entering MakeKey, used when another rule takes the key.
        public void ClearKeyQuiet()
        {
            _suppressNotify = true;
            IsKey = false;
            _suppressNotify = false;
        }

        public CompareRule ToModel() =>
            new CompareRule
            {
                Id = Id,
                IsKey = IsKey,
                ExcelColumns = ExcelColumns
                    .Select(slot => slot.Selected?.Column ?? string.Empty)
                    .Where(column => !string.IsNullOrWhiteSpace(column))
                    .ToList(),
                BbrPropertyName = BbrProperty ?? string.Empty,
                Kind = Kind.Kind,
                Tolerance = ParseTolerance(ToleranceText),
                ToleranceIsPercent = ToleranceIsPercent,
            };

        public void OnRuleEdited()
        {
            OnPropertyChanged(nameof(IsNumericWithJoinedColumns));

            if (_suppressNotify)
            {
                return;
            }

            _owner.OnRuleEdited();
        }

        private void RefreshColumnState()
        {
            foreach (ExcelColumnSlotViewModel slot in ExcelColumns)
            {
                slot.RaiseCanRemoveChanged();
            }
        }

        private static double ParseTolerance(string text)
        {
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out double value)
                || double.TryParse(
                    (text ?? string.Empty).Replace(',', '.'),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out value))
            {
                return Math.Abs(value);
            }

            return 0.0;
        }
    }
}
