using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FormsDialogResult = System.Windows.Forms.DialogResult;
using FormsSaveFileDialog = System.Windows.Forms.SaveFileDialog;

namespace IntersectUtilities.MPE.MatchBBR.ViewModels
{
    // Tab 2: the comparison itself, plus the three things you do with it — preview it in the
    // drawing, write values back, export the list.
    internal sealed partial class CompareTabViewModel : MatchBbrTabViewModel
    {
        private readonly MatchBbrViewModel _root;
        private MatchBbrState? _state;

        public CompareTabViewModel(MatchBbrViewModel root)
        {
            _root = root;
            RowsView = CollectionViewSource.GetDefaultView(Rows);
            RowsView.Filter = FilterRow;
        }

        // Raised when the rule set changes, so the view can rebuild the per-rule column groups.
        public event Action? RuleColumnsChanged;

        public ObservableCollection<ComparisonRowViewModel> Rows { get; } = new();

        public ICollectionView RowsView { get; }

        // The rules currently producing value columns, in table order.
        public IReadOnlyList<CompareRule> ValueRules { get; private set; } = Array.Empty<CompareRule>();

        [ObservableProperty]
        private ComparisonRowViewModel? _selectedRow;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private bool _showMatch = true;

        [ObservableProperty]
        private bool _showAfvigelse = true;

        [ObservableProperty]
        private bool _showKunIExcel = true;

        [ObservableProperty]
        private bool _showKunITegning = true;

        [ObservableProperty]
        private bool _showDublet = true;

        [ObservableProperty]
        private string _boundaryText = "Ingen afgrænsning valgt";

        [ObservableProperty]
        private string _counts = string.Empty;

        public void Rebind(MatchBbrState state)
        {
            _state = state;
            Rows.Clear();
            Counts = string.Empty;
            BoundaryText = state.HasBoundary
                ? $"{state.BoundaryCount} afgrænsning(er)"
                : "Ingen afgrænsning valgt";
        }

        public void OnRulesChanged()
        {
            ValueRules = _state?.Rules.Where(r => !r.IsKey && r.IsConfigured).ToList()
                         ?? (IReadOnlyList<CompareRule>)Array.Empty<CompareRule>();
            RuleColumnsChanged?.Invoke();
        }

        // ---- Commands ------------------------------------------------------

        [RelayCommand]
        private void PickBoundary()
        {
            if (_state is null)
            {
                return;
            }

            if (_state.PickBoundary())
            {
                BoundaryText = $"{_state.BoundaryCount} afgrænsning(er)";
                ReloadDrawing();
            }
        }

        [RelayCommand]
        private void ReloadDrawing()
        {
            if (_state is null)
            {
                return;
            }

            if (_state.ReloadFromDrawing())
            {
                Compare();
            }
        }

        [RelayCommand]
        private void Compare()
        {
            if (_state is null)
            {
                return;
            }

            if (!_state.Compare())
            {
                Rows.Clear();
                Counts = string.Empty;
                return;
            }

            OnRulesChanged();

            Rows.Clear();
            foreach (ComparisonRow row in _state.Result.Rows)
            {
                Rows.Add(new ComparisonRowViewModel(row, this));
            }

            UpdateCounts();
            RefreshView();
        }

        // One place that both re-filters the grid and keeps a visible preview in step with it.
        private void RefreshView()
        {
            RowsView.Refresh();
            RefreshPreviewIfVisible();
            OnPropertyChanged(nameof(PreviewButtonText));
        }

        // Toggles the transient preview. It draws whatever the grid is currently showing, so
        // filtering to "Afvigelse" and previewing highlights exactly those blocks.
        [RelayCommand]
        private void TogglePreview()
        {
            if (_state is null)
            {
                return;
            }

            if (_state.IsPreviewVisible)
            {
                _state.ClearPreview();
                SetStatus("Forhåndsvisning skjult.", MatchBbrStatusKind.Info);
            }
            else
            {
                _state.ShowPreview(VisibleRows());
            }

            OnPropertyChanged(nameof(PreviewButtonText));
        }

        public string PreviewButtonText =>
            _state?.IsPreviewVisible == true ? "Skjul forhåndsvisning" : "Vis forhåndsvisning";

        // Refreshes an already-visible preview so it keeps matching the grid after a filter,
        // a re-compare or a write-back. Does nothing when the preview is off.
        private void RefreshPreviewIfVisible()
        {
            if (_state?.IsPreviewVisible == true)
            {
                _state.ShowPreview(VisibleRows());
            }
        }

        private List<ComparisonRow> VisibleRows() =>
            RowsView.Cast<ComparisonRowViewModel>().Select(x => x.Row).ToList();

        public void OnPaletteHidden()
        {
            _state?.ClearPreview();
            OnPropertyChanged(nameof(PreviewButtonText));
        }

        [RelayCommand]
        private void WriteAll()
        {
            if (_state is null)
            {
                return;
            }

            int writable = Rows.Count(r => r.IsWritable);
            if (writable == 0)
            {
                SetStatus(
                    "Ingen entydigt matchede rækker at skrive (dubletter springes over).",
                    MatchBbrStatusKind.Warning);
                return;
            }

            // Writing edits survey data on real buildings and is not undone by re-running the
            // comparison, so the count is put in front of the user before anything is written.
            int rules = _state.Rules.Count(r => !r.IsKey && r.IsConfigured);
            MessageBoxResult answer = MessageBox.Show(
                $"Skriv Excel-værdier til {writable} matchede BBR-blok(ke)?\n\n"
                + $"{rules} regel/regler skrives pr. blok. Dubletter springes over.\n"
                + "Handlingen kan fortrydes med UNDO som ét trin.",
                "Skriv til blokke",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning,
                MessageBoxResult.Cancel);

            if (answer != MessageBoxResult.OK)
            {
                SetStatus("Skrivning annulleret.", MatchBbrStatusKind.Info);
                return;
            }

            if (_state.WriteAll())
            {
                RefreshAfterWrite();
            }
        }

        // After a write the drawing holds new values, but BbrRecords is still the snapshot taken
        // before it. Re-comparing against that snapshot would redisplay the old BBR values and
        // leave every row it just fixed sitting on Afvigelse — so re-read the blocks first.
        private void RefreshAfterWrite()
        {
            if (_state is null)
            {
                return;
            }

            // Captured before the reload's own status message overwrites it.
            string writeResult = Status;

            if (_state.ReloadFromDrawing())
            {
                Compare();
            }

            SetStatus(
                $"{writeResult} Sammenligningen er genindlæst fra tegningen.",
                MatchBbrStatusKind.Ok);
        }

        [RelayCommand]
        private void Export()
        {
            if (_state is null || _state.Result.Rows.Count == 0)
            {
                SetStatus("Kør en sammenligning først.", MatchBbrStatusKind.Warning);
                return;
            }

            using FormsSaveFileDialog dialog = new FormsSaveFileDialog
            {
                Title = "Gem sammenligning",
                Filter = "Excel-projektmappe (*.xlsx)|*.xlsx",
                OverwritePrompt = true,
                FileName = SuggestFileName(),
            };

            if (!string.IsNullOrWhiteSpace(_state.WorkbookPath))
            {
                string? directory = Path.GetDirectoryName(_state.WorkbookPath);
                if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                {
                    dialog.InitialDirectory = directory;
                }
            }

            if (dialog.ShowDialog() != FormsDialogResult.OK)
            {
                return;
            }

            // Exports what is on screen, so a filtered view exports the filtered list.
            List<ComparisonRow> visible = VisibleRows();

            _state.Export(dialog.FileName, visible);
        }

        // Writes the circles the preview draws transiently into the drawing as real entities.
        // Like the preview it follows the grid, so a filtered view exports only those circles.
        [RelayCommand]
        private void ExportCircles()
        {
            if (_state is null)
            {
                return;
            }

            List<ComparisonRow> visible = VisibleRows();
            int withBlock = visible.Count(r => r.BbrRecord is not null);

            if (withBlock == 0)
            {
                SetStatus(
                    "Ingen af de viste rækker har en blok at tegne omkring.",
                    MatchBbrStatusKind.Warning);
                return;
            }

            // The count is put in front of the user because this adds geometry to a production
            // drawing, and because a re-export silently replaces the previous set.
            MessageBoxResult answer = MessageBox.Show(
                $"Tegn {withBlock} cirkel/cirkler i tegningen?\n\n"
                + "De lægges på lag efter status (BBR_MATCH, BBR_AFVIGELSE, …).\n"
                + "Cirkler fra en tidligere kørsel erstattes. Kan fortrydes med UNDO som ét trin.",
                "Tegn cirkler",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Question,
                MessageBoxResult.OK);

            if (answer != MessageBoxResult.OK)
            {
                SetStatus("Tegning af cirkler annulleret.", MatchBbrStatusKind.Info);
                return;
            }

            _state.ExportCircles(visible);
        }

        [RelayCommand]
        private void EraseCircles()
        {
            _state?.EraseCircles();
        }

        // Called by the per-cell arrow button in the grid.
        public void WriteSingle(ComparisonRowViewModel row, string ruleId)
        {
            if (_state is null)
            {
                return;
            }

            CompareRule? rule = _state.Rules.FirstOrDefault(r =>
                string.Equals(r.Id, ruleId, StringComparison.Ordinal));
            if (rule is null || rule.IsKey)
            {
                SetStatus("Nøglereglen kan ikke skrives til tegningen.", MatchBbrStatusKind.Warning);
                return;
            }

            if (_state.WriteOne(row.Row, rule))
            {
                RefreshAfterWrite();
            }
        }

        // ---- Row helpers used by ComparisonRowViewModel --------------------

        public string KeyRuleExcelText(ComparisonRow row)
        {
            if (row.ExcelRow is null || _state is null)
            {
                return string.Empty;
            }

            CompareRule? keyRule = _state.Rules.FirstOrDefault(r => r.IsKey && r.IsConfigured);
            return keyRule is null ? string.Empty : keyRule.ReadExcelText(row.ExcelRow.Row);
        }

        public string DistrictText(ComparisonRow row)
        {
            string? column = _state?.DistrictColumn;
            if (!string.IsNullOrWhiteSpace(column) && row.ExcelRow is not null)
            {
                string value = row.ExcelRow.Row[column!];
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return row.BbrRecord?.Distrikt ?? string.Empty;
        }

        // ---- Filtering -----------------------------------------------------

        partial void OnSearchTextChanged(string value) => RefreshView();

        partial void OnShowMatchChanged(bool value) => RefreshView();

        partial void OnShowAfvigelseChanged(bool value) => RefreshView();

        partial void OnShowKunIExcelChanged(bool value) => RefreshView();

        partial void OnShowKunITegningChanged(bool value) => RefreshView();

        partial void OnShowDubletChanged(bool value) => RefreshView();

        partial void OnSelectedRowChanged(ComparisonRowViewModel? value)
        {
            if (value is not null && value.HasBlock)
            {
                _state?.ZoomTo(value.Row);
            }
        }

        private bool FilterRow(object item)
        {
            if (item is not ComparisonRowViewModel row)
            {
                return false;
            }

            bool statusAllowed = row.Status switch
            {
                MatchStatus.Match => ShowMatch,
                MatchStatus.Afvigelse => ShowAfvigelse,
                MatchStatus.KunIExcel => ShowKunIExcel,
                MatchStatus.KunITegning => ShowKunITegning,
                MatchStatus.Dublet => ShowDublet,
                _ => true,
            };

            if (!statusAllowed)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(SearchText))
            {
                return true;
            }

            return row.SearchText.IndexOf(SearchText.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void UpdateCounts()
        {
            if (_state is null)
            {
                Counts = string.Empty;
                return;
            }

            Counts = _state.Result.Summary();
        }

        private string SuggestFileName()
        {
            string stem = string.IsNullOrWhiteSpace(_state?.WorkbookPath)
                ? "BBR"
                : Path.GetFileNameWithoutExtension(_state!.WorkbookPath);
            return $"{stem}_sammenligning.xlsx";
        }
    }
}
