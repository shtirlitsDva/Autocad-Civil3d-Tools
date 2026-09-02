using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FormsDialogResult = System.Windows.Forms.DialogResult;
using FormsOpenFileDialog = System.Windows.Forms.OpenFileDialog;

namespace IntersectUtilities.MPE.MatchBBR.ViewModels
{
    // Tab 1: pick the workbook, declare what is compared with what, and narrow the rows down to
    // the district you are actually working on.
    internal sealed partial class ExcelTabViewModel : MatchBbrTabViewModel
    {
        private readonly MatchBbrViewModel _root;

        // Observable, not a plain List: the column combo boxes in the rule table bind straight to
        // it, and switching worksheet refills it in place. A List would leave every combo showing
        // the previous sheet's columns, because the collection reference never changes.
        private readonly ObservableCollection<ColumnOption> _columns = new();

        private MatchBbrState? _state;
        private bool _suspendReactions;

        public ExcelTabViewModel(MatchBbrViewModel root)
        {
            _root = root;
            RowsView = CollectionViewSource.GetDefaultView(Rows);
            RowsView.Filter = FilterRow;
        }

        // Raised when the worksheet changes, so the view can rebuild the preview grid's columns —
        // they are only known once a sheet has been read.
        public event Action? SheetColumnsChanged;

        public ObservableCollection<ExcelRowViewModel> Rows { get; } = new();

        public ICollectionView RowsView { get; }

        public ObservableCollection<CompareRuleViewModel> Rules { get; } = new();

        public ObservableCollection<string> SheetNames { get; } = new();

        // One filter per worksheet column, keyed by column letter. The preview grid hands each
        // generated column its own entry as the header's DataContext.
        public Dictionary<string, ColumnFilterViewModel> ColumnFilters { get; } =
            new Dictionary<string, ColumnFilterViewModel>(StringComparer.OrdinalIgnoreCase);

        public ObservableCollection<ColumnOption> Columns => _columns;

        // The BBR property set's own property names, so a rule can never name a property that
        // does not exist. Spelling these by hand is a known trap (VarmeForbrug has a capital F).
        public IReadOnlyList<string> BbrProperties { get; } = new PSetDefs.BBR()
            .ListOfProperties()
            .Select(p => p.Name)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        [ObservableProperty]
        private string _workbookPath = string.Empty;

        [ObservableProperty]
        private string? _selectedSheet;

        [ObservableProperty]
        private ColumnOption? _districtColumn;

        [ObservableProperty]
        private bool _skipTypeIngen = true;

        [ObservableProperty]
        private string _summary = "Ingen fil indlæst.";

        public void Rebind(MatchBbrState state)
        {
            _state = state;
            state.SkipTypeIngen = SkipTypeIngen;

            if (!string.IsNullOrWhiteSpace(state.WorkbookPath))
            {
                WorkbookPath = state.WorkbookPath!;
            }
        }

        // ---- Commands ------------------------------------------------------

        [RelayCommand]
        private void Browse()
        {
            using FormsOpenFileDialog dialog = new FormsOpenFileDialog
            {
                Title = "Vælg Excel-fil",
                Filter = "Excel-projektmappe (*.xlsx)|*.xlsx",
                CheckFileExists = true,
                Multiselect = false,
            };

            if (!string.IsNullOrWhiteSpace(WorkbookPath))
            {
                string? directory = Path.GetDirectoryName(WorkbookPath);
                if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                {
                    dialog.InitialDirectory = directory;
                }
            }

            if (dialog.ShowDialog() != FormsDialogResult.OK)
            {
                return;
            }

            WorkbookPath = dialog.FileName;
            Load(null);
        }

        [RelayCommand]
        private void Reload() => Load(SelectedSheet);

        [RelayCommand]
        private void AddRule()
        {
            // Starts with one empty column slot; the + in the row adds more.
            CompareRule model = new CompareRule
            {
                IsKey = Rules.Count == 0,
                Kind = CompareRuleKind.TextNormalized,
            };

            Rules.Add(new CompareRuleViewModel(this, model));
            OnRuleEdited();
        }

        [RelayCommand]
        private void SelectAllRows() => SetAllRows(true);

        [RelayCommand]
        private void DeselectAllRows() => SetAllRows(false);

        // ---- Loading -------------------------------------------------------

        private void Load(string? sheetName)
        {
            if (_state is null || string.IsNullOrWhiteSpace(WorkbookPath))
            {
                SetStatus("Vælg en Excel-fil først.", MatchBbrStatusKind.Warning);
                return;
            }

            if (!_state.LoadWorkbook(WorkbookPath, sheetName))
            {
                return;
            }

            _suspendReactions = true;
            try
            {
                SheetNames.Clear();
                foreach (string name in _state.SheetNames)
                {
                    SheetNames.Add(name);
                }

                SelectedSheet = _state.SheetName;

                _columns.Clear();
                foreach (string column in _state.Sheet.ColumnOrder)
                {
                    _columns.Add(new ColumnOption(column, _state.Sheet.DescribeColumn(column)));
                }

                Rows.Clear();
                foreach (ExcelRowRecord record in _state.ExcelRows)
                {
                    Rows.Add(new ExcelRowViewModel(record, UpdateSummary));
                }

                MatchBbrSettings? saved = MatchBbrRuleStore.TryLoad(WorkbookPath);
                ApplyRules(saved?.Rules ?? SeedRules());
                DistrictColumn = FindColumn(saved?.DistrictColumn) ?? GuessDistrictColumn();

                RebuildColumnFilters(saved?.ColumnFilters);
            }
            finally
            {
                _suspendReactions = false;
            }

            SheetColumnsChanged?.Invoke();
            OnRuleEdited();
            ApplyFilter();
        }

        // Auto-detection: a workbook with Vejnavn and Husnummer gets a two-column key, one with a
        // single Adresse column gets a one-column key. Either can be flipped afterwards; the
        // point is that the common cases need no setup.
        private List<CompareRule> SeedRules()
        {
            List<CompareRule> seeded = new List<CompareRule>();

            string? vejnavn = FindColumnByHeader("vejnavn", "vej", "gade");
            string? husnummer = FindColumnByHeader("husnummer", "husnr", "nummer", "nr");
            string? adresse = FindColumnByHeader("adresse");

            CompareRule keyRule = new CompareRule
            {
                IsKey = true,
                BbrPropertyName = MatchBbrConstants.AdresseProperty,
                Kind = CompareRuleKind.TextNormalized,
            };

            if (vejnavn is not null && husnummer is not null)
            {
                keyRule.ExcelColumns = new List<string> { vejnavn, husnummer };
            }
            else if (adresse is not null)
            {
                keyRule.ExcelColumns = new List<string> { adresse };
            }
            else
            {
                string? first = _columns.FirstOrDefault()?.Column;
                keyRule.ExcelColumns = first is null
                    ? new List<string>()
                    : new List<string> { first };
            }

            seeded.Add(keyRule);

            string? forbrug = FindColumnByHeader("varmeforbrug", "forbrug", "energi", "mwh");
            if (forbrug is not null)
            {
                seeded.Add(new CompareRule
                {
                    IsKey = false,
                    ExcelColumns = new List<string> { forbrug },
                    BbrPropertyName = MatchBbrConstants.EstimeretVarmeForbrugProperty,
                    Kind = CompareRuleKind.Numeric,
                    Tolerance = 5.0,
                    ToleranceIsPercent = true,
                });
            }

            return seeded;
        }

        private void ApplyRules(IEnumerable<CompareRule> models)
        {
            Rules.Clear();
            foreach (CompareRule model in models)
            {
                Rules.Add(new CompareRuleViewModel(this, model));
            }

            if (Rules.Count == 0)
            {
                Rules.Add(new CompareRuleViewModel(this, new CompareRule { IsKey = true }));
            }
            else if (!Rules.Any(r => r.IsKey))
            {
                Rules[0].IsKey = true;
            }
        }

        // ---- Rule table callbacks -----------------------------------------

        public ColumnOption? FindColumn(string? column) =>
            string.IsNullOrWhiteSpace(column)
                ? null
                : _columns.FirstOrDefault(x =>
                    string.Equals(x.Column, column, StringComparison.OrdinalIgnoreCase));

        public void MakeKey(CompareRuleViewModel rule)
        {
            foreach (CompareRuleViewModel other in Rules)
            {
                if (!ReferenceEquals(other, rule) && other.IsKey)
                {
                    other.ClearKeyQuiet();
                }
            }
        }

        public void RemoveRule(CompareRuleViewModel rule)
        {
            if (Rules.Count <= 1)
            {
                SetStatus("Der skal være mindst én regel (nøglereglen).", MatchBbrStatusKind.Warning);
                return;
            }

            bool wasKey = rule.IsKey;
            Rules.Remove(rule);

            if (wasKey && Rules.Count > 0)
            {
                Rules[0].IsKey = true;
            }

            OnRuleEdited();
        }

        public void OnRuleEdited()
        {
            if (_suspendReactions || _state is null)
            {
                return;
            }

            _root.NotifyRulesChanged();
            SaveSettings();
        }

        public IEnumerable<CompareRule> BuildRules() => Rules.Select(r => r.ToModel());

        // ---- Filtering -----------------------------------------------------

        partial void OnDistrictColumnChanged(ColumnOption? value)
        {
            if (_state is not null)
            {
                _state.DistrictColumn = value?.Column;
            }
        }

        partial void OnSelectedSheetChanged(string? value)
        {
            if (_suspendReactions || string.IsNullOrWhiteSpace(value) || _state is null)
            {
                return;
            }

            if (!string.Equals(value, _state.SheetName, StringComparison.OrdinalIgnoreCase))
            {
                Load(value);
            }
        }

        partial void OnSkipTypeIngenChanged(bool value)
        {
            if (_state is not null)
            {
                _state.SkipTypeIngen = value;
            }
        }

        // Builds one filter per worksheet column, each with that column's distinct values and
        // their row counts — the dropdown you get from a column header in Excel.
        private void RebuildColumnFilters(IReadOnlyDictionary<string, List<string>>? saved)
        {
            ColumnFilters.Clear();

            if (_state is null)
            {
                return;
            }

            foreach (ColumnOption option in _columns)
            {
                List<(string Value, int Count)> values = Rows
                    .GroupBy(r => r[option.Column].Trim(), StringComparer.OrdinalIgnoreCase)
                    .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(g => (g.Key, g.Count()))
                    .ToList();

                List<string>? checkedValues =
                    saved is not null && saved.TryGetValue(option.Column, out List<string>? stored)
                        ? stored
                        : null;

                ColumnFilters[option.Column] = new ColumnFilterViewModel(
                    option.Column,
                    _state.Sheet.HeaderByColumn.TryGetValue(option.Column, out string? header)
                        ? header
                        : string.Empty,
                    values,
                    checkedValues,
                    OnColumnFilterChanged);
            }
        }

        private void OnColumnFilterChanged()
        {
            if (_suspendReactions)
            {
                return;
            }

            ApplyFilter();
            SaveSettings();
        }

        // Every active column filter must pass — the same AND semantics as Excel's autofilter.
        //
        // The filter drives the row ticks rather than only hiding rows: what the comparison
        // consumes is the ticked set, so a hidden-but-ticked row would be a trap.
        private void ApplyFilter()
        {
            List<(string Column, HashSet<string> Allowed)> active = ColumnFilters.Values
                .Where(f => f.IsActive)
                .Select(f => (f.Column, f.AllowedValues()))
                .ToList();

            foreach (ExcelRowViewModel row in Rows)
            {
                bool passes = true;
                foreach ((string column, HashSet<string> allowed) in active)
                {
                    if (!allowed.Contains(row[column].Trim()))
                    {
                        passes = false;
                        break;
                    }
                }

                row.SetPassesFilters(passes);
            }

            RowsView.Refresh();
            UpdateSummary();
        }

        // Filtered-out rows are hidden, exactly as they would be in Excel.
        private bool FilterRow(object item) => item is ExcelRowViewModel row && row.PassesFilters;

        // Acts on what the filters are showing, the way pasting into a filtered sheet does —
        // ticking "all" while filtered to one district must not silently re-tick the other 4000.
        private void SetAllRows(bool value)
        {
            foreach (ExcelRowViewModel row in Rows.Where(r => r.PassesFilters))
            {
                row.SetCheckedQuiet(value);
            }

            UpdateSummary();
        }

        private void UpdateSummary()
        {
            if (Rows.Count == 0)
            {
                Summary = "Ingen fil indlæst.";
                return;
            }

            int visible = Rows.Count(r => r.PassesFilters);
            int included = Rows.Count(r => r.IsIncluded);

            Summary = visible == Rows.Count
                ? $"{Rows.Count} rækker · {included} valgt"
                : $"{visible} af {Rows.Count} rækker · {included} valgt";
        }

        private void SaveSettings()
        {
            if (_state is null || string.IsNullOrWhiteSpace(WorkbookPath))
            {
                return;
            }

            MatchBbrRuleStore.TrySave(new MatchBbrSettings
            {
                WorkbookPath = WorkbookPath,
                SheetName = SelectedSheet,
                Rules = BuildRules().ToList(),
                DistrictColumn = DistrictColumn?.Column,

                // Only narrowed columns are stored. Saving the fully-checked ones would bloat the
                // file with every distinct value in the sheet for no gain.
                ColumnFilters = ColumnFilters.Values
                    .Where(f => f.IsActive)
                    .ToDictionary(f => f.Column, f => f.CheckedValues().ToList()),
            });
        }

        // ---- Header guessing ----------------------------------------------

        private ColumnOption? GuessDistrictColumn()
        {
            return FindColumn(FindColumnByHeader("varmedistrikt", "distrikt", "område"));
        }

        private string? FindColumnByHeader(params string[] needles)
        {
            if (_state is null)
            {
                return null;
            }

            // Exact header first, then a contains-match, so "Vejnavn" wins over "Vejnavn (kort)".
            foreach (string needle in needles)
            {
                string? exact = _state.Sheet.FindColumnByHeader(header =>
                    string.Equals(header.Trim(), needle, StringComparison.OrdinalIgnoreCase));
                if (exact is not null)
                {
                    return exact;
                }
            }

            foreach (string needle in needles)
            {
                string? partial = _state.Sheet.FindColumnByHeader(header =>
                    header.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0);
                if (partial is not null)
                {
                    return partial;
                }
            }

            return null;
        }
    }
}
