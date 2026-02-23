using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using DimensioneringV2.BBRData.AutoCAD;
using DimensioneringV2.BBRData.Models;
using DimensioneringV2.BBRData.Services;

namespace DimensioneringV2.BBRData.ViewModels
{
    internal partial class BbrDataWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _hasBbrBlocks;

        [ObservableProperty]
        private bool _hasCsvData;

        [ObservableProperty]
        private bool _hasError;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private int _bbrBlockCount;

        [ObservableProperty]
        private int _csvRowCount;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private string _csvFilePath = string.Empty;

        [ObservableProperty]
        private string _selectedDelimiter = ";";

        [ObservableProperty]
        private string _selectedDecimalSeparator = ".";

        [ObservableProperty]
        private bool _canUpdate;

        [ObservableProperty]
        private string _validationMessage = string.Empty;

        [ObservableProperty]
        private MatchResult? _matchResult;

        [ObservableProperty]
        private bool _hideBbrUnmatched;

        [ObservableProperty]
        private bool _hideCsvUnmatched;

        partial void OnHideBbrUnmatchedChanged(bool value) => RebuildDisplayFromCache();
        partial void OnHideCsvUnmatchedChanged(bool value) => RebuildDisplayFromCache();

        // Separate key collections — matched by order (BBR key[0] ↔ CSV key[0])
        public ObservableCollection<BbrMatchKey> BbrKeys { get; } = new();
        public ObservableCollection<CsvMatchKey> CsvKeys { get; } = new();

        // Unified transfer mappings — each pairs a BBR property with a CSV column
        public ObservableCollection<TransferMapping> TransferMappings { get; } = new();

        // Display-only columns (not transferred)
        public ObservableCollection<BbrPropertyDescriptor> DisplayBbrProps { get; } = new();
        public ObservableCollection<CsvKeyPart> DisplayCsvCols { get; } = new();
        public ObservableCollection<DisplayRowViewModel> DisplayRows { get; } = new();

        public IReadOnlyList<BbrPropertyDescriptor> AllBbrProperties => BbrPropertyDescriptor.All;
        public ObservableCollection<string> CsvHeaders { get; } = new();

        private List<BbrRowData> _bbrRows = new();
        private List<CsvRowData> _csvRows = new();
        private string[]? _csvHeadersArray;
        private CsvLoadResult? _csvLoadResult;

        public BbrDataWindowViewModel()
        {
            LoadBbrBlocks();
        }

        partial void OnSelectedDelimiterChanged(string value) => ReloadCsv();
        partial void OnSelectedDecimalSeparatorChanged(string value) => ReloadCsv();

        public event EventHandler? ColumnsChanged;

        #region Commands — CSV file

        [RelayCommand]
        private void LoadCsv()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select CSV file",
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                DefaultExt = ".csv"
            };

            if (dialog.ShowDialog() == true)
            {
                CsvFilePath = dialog.FileName;
                ReloadCsv();
            }
        }

        #endregion

        #region Commands — BBR keys

        [RelayCommand]
        private void AddBbrKey()
        {
            var key = new BbrMatchKey { Order = BbrKeys.Count + 1 };
            BbrKeys.Add(key);
            NotifyBbrKeyChanged();
        }

        [RelayCommand]
        private void RemoveBbrKey(BbrMatchKey? key)
        {
            if (key == null) return;
            BbrKeys.Remove(key);
            for (int i = 0; i < BbrKeys.Count; i++)
                BbrKeys[i].Order = i + 1;
            NotifyBbrKeyChanged();
        }

        #endregion

        #region Commands — CSV keys

        [RelayCommand]
        private void AddCsvKey()
        {
            var key = new CsvMatchKey { Order = CsvKeys.Count + 1 };
            CsvKeys.Add(key);
            NotifyCsvKeyChanged();
        }

        [RelayCommand]
        private void RemoveCsvKey(CsvMatchKey? key)
        {
            if (key == null) return;
            CsvKeys.Remove(key);
            for (int i = 0; i < CsvKeys.Count; i++)
                CsvKeys[i].Order = i + 1;
            NotifyCsvKeyChanged();
        }

        #endregion

        #region Commands — Transfer mappings

        [RelayCommand]
        private void AddTransferMapping(object? parameter)
        {
            if (parameter is not object[] args || args.Length != 2) return;
            var bbrProp = args[0] as BbrPropertyDescriptor;
            var csvCol = args[1] as string;
            if (bbrProp == null || string.IsNullOrEmpty(csvCol)) return;
            if (bbrProp.IsReadOnly) return;
            if (TransferMappings.Any(m => m.BbrProperty == bbrProp || m.CsvColumnName == csvCol))
                return;

            TransferMappings.Add(new TransferMapping(bbrProp, csvCol));
            OnPropertyChanged(nameof(AvailableBbrPropsForTransfer));
            OnPropertyChanged(nameof(AvailableCsvColsForTransfer));
            OnPropertyChanged(nameof(AvailableBbrPropsForDisplay));
            OnPropertyChanged(nameof(AvailableCsvColsForDisplay));
            RebuildDisplayFromCache();
        }

        [RelayCommand]
        private void RemoveTransferMapping(TransferMapping? mapping)
        {
            if (mapping == null) return;
            TransferMappings.Remove(mapping);
            OnPropertyChanged(nameof(AvailableBbrPropsForTransfer));
            OnPropertyChanged(nameof(AvailableCsvColsForTransfer));
            OnPropertyChanged(nameof(AvailableBbrPropsForDisplay));
            OnPropertyChanged(nameof(AvailableCsvColsForDisplay));
            RebuildDisplayFromCache();
        }

        #endregion

        #region Commands — Display props & cols

        [RelayCommand]
        private void AddDisplayBbrProp(BbrPropertyDescriptor? prop)
        {
            if (prop == null) return;
            if (DisplayBbrProps.Contains(prop)) return;
            DisplayBbrProps.Add(prop);
            RebuildDisplayFromCache();
        }

        [RelayCommand]
        private void AddDisplayCsvCol(object? parameter)
        {
            if (parameter is not object[] args || args.Length != 2) return;
            string colName = args[0]?.ToString() ?? string.Empty;
            var dataType = args[1] is BbrDataType dt ? dt : BbrDataType.String;
            if (DisplayCsvCols.Any(c => c.ColumnName == colName)) return;
            DisplayCsvCols.Add(new CsvKeyPart(colName, dataType));
            RebuildDisplayFromCache();
        }

        [RelayCommand]
        private void RemoveDisplayBbrProp(BbrPropertyDescriptor? prop)
        {
            if (prop == null) return;
            DisplayBbrProps.Remove(prop);
            RebuildDisplayFromCache();
        }

        [RelayCommand]
        private void RemoveDisplayCsvCol(CsvKeyPart? col)
        {
            if (col == null) return;
            DisplayCsvCols.Remove(col);
            RebuildDisplayFromCache();
        }

        #endregion

        #region Commands — Update blocks

        [RelayCommand(CanExecute = nameof(CanUpdate))]
        private void UpdateBlocks()
        {
            if (MatchResult == null) return;

            // Step 1: Save ignore states before reload
            var savedIgnoreStates = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            foreach (var group in MatchResult.Groups)
                savedIgnoreStates[group.KeyValue] = group.IsIgnored;

            // Step 2: Write only changed blocks (skip-if-equal)
            var transferableGroups = MatchResult.TransferableGroups;
            var writeResults = BbrBlockWriter.WriteUpdates(
                transferableGroups,
                TransferMappings.ToList(),
                _csvHeadersArray ?? Array.Empty<string>(),
                SelectedDecimalSeparator);

            int totalWritten = writeResults.Count(r => r.Value > 0);
            int totalSkipped = writeResults.Count(r => r.Value == 0);

            // Step 3: Read-back BBR data in fresh transaction
            LoadBbrBlocks();

            // Step 4: Recompute matches with fresh data
            RecomputeMatches();

            // Step 5: Restore ignore states
            if (MatchResult != null)
            {
                foreach (var group in MatchResult.Groups)
                {
                    if (savedIgnoreStates.TryGetValue(group.KeyValue, out bool wasIgnored))
                        group.IsIgnored = wasIgnored;
                }
                // Sync display row ignore states
                foreach (var row in DisplayRows)
                {
                    if (row.MatchGroup != null &&
                        savedIgnoreStates.TryGetValue(row.MatchGroup.KeyValue, out bool wasIgnored))
                        row.IsIgnored = wasIgnored;
                }
            }

            // Step 6: Apply post-transfer colors (green=success, red=failure)
            ApplyPostTransferColors();

            Revalidate();

            MessageBox.Show(
                $"Transfer complete: {totalWritten} block(s) updated, {totalSkipped} skipped (already equal).",
                "Update Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ApplyPostTransferColors()
        {
            foreach (var row in DisplayRows)
            {
                if (row.Category != MatchCategory.OneToOne) continue;

                foreach (var mapping in TransferMappings)
                {
                    if (!row.TransferCells.TryGetValue(mapping.Key, out var cell))
                        continue;

                    bool match = AreValuesEqual(
                        row.MatchGroup?.BbrRows.FirstOrDefault(),
                        row.MatchGroup?.CsvRows.FirstOrDefault(),
                        mapping);

                    cell.CellBackground = match
                        ? TransferCellBrushes.PostTransferSuccess
                        : TransferCellBrushes.PostTransferFailure;
                }
            }
        }

        #endregion

        #region Data loading

        private void LoadBbrBlocks()
        {
            try
            {
                _bbrRows = BbrBlockReader.ReadAll();
                BbrBlockCount = _bbrRows.Count;
                HasBbrBlocks = _bbrRows.Count > 0;
                HasError = !HasBbrBlocks;
                ErrorMessage = HasBbrBlocks ? string.Empty : "No BBR blocks found in the current drawing.";
                StatusMessage = HasBbrBlocks ? $"Loaded {BbrBlockCount} BBR block(s)." : string.Empty;

                OnPropertyChanged(nameof(AvailableBbrPropsForKey));
                OnPropertyChanged(nameof(AvailableBbrPropsForTransfer));
                OnPropertyChanged(nameof(AvailableBbrPropsForDisplay));
            }
            catch (Exception ex)
            {
                HasError = true;
                HasBbrBlocks = false;
                ErrorMessage = $"Error loading BBR blocks: {ex.Message}";
            }
        }

        private void ReloadCsv()
        {
            if (string.IsNullOrEmpty(CsvFilePath)) return;

            string delimiter = SelectedDelimiter == "\\t" ? "\t" : SelectedDelimiter;

            _csvLoadResult = GenericCsvReader.Load(CsvFilePath, delimiter, out string? error);

            if (_csvLoadResult == null || error != null)
            {
                HasCsvData = false;
                ValidationMessage = error ?? "Delimiter error";
                _csvHeadersArray = null;
                _csvRows.Clear();
                CsvHeaders.Clear();
                CsvRowCount = 0;
                RecomputeMatches();
                return;
            }

            _csvHeadersArray = _csvLoadResult.Headers;
            CsvHeaders.Clear();
            foreach (var h in _csvHeadersArray)
                CsvHeaders.Add(h);

            _csvRows.Clear();
            for (int i = 0; i < _csvLoadResult.Rows.Count; i++)
            {
                var row = new CsvRowData(i, _csvLoadResult.Rows[i]);
                for (int c = 0; c < _csvHeadersArray.Length && c < _csvLoadResult.Rows[i].Length; c++)
                {
                    row.TypedValues[_csvHeadersArray[c]] = _csvLoadResult.Rows[i][c];
                }
                _csvRows.Add(row);
            }

            HasCsvData = true;
            CsvRowCount = _csvRows.Count;
            StatusMessage = $"Loaded {BbrBlockCount} BBR block(s), {CsvRowCount} CSV row(s).";

            OnPropertyChanged(nameof(AvailableCsvColsForKey));
            OnPropertyChanged(nameof(AvailableCsvColsForTransfer));
            OnPropertyChanged(nameof(AvailableCsvColsForDisplay));

            RecomputeMatches();
        }

        #endregion

        #region Key change notification

        public void NotifyBbrKeyChanged()
        {
            OnPropertyChanged(nameof(BbrKeys));
            OnPropertyChanged(nameof(AvailableBbrPropsForKey));
            RecomputeMatches();
        }

        public void NotifyCsvKeyChanged()
        {
            OnPropertyChanged(nameof(CsvKeys));
            OnPropertyChanged(nameof(AvailableCsvColsForKey));
            RecomputeMatches();
        }

        #endregion

        #region Matching

        private void RecomputeMatches()
        {
            if (!HasBbrBlocks || !HasCsvData || BbrKeys.Count == 0 || CsvKeys.Count == 0)
            {
                // Show BBR-only preview if we have BBR keys but no CSV keys yet
                if (HasBbrBlocks && BbrKeys.Count > 0 && BbrKeys.Any(k => k.HasParts))
                {
                    MatchResult = null;
                    BuildBbrOnlyRows(BbrKeys.Where(k => k.HasParts).ToList());
                    Revalidate();
                    return;
                }

                MatchResult = null;
                DisplayRows.Clear();
                Revalidate();
                return;
            }

            var readyBbrKeys = BbrKeys.Where(k => k.HasParts).ToList();
            var readyCsvKeys = CsvKeys.Where(k => k.HasParts).ToList();

            if (readyBbrKeys.Count == 0 || readyCsvKeys.Count == 0)
            {
                MatchResult = null;
                if (readyBbrKeys.Count > 0)
                    BuildBbrOnlyRows(readyBbrKeys);
                else
                    DisplayRows.Clear();
                Revalidate();
                return;
            }

            MatchResult = MatchingEngine.ComputeMatches(
                _bbrRows, _csvRows, readyBbrKeys, readyCsvKeys);

            foreach (var group in MatchResult.Groups)
            {
                group.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(MatchGroup.IsIgnored))
                        Revalidate();
                };
            }

            BuildDisplayRows();
            Revalidate();
        }

        private void BuildBbrOnlyRows(IReadOnlyList<BbrMatchKey> bbrKeys)
        {
            DisplayRows.Clear();
            foreach (var bbrRow in _bbrRows)
            {
                var row = new DisplayRowViewModel
                {
                    Category = MatchCategory.BbrUnmatched,
                    KeyValue = string.Join("|", bbrKeys.Select(k => k.ComputeKeyValue(bbrRow))),
                    CanIgnore = false,
                    IsGroupHeader = false
                };

                foreach (var prop in DisplayBbrProps)
                    row.BbrValues[prop.Name] = bbrRow.GetDisplayValue(prop.Name);

                // Populate transfer cells (BBR side only, no comparison)
                foreach (var mapping in TransferMappings)
                {
                    var cell = new TransferCellData
                    {
                        BbrValue = bbrRow.GetDisplayValue(mapping.BbrProperty.Name),
                        CellBackground = TransferCellBrushes.Neutral
                    };
                    row.TransferCells[mapping.Key] = cell;
                }

                DisplayRows.Add(row);
            }
            ColumnsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void BuildDisplayRows()
        {
            DisplayRows.Clear();

            if (MatchResult == null) return;

            foreach (var group in MatchResult.Groups)
            {
                bool isFirst = true;

                if (group.Category == MatchCategory.OneToOne)
                {
                    var row = CreateDisplayRow(group, group.BbrRows[0], group.CsvRows[0], true);
                    DisplayRows.Add(row);
                    continue;
                }

                if (group.Category == MatchCategory.OneToMany || group.Category == MatchCategory.ManyToOne)
                {
                    foreach (var bbrRow in group.BbrRows)
                    {
                        foreach (var csvRow in group.CsvRows)
                        {
                            var row = CreateDisplayRow(group, bbrRow, csvRow, isFirst);
                            isFirst = false;
                            DisplayRows.Add(row);
                        }
                    }
                    continue;
                }

                if (group.Category == MatchCategory.BbrUnmatched)
                {
                    if (HideBbrUnmatched) continue;
                    foreach (var bbrRow in group.BbrRows)
                    {
                        var row = CreateDisplayRow(group, bbrRow, null, true);
                        DisplayRows.Add(row);
                    }
                    continue;
                }

                if (group.Category == MatchCategory.CsvUnmatched)
                {
                    if (HideCsvUnmatched) continue;
                    foreach (var csvRow in group.CsvRows)
                    {
                        var row = CreateDisplayRow(group, null, csvRow, true);
                        DisplayRows.Add(row);
                    }
                    continue;
                }

                if (group.Category == MatchCategory.ManyToMany)
                {
                    var row = new DisplayRowViewModel
                    {
                        Category = MatchCategory.ManyToMany,
                        KeyValue = group.KeyValue,
                        CanIgnore = true,
                        IsGroupHeader = true,
                        MatchGroup = group
                    };
                    row.BbrValues["_info"] = $"Duplicate key: {group.BbrRows.Count} BBR x {group.CsvRows.Count} CSV";
                    DisplayRows.Add(row);
                }
            }

            ColumnsChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Rebuilds display rows from cached data without recomputing matches.
        /// Used when columns change, filter toggles change, or ignore state changes.
        /// </summary>
        private void RebuildDisplayFromCache()
        {
            if (MatchResult != null)
                BuildDisplayRows();
            else if (HasBbrBlocks && BbrKeys.Count > 0 && BbrKeys.Any(k => k.HasParts))
                BuildBbrOnlyRows(BbrKeys.Where(k => k.HasParts).ToList());
            else
                ColumnsChanged?.Invoke(this, EventArgs.Empty);

            Revalidate();
        }

        private DisplayRowViewModel CreateDisplayRow(
            MatchGroup group, BbrRowData? bbrRow, CsvRowData? csvRow, bool isGroupHeader)
        {
            var row = new DisplayRowViewModel
            {
                Category = group.Category,
                KeyValue = group.KeyValue,
                CanIgnore = isGroupHeader,
                IsGroupHeader = isGroupHeader,
                MatchGroup = group
            };

            // Display-only BBR columns
            if (bbrRow != null)
            {
                foreach (var prop in DisplayBbrProps)
                    row.BbrValues[prop.Name] = bbrRow.GetDisplayValue(prop.Name);
            }

            // Display-only CSV columns — normalize to '.' decimals
            if (csvRow != null)
            {
                foreach (var col in DisplayCsvCols)
                    row.CsvValues[col.ColumnName] = FormatCsvValueInvariant(
                        GetCsvRawValue(csvRow, col.ColumnName), col.DataType);
            }

            // Transfer comparison cells
            foreach (var mapping in TransferMappings)
            {
                var cell = new TransferCellData();

                if (bbrRow != null)
                    cell.BbrValue = bbrRow.GetDisplayValue(mapping.BbrProperty.Name);
                if (csvRow != null)
                    cell.CsvValue = FormatCsvValueInvariant(
                        GetCsvRawValue(csvRow, mapping.CsvColumnName), mapping.DataType);

                // Per-cell color: only meaningful for 1:1 groups with both sides present
                if (group.Category == MatchCategory.OneToOne && bbrRow != null && csvRow != null)
                {
                    bool equal = AreValuesEqual(bbrRow, csvRow, mapping);
                    cell.CellBackground = equal
                        ? TransferCellBrushes.PreTransferSame
                        : TransferCellBrushes.PreTransferDifferent;
                }
                else
                {
                    cell.CellBackground = TransferCellBrushes.Neutral;
                }

                row.TransferCells[mapping.Key] = cell;
            }

            return row;
        }

        /// <summary>
        /// Compares a BBR value with a CSV value using typed comparison.
        /// Uses the selected decimal separator to parse CSV, then compares typed objects.
        /// </summary>
        private bool AreValuesEqual(BbrRowData? bbrRow, CsvRowData? csvRow, TransferMapping mapping)
        {
            if (bbrRow == null || csvRow == null) return false;

            bbrRow.Values.TryGetValue(mapping.BbrProperty.Name, out var bbrVal);
            string csvRaw = GetCsvRawValue(csvRow, mapping.CsvColumnName);

            object? csvTyped = GenericCsvReader.ConvertValue(
                csvRaw, mapping.DataType, SelectedDecimalSeparator);

            if (bbrVal == null && csvTyped == null) return true;
            if (bbrVal == null || csvTyped == null) return false;
            return bbrVal.Equals(csvTyped);
        }

        /// <summary>
        /// Extracts raw string value from a CSV row by column name.
        /// </summary>
        private string GetCsvRawValue(CsvRowData csvRow, string columnName)
        {
            if (_csvHeadersArray == null) return string.Empty;
            int colIndex = Array.IndexOf(_csvHeadersArray, columnName);
            if (colIndex >= 0 && colIndex < csvRow.RawFields.Length)
                return csvRow.RawFields[colIndex];
            return string.Empty;
        }

        /// <summary>
        /// Converts a raw CSV value to the target type using the selected decimal separator,
        /// then formats it with InvariantCulture (always '.') for display.
        /// Falls back to the raw string if conversion fails.
        /// </summary>
        private string FormatCsvValueInvariant(string rawValue, BbrDataType dataType)
        {
            if (string.IsNullOrWhiteSpace(rawValue)) return rawValue;

            object? converted = GenericCsvReader.ConvertValue(
                rawValue, dataType, SelectedDecimalSeparator);

            if (converted == null) return rawValue.Trim();

            return converted switch
            {
                double d => d.ToString(CultureInfo.InvariantCulture),
                int i => i.ToString(CultureInfo.InvariantCulture),
                string s => s,
                _ => converted.ToString() ?? rawValue
            };
        }

        #endregion

        #region Validation

        private void Revalidate()
        {
            var (isValid, message) = MatchingEngine.ValidateSetup(
                BbrKeys.ToList(),
                CsvKeys.ToList(),
                TransferMappings.ToList(),
                MatchResult);

            CanUpdate = isValid;
            ValidationMessage = message;
            UpdateBlocksCommand.NotifyCanExecuteChanged();
        }

        #endregion

        #region Available items for dropdowns (exclude already-used items, sort alphabetically)

        public IEnumerable<BbrPropertyDescriptor> AvailableBbrPropsForKey =>
            AllBbrProperties.Where(p =>
                !BbrKeys.SelectMany(k => k.Parts).Contains(p) &&
                !TransferMappings.Any(m => m.BbrProperty == p))
                .OrderBy(p => p.Name);

        public IEnumerable<BbrPropertyDescriptor> AvailableBbrPropsForTransfer =>
            AllBbrProperties.Where(p =>
                !p.IsReadOnly &&
                !BbrKeys.SelectMany(k => k.Parts).Contains(p) &&
                !TransferMappings.Any(m => m.BbrProperty == p) &&
                !DisplayBbrProps.Contains(p))
                .OrderBy(p => p.Name);

        public IEnumerable<BbrPropertyDescriptor> AvailableBbrPropsForDisplay =>
            AllBbrProperties.Where(p =>
                !BbrKeys.SelectMany(k => k.Parts).Contains(p) &&
                !TransferMappings.Any(m => m.BbrProperty == p) &&
                !DisplayBbrProps.Contains(p))
                .OrderBy(p => p.Name);

        public IEnumerable<string> AvailableCsvColsForKey =>
            CsvHeaders.Where(h =>
                !CsvKeys.SelectMany(k => k.Parts).Any(p => p.ColumnName == h))
                .OrderBy(h => h);

        public IEnumerable<string> AvailableCsvColsForTransfer =>
            CsvHeaders.Where(h =>
                !CsvKeys.SelectMany(k => k.Parts).Any(p => p.ColumnName == h) &&
                !TransferMappings.Any(m => m.CsvColumnName == h) &&
                !DisplayCsvCols.Any(c => c.ColumnName == h))
                .OrderBy(h => h);

        public IEnumerable<string> AvailableCsvColsForDisplay =>
            CsvHeaders.Where(h =>
                !CsvKeys.SelectMany(k => k.Parts).Any(p => p.ColumnName == h) &&
                !TransferMappings.Any(m => m.CsvColumnName == h) &&
                !DisplayCsvCols.Any(c => c.ColumnName == h))
                .OrderBy(h => h);

        #endregion
    }

    internal partial class DisplayRowViewModel : ObservableObject
    {
        public MatchCategory Category { get; set; }
        public string KeyValue { get; set; } = string.Empty;
        public Dictionary<string, string> BbrValues { get; } = new();
        public Dictionary<string, string> CsvValues { get; } = new();
        public Dictionary<string, TransferCellData> TransferCells { get; } = new();
        public bool CanIgnore { get; set; }
        public bool IsGroupHeader { get; set; }
        public MatchGroup? MatchGroup { get; set; }

        [ObservableProperty]
        private bool _isIgnored;

        partial void OnIsIgnoredChanged(bool value)
        {
            if (MatchGroup != null)
                MatchGroup.IsIgnored = value;
        }

        public SolidColorBrush RowBackground => Category switch
        {
            MatchCategory.OneToOne => new SolidColorBrush(Color.FromRgb(0x2D, 0x4A, 0x2D)),
            MatchCategory.OneToMany => new SolidColorBrush(Color.FromRgb(0x4A, 0x4A, 0x2D)),
            MatchCategory.ManyToOne => new SolidColorBrush(Color.FromRgb(0x4A, 0x4A, 0x2D)),
            MatchCategory.BbrUnmatched => new SolidColorBrush(Color.FromRgb(0x5A, 0x2D, 0x2D)),
            MatchCategory.CsvUnmatched => new SolidColorBrush(Color.FromRgb(0x5A, 0x2D, 0x2D)),
            MatchCategory.ManyToMany => new SolidColorBrush(Color.FromRgb(0x6A, 0x2D, 0x2D)),
            _ => new SolidColorBrush(Colors.Transparent)
        };

        public string CategoryLabel => Category switch
        {
            MatchCategory.OneToOne => "1:1",
            MatchCategory.OneToMany => "1:N",
            MatchCategory.ManyToOne => "N:1",
            MatchCategory.ManyToMany => "N:N",
            MatchCategory.BbrUnmatched => "BBR x",
            MatchCategory.CsvUnmatched => "CSV x",
            _ => string.Empty
        };
    }
}
