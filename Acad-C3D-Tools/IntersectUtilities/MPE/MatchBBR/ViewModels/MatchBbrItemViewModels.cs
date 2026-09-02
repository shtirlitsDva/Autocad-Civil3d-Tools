using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace IntersectUtilities.MPE.MatchBBR.ViewModels
{
    // One selectable worksheet column. Identity is the column letter, not the header: headers
    // repeat and are sometimes blank, so binding on them would pick the wrong column.
    internal sealed class ColumnOption
    {
        public ColumnOption(string column, string display)
        {
            Column = column;
            Display = display;
        }

        public string Column { get; }

        public string Display { get; }

        public override string ToString() => Display;
    }

    internal sealed class CompareRuleKindOption
    {
        public CompareRuleKindOption(CompareRuleKind kind, string display)
        {
            Kind = kind;
            Display = display;
        }

        public CompareRuleKind Kind { get; }

        public string Display { get; }

        public override string ToString() => Display;

        public static IReadOnlyList<CompareRuleKindOption> All { get; } = new[]
        {
            new CompareRuleKindOption(CompareRuleKind.TextNormalized, "Tekst (normaliseret)"),
            new CompareRuleKindOption(CompareRuleKind.TextExact, "Tekst (eksakt)"),
            new CompareRuleKindOption(CompareRuleKind.Numeric, "Tal ± tolerance"),
        };
    }

    // One column's filter, hanging off that column's header in the preview grid — the same
    // gesture as Excel's own filter dropdown, which is how these workbooks get narrowed in
    // practice. Every column has one, and active filters combine with AND.
    internal sealed partial class ColumnFilterViewModel : ObservableObject
    {
        private readonly Action _onChanged;

        // A column with more distinct values than this is not something anyone filters by
        // picking from a list — it is a hash, an id or a free-text note. Building thousands of
        // checkboxes for every such column would cost far more than it is worth, so those
        // columns say so instead.
        public const int MaxDistinctValues = 1000;

        public ColumnFilterViewModel(
            string column,
            string header,
            IReadOnlyList<(string Value, int Count)> values,
            IReadOnlyCollection<string>? checkedValues,
            Action onChanged)
        {
            Column = column;
            Header = string.IsNullOrWhiteSpace(header) ? $"({column})" : header;
            _onChanged = onChanged;
            DistinctCount = values.Count;

            if (values.Count > MaxDistinctValues)
            {
                return;
            }

            HashSet<string>? selection = checkedValues is null
                ? null
                : new HashSet<string>(checkedValues, StringComparer.OrdinalIgnoreCase);

            foreach ((string value, int count) in values)
            {
                // No saved selection means everything is on: a filter narrows, it never hides by
                // default.
                bool isChecked = selection is null || selection.Contains(value);
                Values.Add(new FilterValueViewModel(value, count, isChecked, OnValueToggled));
            }
        }

        public int DistinctCount { get; }

        public bool IsFilterable => DistinctCount <= MaxDistinctValues;

        public string TooManyValuesText =>
            $"{DistinctCount} forskellige værdier — for mange til at filtrere på.";

        // Worksheet column letter, the stable identity — headers repeat and can be blank.
        public string Column { get; }

        public string Header { get; }

        public ObservableCollection<FilterValueViewModel> Values { get; } = new();

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private bool _isOpen;

        public IEnumerable<FilterValueViewModel> VisibleValues =>
            string.IsNullOrWhiteSpace(SearchText)
                ? Values
                : Values.Where(x => x.Value.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0);

        // Drives the funnel glyph in the header, so a filtered column is obvious at a glance
        // rather than only discoverable by opening every dropdown.
        public bool IsActive => Values.Any(x => !x.IsChecked);

        public int CheckedCount => Values.Count(x => x.IsChecked);

        public string Summary => IsActive ? $"{CheckedCount}/{Values.Count}" : string.Empty;

        public HashSet<string> AllowedValues() =>
            new HashSet<string>(
                Values.Where(x => x.IsChecked).Select(x => x.Value),
                StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<string> CheckedValues() =>
            Values.Where(x => x.IsChecked).Select(x => x.Value).ToList();

        [RelayCommand]
        private void SelectAll() => SetAll(true);

        [RelayCommand]
        private void ClearAll() => SetAll(false);

        [RelayCommand]
        private void Close() => IsOpen = false;

        partial void OnSearchTextChanged(string value) => OnPropertyChanged(nameof(VisibleValues));

        private void SetAll(bool value)
        {
            // Only what the search box is showing, so "Ryd" after typing "7.2" clears exactly the
            // districts on screen rather than silently everything.
            foreach (FilterValueViewModel filterValue in VisibleValues.ToList())
            {
                filterValue.SetCheckedQuiet(value);
            }

            OnValueToggled();
        }

        private void OnValueToggled()
        {
            // Refresh the header glyph and count, then let the tab re-apply the whole filter set.
            OnPropertyChanged(nameof(IsActive));
            OnPropertyChanged(nameof(CheckedCount));
            OnPropertyChanged(nameof(Summary));
            _onChanged();
        }
    }

    // One distinct value inside a column filter, with the number of rows carrying it.
    internal sealed partial class FilterValueViewModel : ObservableObject
    {
        private readonly Action _onChanged;

        public FilterValueViewModel(string value, int count, bool isChecked, Action onChanged)
        {
            Value = value;
            Count = count;
            _isChecked = isChecked;
            _onChanged = onChanged;
        }

        public string Value { get; }

        public int Count { get; }

        public string Display => string.IsNullOrWhiteSpace(Value)
            ? $"(tom)  ·  {Count}"
            : $"{Value}  ·  {Count}";

        [ObservableProperty]
        private bool _isChecked;

        private bool _suppressCallback;

        partial void OnIsCheckedChanged(bool value)
        {
            if (!_suppressCallback)
            {
                _onChanged();
            }
        }

        // Used by "Alle"/"Ryd", which re-apply the whole filter once instead of once per value.
        public void SetCheckedQuiet(bool value)
        {
            _suppressCallback = true;
            IsChecked = value;
            _suppressCallback = false;
        }
    }

    // A worksheet row in the Excel tab's preview grid. The indexer is what lets the grid bind
    // columns that are only known at runtime: a column is generated with Path=[D].
    internal sealed partial class ExcelRowViewModel : ObservableObject
    {
        private readonly Action _onIncludedChanged;

        public ExcelRowViewModel(ExcelRowRecord record, Action onIncludedChanged)
        {
            Record = record;
            _isChecked = record.IsIncluded;
            _onIncludedChanged = onIncludedChanged;
        }

        public ExcelRowRecord Record { get; }

        public string RowReference => Record.Row.RowReference;

        public string this[string column] => Record.Row[column];

        // The user's own tick. Separate from the column filters on purpose: a filter answers
        // "which rows am I working on", the tick answers "and is this particular one in". A row
        // takes part in the comparison only when it is both visible and ticked.
        [ObservableProperty]
        private bool _isChecked = true;

        private bool _suppressCallback;

        // Set by the column filters; drives whether the grid shows the row at all.
        public bool PassesFilters { get; private set; } = true;

        public bool IsIncluded => PassesFilters && IsChecked;

        partial void OnIsCheckedChanged(bool value)
        {
            Record.IsIncluded = IsIncluded;

            if (!_suppressCallback)
            {
                _onIncludedChanged();
            }
        }

        // Applied in bulk when filters change, so the summary is recounted once rather than once
        // per row — that matters when a district filter flips several thousand rows at a time.
        public void SetPassesFilters(bool value)
        {
            PassesFilters = value;
            Record.IsIncluded = IsIncluded;
            OnPropertyChanged(nameof(PassesFilters));
            OnPropertyChanged(nameof(IsIncluded));
        }

        public void SetCheckedQuiet(bool value)
        {
            _suppressCallback = true;
            IsChecked = value;
            _suppressCallback = false;
        }
    }

    // A row of the comparison grid.
    internal sealed partial class ComparisonRowViewModel : ObservableObject
    {
        private readonly CompareTabViewModel _owner;

        public ComparisonRowViewModel(ComparisonRow row, CompareTabViewModel owner)
        {
            Row = row;
            _owner = owner;
            SearchText = ComparisonRowSearch.Build(row);
        }

        public ComparisonRow Row { get; }

        public MatchStatus Status => Row.Status;

        public string StatusText => MatchBbrExport.StatusText(Row.Status);

        public string Reason => Row.Reason;

        public string Key => Row.Key;

        public string ExcelAddress => _owner.KeyRuleExcelText(Row);

        public string BbrAddress => Row.BbrRecord?.Adresse ?? string.Empty;

        public string District => _owner.DistrictText(Row);

        public bool HasBlock => Row.HasBlock;

        public bool IsWritable => Row.IsWritable;

        // Cell lookups for the runtime-generated rule columns: Path=[<ruleId>].ExcelText etc.
        public RuleCellResult? this[string ruleId] => Row.Cell(ruleId);

        // Text used by the free-text search box, precomputed so filtering a large grid does not
        // re-walk the cells on every keystroke.
        public string SearchText { get; }

        [RelayCommand]
        private void WriteCell(string? ruleId)
        {
            if (string.IsNullOrWhiteSpace(ruleId))
            {
                return;
            }

            _owner.WriteSingle(this, ruleId!);
        }

        public void RaiseAllChanged()
        {
            OnPropertyChanged(string.Empty);
        }
    }

    internal static class ComparisonRowSearch
    {
        public static string Build(ComparisonRow row)
        {
            IEnumerable<string> parts = new[]
                {
                    row.Key,
                    row.BbrRecord?.Adresse ?? string.Empty,
                    row.BbrRecord?.Distrikt ?? string.Empty,
                    row.Reason,
                }
                .Concat(row.Cells.SelectMany(c => new[] { c.ExcelText, c.BbrText }));

            return string.Join(
                " ",
                parts.Where(x => !string.IsNullOrWhiteSpace(x))).ToUpperInvariant();
        }
    }
}
