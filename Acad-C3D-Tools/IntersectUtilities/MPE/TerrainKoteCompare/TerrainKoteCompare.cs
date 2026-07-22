using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using IntersectUtilities.MPE.TerrainKoteCompare;
using static IntersectUtilities.UtilsCommon.Utils;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;
using FormsOpenFileDialog = System.Windows.Forms.OpenFileDialog;
using FormsSaveFileDialog = System.Windows.Forms.SaveFileDialog;
using WinFormsDialogResult = System.Windows.Forms.DialogResult;
using WinLabel = System.Windows.Controls.TextBlock;

namespace IntersectUtilities
{
    public partial class Intersect
    {
        private const string TerrainKoteCompareCommandName = "TERRAINKOTECOMPARE";

        /// <command>TERRAINKOTECOMPARE</command>
        /// <summary>
        /// Opens an MPE palette for comparing surveyed terrain kote points ("terrænkote" blocks) against one or more
        /// TIN terrain models loaded from external DWG files. Every TinSurface found in every loaded file is active;
        /// where several surfaces cover the same point the one whose elevation is closest to the surveyed Z wins.
        /// Each point is projected in Z onto the terrain and the signed difference is reported, positive when the
        /// surveyed point sits above the model. Results can be previewed as transient markers, numbered and labelled
        /// in the drawing, and exported to Excel.
        /// </summary>
        /// <category>MPE</category>
        [CommandMethod(TerrainKoteCompareCommandName, CommandFlags.Modal)]
        public void terrainkotecompare()
        {
            DocumentCollection docCol = AcadApp.DocumentManager;
            Document doc = docCol.MdiActiveDocument;
            Editor editor = doc.Editor;
            try
            {
                TerrainKoteComparePaletteHost.Show(doc);
                editor.WriteMessage($"\n{TerrainKoteCompareCommandName} opened the palette.");
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                editor.WriteMessage($"\n{TerrainKoteCompareCommandName} failed. See debug output for details.");
                return;
            }
        }
    }
}

namespace IntersectUtilities.MPE.TerrainKoteCompare
{
    internal static class TerrainKoteComparePaletteHost
    {
        private static PaletteSet? _palette;
        private static TerrainKoteCompareControl? _control;

        private static readonly System.Drawing.Size DefaultPaletteSize = new System.Drawing.Size(1000, 1000);

        public static void Show(Document document)
        {
            bool justCreated = _palette == null;

            if (_palette == null)
            {
                _control = new TerrainKoteCompareControl();
                // AutoCAD persists a palette's geometry per profile keyed by this GUID. Roll the
                // GUID whenever the default size changes, otherwise the saved geometry wins and the
                // new default is never seen.
                _palette = new PaletteSet(
                    "Compare Terrain Kote",
                    "TERRAIN_KOTE_COMPARE",
                    new Guid("3F6A9C42-81D7-4B0E-95CA-E7248B15D9A0"))
                {
                    Style = PaletteSetStyles.ShowPropertiesMenu | PaletteSetStyles.ShowCloseButton,
                    MinimumSize = new System.Drawing.Size(380, 300)
                };

                _palette.AddVisual("Compare", _control);
                _palette.DockEnabled = DockSides.Left | DockSides.Right | DockSides.None;
                _palette.StateChanged += (_, _) =>
                {
                    if (_palette != null && !_palette.Visible)
                    {
                        _control?.ClearPreview();
                    }
                };
            }

            _control!.AttachDocument(document);
            _palette.Visible = true;

            // Applied only on first show; see PaletteSizing for why Size in the initializer alone
            // does not stick.
            if (justCreated)
            {
                IntersectUtilities.MPE.Shared.PaletteSizing.ApplyDefault(_palette, DefaultPaletteSize);
            }
        }

        // Disposes the cached palette AND every retained terrain side-database on plugin unload
        // (called from IExtensionApplication.Terminate) so nothing survives an unload/reload cycle.
        public static void Reset()
        {
            _control?.DisposeSurfaces();

            if (_palette != null)
            {
                try
                {
                    _palette.Visible = false;
                    _palette.Dispose();
                }
                catch { }
                _palette = null;
            }

            _control = null;
        }
    }

    internal sealed class TerrainKoteCompareControl : UserControl
    {
        // Deeper page background than the section cards, so the stepped cards read as raised panels.
        private static readonly Brush PageBackgroundBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(26, 32, 44));
        private static readonly Brush PanelBackgroundBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(41, 50, 66));
        private static readonly Brush CardBackgroundBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(41, 50, 66));
        private static readonly Brush StatBackgroundBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(31, 39, 52));
        private static readonly Brush InputBackgroundBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(49, 59, 79));
        private static readonly Brush ButtonBackgroundBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(85, 98, 122));
        private static readonly Brush AccentButtonBackgroundBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(95, 166, 220));
        private static readonly Brush ButtonHoverBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(98, 112, 138));
        private static readonly Brush ButtonPressedBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(70, 82, 104));
        private static readonly Brush AccentButtonHoverBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(109, 181, 235));
        private static readonly Brush AccentButtonPressedBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(74, 145, 198));
        private static readonly Brush BorderBrushValue = new SolidColorBrush(System.Windows.Media.Color.FromRgb(74, 88, 112));
        private static readonly Brush ForegroundBrushValue = new SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 245, 252));
        // Secondary text: file paths, hints, subtitles. Clearly dimmer than primary content.
        private static readonly Brush MutedForegroundBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(148, 161, 183));
        // Step badges on section headers.
        private static readonly Brush StepBadgeBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(95, 166, 220));

        private const string DefaultPointLayerName = "Terrænkote_punkt";

        // Fixed presentation/numbering constants. These were briefly exposed as palette inputs but
        // are not things a user needs to tune per run: the row height only has to be coarse enough
        // to band a survey grid into reading order, and the marker/text sizes suit the 1:250-ish
        // plans this tool is used on.
        private const double RowHeight = 5.0;
        private const double MarkerSize = 0.5;
        private const double TextHeight = 0.5;

        private readonly TerrainKoteCompareTransientRenderer _renderer = new TerrainKoteCompareTransientRenderer();
        private readonly TerrainKoteCompareSurfaceSet _surfaceSet = new TerrainKoteCompareSurfaceSet();

        private readonly StackPanel _fileListPanel;
        private readonly ComboBox _pointLayerComboBox;
        private readonly WinLabel _pointsSummaryText;
        private readonly WinLabel _terrainSummaryText;
        private readonly WinLabel _resultDetailText;
        private readonly TextBox _statusTextBox;

        // Assigned in BuildReviewFilters (invoked from the constructor), so not readonly.
        private Button _switchKoteButton = null!;
        private Button _showHideButton = null!;
        private readonly Dictionary<TerrainKoteCompareClassification, WinLabel> _countTextByClass =
            new Dictionary<TerrainKoteCompareClassification, WinLabel>();

        private Document? _document;
        private List<ObjectId> _selectedPointIds = new List<ObjectId>();
        private TerrainKoteCompareResult? _lastResult;
        private TerrainKoteCompareValueMode _valueMode = TerrainKoteCompareValueMode.Difference;
        private bool _previewVisible;

        private readonly HashSet<TerrainKoteCompareClassification> _visibleClassifications =
            new HashSet<TerrainKoteCompareClassification>
            {
                TerrainKoteCompareClassification.Above,
                TerrainKoteCompareClassification.Below,
                TerrainKoteCompareClassification.OutsideSurface,
                TerrainKoteCompareClassification.NoHeight
            };

        public TerrainKoteCompareControl()
        {
            Background = PageBackgroundBrush;

            StackPanel root = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(14),
                Background = Background
            };

            // ---- 1 · Terrain sources ------------------------------------------------------------
            Button chooseTerrainButton = CreateButton("Choose Terrain", isAccent: true);
            chooseTerrainButton.Click += (_, _) => AddSurfaceFiles();
            Button clearFilesButton = CreateButton("Clear all");
            clearFilesButton.Click += (_, _) => ClearSurfaceFiles();

            _fileListPanel = new StackPanel { Orientation = Orientation.Vertical };

            StackPanel terrainBody = new StackPanel { Orientation = Orientation.Vertical };
            terrainBody.Children.Add(_fileListPanel);
            _terrainSummaryText = CreateMutedText("No terrain files loaded yet.");
            _terrainSummaryText.Margin = new Thickness(2, 8, 0, 0);
            terrainBody.Children.Add(_terrainSummaryText);

            root.Children.Add(CreateSection(
                "1", "Terrain sources", terrainBody,
                headerActions: new[] { chooseTerrainButton, clearFilesButton }));

            // ---- 2 · Point layer ----------------------------------------------------------------
            _pointLayerComboBox = new ComboBox
            {
                Margin = new Thickness(0, 0, 8, 0),
                IsEditable = false,
                Background = InputBackgroundBrush,
                Foreground = ForegroundBrushValue,
                BorderBrush = BorderBrushValue,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 6, 10, 6),
                Style = CreateComboBoxStyle(),
                MinWidth = 240,
                MinHeight = 32,
                VerticalAlignment = VerticalAlignment.Center
            };
            _pointLayerComboBox.DropDownOpened += (_, _) => RefreshLayerList();

            Button loadAllButton = CreateButton("Load all", isAccent: true);
            loadAllButton.Click += (_, _) => LoadAllPoints();
            Button selectButton = CreateButton("Select from drawing");
            selectButton.Click += (_, _) => SelectPoints();

            WrapPanel pointRow = new WrapPanel { Orientation = Orientation.Horizontal };
            pointRow.Children.Add(_pointLayerComboBox);
            pointRow.Children.Add(loadAllButton);
            pointRow.Children.Add(selectButton);

            StackPanel pointBody = new StackPanel { Orientation = Orientation.Vertical };
            pointBody.Children.Add(pointRow);
            _pointsSummaryText = CreateMutedText("No points loaded.");
            _pointsSummaryText.Margin = new Thickness(2, 8, 0, 0);
            pointBody.Children.Add(_pointsSummaryText);

            root.Children.Add(CreateSection("2", "Point layer", pointBody));

            // ---- 3 · Compute --------------------------------------------------------------------
            Button computeButton = CreateButton("Compute terrain comparison", isAccent: true);
            computeButton.MinHeight = 42;
            computeButton.FontWeight = FontWeights.Bold;
            computeButton.HorizontalAlignment = HorizontalAlignment.Stretch;
            computeButton.Margin = new Thickness(0);
            computeButton.Click += (_, _) => Compute();
            root.Children.Add(CreateSection("3", "Compute", computeButton));

            // ---- 4 · Results (the visual centre) ------------------------------------------------
            StackPanel resultsBody = new StackPanel { Orientation = Orientation.Vertical };
            resultsBody.Children.Add(BuildResultCounters());
            _resultDetailText = CreateMutedText("Run Compute to see results.");
            _resultDetailText.Margin = new Thickness(2, 10, 0, 0);
            resultsBody.Children.Add(_resultDetailText);
            root.Children.Add(CreateSection("4", "Results", resultsBody));

            // ---- 5 · Review filters -------------------------------------------------------------
            root.Children.Add(CreateSection("5", "Review filters", BuildReviewFilters()));

            // ---- 6 · Labels & export ------------------------------------------------------------
            Button createLabelsButton = CreateButton("Create Labels");
            createLabelsButton.Click += (_, _) => CreateLabels();
            Button exportButton = CreateButton("Export to Excel");
            exportButton.Click += (_, _) => ExportToExcel();
            WrapPanel actionRow = new WrapPanel { Orientation = Orientation.Horizontal };
            actionRow.Children.Add(createLabelsButton);
            actionRow.Children.Add(exportButton);
            root.Children.Add(CreateSection("6", "Labels & export", actionRow));

            // ---- Status line --------------------------------------------------------------------
            _statusTextBox = new TextBox
            {
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                MinHeight = 46,
                MaxHeight = 96,
                Margin = new Thickness(0, 2, 0, 0),
                Background = StatBackgroundBrush,
                Foreground = MutedForegroundBrush,
                BorderBrush = BorderBrushValue,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 6, 10, 6)
            };
            root.Children.Add(_statusTextBox);

            // The palette often docks shorter than the content; without this the bottom controls are
            // simply unreachable.
            Content = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Background = PageBackgroundBrush,
                Padding = new Thickness(0, 0, 4, 0),
                Content = root
            };

            UpdateSwitchKoteCaption();
            UpdateShowHideCaption();
            RefreshFileList();
            UpdateResultCounts();
            UpdateStatus("Step 1: choose one or more terrain DWG files.");
        }

        public void AttachDocument(Document document)
        {
            _document = document;
            RefreshLayerList();
            RefreshFileList();
            UpdatePointsSummary();
            UpdateResultCounts();
        }

        // Fills the layer dropdown from the drawing's layer table, keeping whatever was already
        // selected. On first fill it prefers the conventional survey layer if the drawing has it.
        private void RefreshLayerList()
        {
            if (_document == null)
            {
                return;
            }

            try
            {
                string? previouslySelected = _pointLayerComboBox.SelectedItem as string;

                List<string> layerNames = new List<string>();
                using (DocumentLock documentLock = _document.LockDocument())
                using (Transaction tx = _document.Database.TransactionManager.StartTransaction())
                {
                    LayerTable layerTable = (LayerTable)tx.GetObject(_document.Database.LayerTableId, OpenMode.ForRead);
                    foreach (ObjectId layerId in layerTable)
                    {
                        if (tx.GetObject(layerId, OpenMode.ForRead) is LayerTableRecord layer)
                        {
                            layerNames.Add(layer.Name);
                        }
                    }

                    tx.Commit();
                }

                layerNames.Sort(StringComparer.OrdinalIgnoreCase);

                _pointLayerComboBox.Items.Clear();
                foreach (string layerName in layerNames)
                {
                    _pointLayerComboBox.Items.Add(layerName);
                }

                string? target = previouslySelected ?? layerNames.FirstOrDefault(
                    name => string.Equals(name, DefaultPointLayerName, StringComparison.OrdinalIgnoreCase));

                int index = target == null
                    ? -1
                    : layerNames.FindIndex(name => string.Equals(name, target, StringComparison.OrdinalIgnoreCase));

                _pointLayerComboBox.SelectedIndex = index >= 0 ? index : (layerNames.Count > 0 ? 0 : -1);
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                UpdateStatus("Reading the drawing's layers failed. See debug output for details.");
            }
        }

        public void DisposeSurfaces()
        {
            _renderer.Clear();
            _surfaceSet.Dispose();
        }

        #region Surface files

        private void AddSurfaceFiles()
        {
            using FormsOpenFileDialog dialog = new FormsOpenFileDialog
            {
                Title = "Select Terrain DWG file(s)",
                Filter = "Drawing Files (*.dwg)|*.dwg|All Files (*.*)|*.*",
                CheckFileExists = true,
                CheckPathExists = true,
                Multiselect = true
            };

            if (dialog.ShowDialog() != WinFormsDialogResult.OK)
            {
                return;
            }

            List<string> messages = new List<string>();
            foreach (string path in dialog.FileNames)
            {
                if (_surfaceSet.Contains(path))
                {
                    messages.Add($"{Path.GetFileName(path)}: already loaded.");
                    continue;
                }

                try
                {
                    int surfaceCount = _surfaceSet.AddFile(path);
                    messages.Add(surfaceCount == 0
                        ? $"{Path.GetFileName(path)}: no TIN surfaces found - not loaded."
                        : $"{Path.GetFileName(path)}: {surfaceCount} TIN surface(s) loaded.");
                }
                catch (System.Exception ex)
                {
                    prdDbg(ex);
                    messages.Add($"{Path.GetFileName(path)}: failed to read. See debug output.");
                }
            }

            InvalidateResult();
            RefreshFileList();
            UpdateStatus(string.Join(Environment.NewLine, messages));
        }

        private void RemoveSurfaceFile(string path)
        {
            _surfaceSet.RemoveFile(path);
            InvalidateResult();
            RefreshFileList();
            UpdateStatus($"Removed {Path.GetFileName(path)}.");
        }

        private void ClearSurfaceFiles()
        {
            _surfaceSet.Clear();
            InvalidateResult();
            RefreshFileList();
            UpdateStatus("All terrain files unloaded.");
        }

        // Rebuilds the terrain-file list as one card per file (filename, surface count, status,
        // remove button, full path on hover). Replaces the old raw-path ListBox.
        private void RefreshFileList()
        {
            _fileListPanel.Children.Clear();

            IReadOnlyList<TerrainKoteCompareFileInfo> infos = _surfaceSet.FileInfos();
            if (infos.Count == 0)
            {
                _fileListPanel.Children.Add(CreateMutedText(
                    "No terrain files loaded. Click Choose Terrain to add one or more DWG files."));
                _terrainSummaryText.Text = "No terrain files loaded yet.";
                return;
            }

            foreach (TerrainKoteCompareFileInfo info in infos)
            {
                _fileListPanel.Children.Add(CreateFileRow(info));
            }

            _terrainSummaryText.Text =
                $"{_surfaceSet.Surfaces.Count} TIN surface(s) across {infos.Count} file(s).";
        }

        private UIElement CreateFileRow(TerrainKoteCompareFileInfo info)
        {
            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel text = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(new WinLabel
            {
                Text = info.FileName,
                Foreground = ForegroundBrushValue,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            text.Children.Add(new WinLabel
            {
                Text = $"{info.SurfaceCount} TIN surface(s)  ·  loaded",
                Foreground = MutedForegroundBrush,
                FontSize = 11,
                Margin = new Thickness(0, 2, 0, 0)
            });
            Grid.SetColumn(text, 0);
            grid.Children.Add(text);

            Button remove = CreateButton("✕");
            remove.MinHeight = 26;
            remove.MinWidth = 30;
            remove.Padding = new Thickness(8, 2, 8, 2);
            remove.Margin = new Thickness(8, 0, 0, 0);
            remove.VerticalAlignment = VerticalAlignment.Center;
            remove.ToolTip = "Remove this terrain file";
            remove.Click += (_, _) => RemoveSurfaceFile(info.FilePath);
            Grid.SetColumn(remove, 1);
            grid.Children.Add(remove);

            return new Border
            {
                Background = StatBackgroundBrush,
                BorderBrush = BorderBrushValue,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10, 7, 8, 7),
                Margin = new Thickness(0, 0, 0, 6),
                ToolTip = info.FilePath,
                Child = grid
            };
        }

        #endregion

        #region Point selection

        private void LoadAllPoints()
        {
            if (_document == null)
            {
                UpdateStatus("No active document is attached to the palette.");
                return;
            }

            string? layerName = _pointLayerComboBox.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(layerName))
            {
                UpdateStatus("Select the layer that carries the terrain kote blocks.");
                return;
            }

            try
            {
                using DocumentLock documentLock = _document.LockDocument();
                using Transaction tx = _document.Database.TransactionManager.StartTransaction();

                BlockTable blockTable = (BlockTable)tx.GetObject(_document.Database.BlockTableId, OpenMode.ForRead);
                BlockTableRecord modelSpace = (BlockTableRecord)tx.GetObject(
                    blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                List<ObjectId> pointIds = new List<ObjectId>();
                foreach (ObjectId objectId in modelSpace)
                {
                    if (tx.GetObject(objectId, OpenMode.ForRead, false) is not BlockReference blockReference)
                    {
                        continue;
                    }

                    if (!string.Equals(blockReference.Layer, layerName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    pointIds.Add(objectId);
                }

                tx.Commit();
                SetSelection(pointIds);
                UpdateStatus($"Loaded {pointIds.Count} terrain kote block(s) from layer \"{layerName}\".");
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                UpdateStatus("Loading terrain kote points failed. See debug output for details.");
            }
        }

        private void SelectPoints()
        {
            if (_document == null)
            {
                UpdateStatus("No active document is attached to the palette.");
                return;
            }

            try
            {
                Editor editor = _document.Editor;
                SelectionFilter filter = new SelectionFilter(
                    new[]
                    {
                        new TypedValue((int)DxfCode.Start, "INSERT")
                    });
                PromptSelectionOptions options = new PromptSelectionOptions
                {
                    MessageForAdding = "\nSelect terrain kote blocks to compare against the terrain: "
                };

                PromptSelectionResult selectionResult = editor.GetSelection(options, filter);
                if (selectionResult.Status != PromptStatus.OK || selectionResult.Value == null || selectionResult.Value.Count == 0)
                {
                    UpdateStatus("Selection cancelled or empty.");
                    return;
                }

                using DocumentLock documentLock = _document.LockDocument();
                using Transaction tx = _document.Database.TransactionManager.StartTransaction();

                List<ObjectId> pointIds = new List<ObjectId>();
                foreach (ObjectId objectId in selectionResult.Value.GetObjectIds())
                {
                    if (tx.GetObject(objectId, OpenMode.ForRead, false) is BlockReference)
                    {
                        pointIds.Add(objectId);
                    }
                }

                tx.Commit();

                if (pointIds.Count == 0)
                {
                    UpdateStatus("Selection did not contain any block references.");
                    return;
                }

                SetSelection(pointIds);
                UpdateStatus($"Loaded {pointIds.Count} selected block(s).");
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                UpdateStatus("Selection failed. See debug output for details.");
            }
        }

        private void SetSelection(IReadOnlyList<ObjectId> pointIds)
        {
            _selectedPointIds = pointIds
                .Where(id => id.IsValid && !id.IsErased)
                .Distinct()
                .ToList();
            InvalidateResult();
            UpdatePointsSummary();
        }

        // The KOTE attribute is a field displaying the block's own insertion-point Z, so Position.Z
        // is the authoritative height. The attribute is only consulted as a fallback for surveys
        // delivered as flat blocks with the elevation typed into the attribute instead.
        private List<TerrainKoteComparePoint> CollectPoints(Transaction tx)
        {
            List<TerrainKoteComparePoint> points = new List<TerrainKoteComparePoint>(_selectedPointIds.Count);

            foreach (ObjectId objectId in _selectedPointIds)
            {
                if (objectId.IsErased) continue;
                if (tx.GetObject(objectId, OpenMode.ForRead, false) is not BlockReference blockReference) continue;

                double? elevation = Math.Abs(blockReference.Position.Z) > 1e-9
                    ? blockReference.Position.Z
                    : TryReadElevationFromAttributes(blockReference, tx);

                points.Add(new TerrainKoteComparePoint(
                    objectId,
                    blockReference.Handle.ToString(),
                    blockReference.Position,
                    elevation));
            }

            return points;
        }

        private static double? TryReadElevationFromAttributes(BlockReference blockReference, Transaction tx)
        {
            foreach (ObjectId attributeId in blockReference.AttributeCollection)
            {
                if (tx.GetObject(attributeId, OpenMode.ForRead, false) is not AttributeReference attribute) continue;

                string text = attribute.TextString?.Trim() ?? string.Empty;
                if (text.Length == 0) continue;

                if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double invariantValue))
                {
                    return invariantValue;
                }

                if (double.TryParse(text, NumberStyles.Float, new CultureInfo("da-DK"), out double danishValue))
                {
                    return danishValue;
                }
            }

            return null;
        }

        #endregion

        #region Compute / preview

        private void Compute()
        {
            if (_document == null)
            {
                UpdateStatus("No active document is attached to the palette.");
                return;
            }

            if (_surfaceSet.Surfaces.Count == 0)
            {
                UpdateStatus("Add at least one terrain DWG containing a TIN surface first.");
                return;
            }

            if (_selectedPointIds.Count == 0)
            {
                UpdateStatus("No terrain kote points are loaded. Use Load all or Select first.");
                return;
            }

            try
            {
                using DocumentLock documentLock = _document.LockDocument();
                using Transaction tx = _document.Database.TransactionManager.StartTransaction();

                List<TerrainKoteComparePoint> points = CollectPoints(tx);
                tx.Commit();

                if (points.Count == 0)
                {
                    UpdateStatus("None of the loaded objects could be read as block references.");
                    return;
                }

                TerrainKoteCompareResult result = TerrainKoteCompareAnalyzer.Analyze(
                    points,
                    _surfaceSet,
                    RowHeight);

                _lastResult = result;
                UpdatePointsSummary();
                UpdateResultCounts();
                // Computing without seeing the outcome is never what you want, so the preview is
                // shown straight away (flipping the toggle to Hide).
                ShowPreview();
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                UpdateStatus("Compute failed. See debug output for details.");
            }
        }

        // The single Show/Hide toggle. Shows the preview (computing first if needed) when hidden,
        // clears it when shown.
        private void TogglePreview()
        {
            if (_previewVisible)
            {
                HidePreview();
                return;
            }

            if (!TryEnsureResult())
            {
                return;
            }

            ShowPreview();
        }

        private void ShowPreview()
        {
            RenderFilteredPreview();
            _previewVisible = true;
            UpdateShowHideCaption();
            if (_lastResult != null)
            {
                UpdateStatus(BuildStatusWithPreviewCount(_lastResult));
            }
        }

        private void HidePreview()
        {
            _renderer.Clear();
            _previewVisible = false;
            UpdateShowHideCaption();
        }

        private void UpdateShowHideCaption()
        {
            _showHideButton.Content = _previewVisible ? "Hide" : "Show";
        }

        private string BuildStatusWithPreviewCount(TerrainKoteCompareResult result)
        {
            int visible = result.Points.Count(point => _visibleClassifications.Contains(point.Classification));
            return $"{BuildResultStatus(result)}{Environment.NewLine}{Environment.NewLine}"
                + $"Previewing {visible} point(s), showing {DescribeValueMode()}.";
        }

        // Called when the palette is hidden; drops the transient preview without changing the result.
        public void ClearPreview()
        {
            HidePreview();
        }

        // Flips the number shown next to each point between the difference and the projected
        // terrain kote. Drives Create Labels too, so the labels always say what the preview said.
        private void SwitchKote()
        {
            _valueMode = _valueMode == TerrainKoteCompareValueMode.Difference
                ? TerrainKoteCompareValueMode.TerrainElevation
                : TerrainKoteCompareValueMode.Difference;

            UpdateSwitchKoteCaption();

            // Only redraw if the preview is currently up; flipping the mode while hidden just
            // changes what the next Show / Create Labels will use.
            if (_previewVisible)
            {
                RenderFilteredPreview();
            }

            if (_lastResult != null)
            {
                UpdateStatus(BuildStatusWithPreviewCount(_lastResult));
            }
        }

        private void UpdateSwitchKoteCaption()
        {
            _switchKoteButton.Content = $"Switch kote ({DescribeValueMode()})";
        }

        private string DescribeValueMode()
        {
            return _valueMode == TerrainKoteCompareValueMode.TerrainElevation
                ? "terrain kote"
                : "difference";
        }

        private void RenderFilteredPreview()
        {
            if (_lastResult == null)
            {
                _renderer.Clear();
                return;
            }

            List<TerrainKoteCompareResultPoint> visiblePoints = _lastResult.Points
                .Where(point => _visibleClassifications.Contains(point.Classification))
                .ToList();

            _renderer.Show(visiblePoints, MarkerSize, TextHeight, _valueMode);
        }

        private bool TryEnsureResult()
        {
            if (_lastResult != null && _lastResult.Points.Count > 0)
            {
                return true;
            }

            Compute();
            return _lastResult != null && _lastResult.Points.Count > 0;
        }

        private void InvalidateResult()
        {
            _lastResult = null;
            _renderer.Clear();
            _previewVisible = false;
            UpdateShowHideCaption();
            UpdateResultCounts();
        }

        #endregion

        #region Labels / export

        private void CreateLabels()
        {
            if (_document == null)
            {
                UpdateStatus("No active document is attached to the palette.");
                return;
            }

            if (!TryEnsureResult())
            {
                UpdateStatus("Nothing to label. Run Compute first.");
                return;
            }

            if (_visibleClassifications.Count == 0)
            {
                UpdateStatus("No classifications are toggled. Check at least one legend entry.");
                return;
            }

            try
            {
                int created = TerrainKoteCompareLabelBaker.CreateLabels(
                    _document,
                    _lastResult!,
                    _visibleClassifications,
                    TextHeight,
                    MarkerSize,
                    _valueMode);

                _document.Editor.Regen();
                UpdateStatus($"Created {created} label(s) showing {DescribeValueMode()} on the terrain kote result layers.");
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                UpdateStatus("Creating labels failed. See debug output for details.");
            }
        }

        private void ExportToExcel()
        {
            if (!TryEnsureResult())
            {
                UpdateStatus("Nothing to export. Run Compute first.");
                return;
            }

            if (_visibleClassifications.Count == 0)
            {
                UpdateStatus("No classifications are toggled. Check at least one legend entry.");
                return;
            }

            string? outputPath = PromptForSavePath();
            if (outputPath == null)
            {
                return;
            }

            try
            {
                int exported = TerrainKoteCompareExcelExport.Export(outputPath, _lastResult!, _visibleClassifications);
                UpdateStatus($"Exported {exported} row(s) to{Environment.NewLine}{outputPath}");
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                UpdateStatus("Excel export failed. See debug output for details.");
            }
        }

        private string? PromptForSavePath()
        {
            string initialDirectory = _document != null && !string.IsNullOrWhiteSpace(_document.Name)
                ? Path.GetDirectoryName(_document.Name) ?? Environment.CurrentDirectory
                : Environment.CurrentDirectory;

            using FormsSaveFileDialog dialog = new FormsSaveFileDialog
            {
                Title = "Save Terrain Kote Comparison",
                Filter = "Excel Files (*.xlsx)|*.xlsx|All Files (*.*)|*.*",
                InitialDirectory = initialDirectory,
                FileName = "terraenkote_sammenligning.xlsx",
                AddExtension = true,
                DefaultExt = "xlsx",
                OverwritePrompt = true
            };

            if (dialog.ShowDialog() != WinFormsDialogResult.OK || string.IsNullOrWhiteSpace(dialog.FileName))
            {
                return null;
            }

            return dialog.FileName;
        }

        #endregion

        #region Status / UI helpers

        private string BuildResultStatus(TerrainKoteCompareResult result)
        {
            string status =
                $"Surfaces: {result.SurfaceCount} in {result.FileCount} file(s){Environment.NewLine}"
                + $"Points analyzed: {result.Points.Count}{Environment.NewLine}"
                + $"Point above terrain: {result.CountOf(TerrainKoteCompareClassification.Above)}{Environment.NewLine}"
                + $"Point below terrain: {result.CountOf(TerrainKoteCompareClassification.Below)}{Environment.NewLine}"
                + $"Outside surface: {result.CountOf(TerrainKoteCompareClassification.OutsideSurface)}{Environment.NewLine}"
                + $"No kote: {result.CountOf(TerrainKoteCompareClassification.NoHeight)}";

            List<TerrainKoteCompareResultPoint> compared = result.Points
                .Where(point => point.Difference.HasValue)
                .ToList();

            if (compared.Count > 0)
            {
                double meanAbsolute = compared.Average(point => Math.Abs(point.Difference!.Value));
                double minimum = compared.Min(point => point.Difference!.Value);
                double maximum = compared.Max(point => point.Difference!.Value);
                status += Environment.NewLine
                    + $"Difference min/max: {minimum.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture)}"
                    + $" / {maximum.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture)} m"
                    + Environment.NewLine
                    + $"Mean absolute difference: {meanAbsolute.ToString("0.00", CultureInfo.InvariantCulture)} m";
            }

            if (result.MultiCoverageCount > 0)
            {
                status += Environment.NewLine
                    + $"Points covered by more than one surface: {result.MultiCoverageCount} (closest elevation used)";
            }

            return status;
        }

        private void UpdateStatus(string message)
        {
            _statusTextBox.Text = message;
        }

        private void UpdatePointsSummary()
        {
            _pointsSummaryText.Text = _selectedPointIds.Count == 0
                ? "No points loaded — use Load all or Select from drawing."
                : $"{_selectedPointIds.Count} point(s) loaded.";
        }

        // Fills the four big result counters and the detail line beneath them.
        private void UpdateResultCounts()
        {
            foreach (KeyValuePair<TerrainKoteCompareClassification, WinLabel> entry in _countTextByClass)
            {
                entry.Value.Text = _lastResult == null
                    ? "0"
                    : _lastResult.CountOf(entry.Key).ToString(CultureInfo.InvariantCulture);
            }

            _resultDetailText.Text = _lastResult == null
                ? "Run Compute to see results."
                : BuildResultDetail(_lastResult);
        }

        private static string BuildResultDetail(TerrainKoteCompareResult result)
        {
            string detail =
                $"{result.Points.Count} points  ·  {result.SurfaceCount} surface(s) in {result.FileCount} file(s)";

            List<TerrainKoteCompareResultPoint> compared = result.Points
                .Where(point => point.Difference.HasValue)
                .ToList();

            if (compared.Count > 0)
            {
                double meanAbsolute = compared.Average(point => Math.Abs(point.Difference!.Value));
                double minimum = compared.Min(point => point.Difference!.Value);
                double maximum = compared.Max(point => point.Difference!.Value);
                detail += Environment.NewLine
                    + $"Difference {minimum.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture)}"
                    + $" … {maximum.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture)} m"
                    + $"  ·  mean |Δ| {meanAbsolute.ToString("0.00", CultureInfo.InvariantCulture)} m";
            }

            if (result.MultiCoverageCount > 0)
            {
                detail += Environment.NewLine
                    + $"{result.MultiCoverageCount} point(s) covered by more than one surface (closest used)";
            }

            return detail;
        }

        private static Button CreateButton(string text, bool isAccent = false)
        {
            Button button = new Button
            {
                Content = text,
                Margin = new Thickness(0, 0, 6, 6),
                Padding = new Thickness(12, 7, 12, 7),
                MinHeight = 32,
                Background = isAccent ? AccentButtonBackgroundBrush : ButtonBackgroundBrush,
                Foreground = ForegroundBrushValue,
                BorderBrush = BorderBrushValue,
                BorderThickness = new Thickness(1),
                Template = CreateButtonTemplate()
            };
            button.Tag = isAccent ? "accent" : "default";
            button.MouseEnter += (_, _) => ApplyButtonVisualState(button, isPressed: false);
            button.MouseLeave += (_, _) => ResetButtonVisualState(button);
            button.PreviewMouseLeftButtonDown += (_, _) => ApplyButtonVisualState(button, isPressed: true);
            button.PreviewMouseLeftButtonUp += (_, _) => ApplyButtonVisualState(button, isPressed: false);
            button.LostMouseCapture += (_, _) => ResetButtonVisualState(button);
            return button;
        }

        // A stepped section card: numbered badge, title, optional header action buttons, and a body.
        private UIElement CreateSection(string step, string title, UIElement body, IReadOnlyList<Button>? headerActions = null)
        {
            Grid header = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Border badge = new Border
            {
                Width = 26,
                Height = 26,
                CornerRadius = new CornerRadius(13),
                Background = StepBadgeBrush,
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new WinLabel
                {
                    Text = step,
                    Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(20, 26, 36)),
                    FontWeight = FontWeights.Bold,
                    FontSize = 13,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            Grid.SetColumn(badge, 0);
            header.Children.Add(badge);

            WinLabel titleText = new WinLabel
            {
                Text = title,
                Foreground = ForegroundBrushValue,
                FontWeight = FontWeights.Bold,
                FontSize = 15,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(titleText, 1);
            header.Children.Add(titleText);

            if (headerActions != null && headerActions.Count > 0)
            {
                WrapPanel actions = new WrapPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                foreach (Button action in headerActions)
                {
                    action.Margin = new Thickness(6, 0, 0, 0);
                    actions.Children.Add(action);
                }
                Grid.SetColumn(actions, 2);
                header.Children.Add(actions);
            }

            StackPanel content = new StackPanel { Orientation = Orientation.Vertical };
            content.Children.Add(header);
            content.Children.Add(body);

            return new Border
            {
                Background = CardBackgroundBrush,
                BorderBrush = BorderBrushValue,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14),
                Margin = new Thickness(0, 0, 0, 12),
                Child = content
            };
        }

        // The four large, scannable result counters — the visual centre of the palette.
        private UIElement BuildResultCounters()
        {
            System.Windows.Controls.Primitives.UniformGrid grid =
                new System.Windows.Controls.Primitives.UniformGrid { Columns = 4 };

            (TerrainKoteCompareClassification Classification, string Title)[] cards =
            {
                (TerrainKoteCompareClassification.Above, "Above terrain"),
                (TerrainKoteCompareClassification.Below, "Below terrain"),
                (TerrainKoteCompareClassification.OutsideSurface, "Outside surface"),
                (TerrainKoteCompareClassification.NoHeight, "Missing elevation")
            };

            foreach ((TerrainKoteCompareClassification classification, string cardTitle) in cards)
            {
                grid.Children.Add(CreateStatCard(classification, cardTitle));
            }

            return grid;
        }

        private UIElement CreateStatCard(TerrainKoteCompareClassification classification, string title)
        {
            System.Windows.Media.Color color = TerrainKoteCompareColors.GetMediaColor(classification);

            Border accent = new Border
            {
                Height = 4,
                Background = new SolidColorBrush(color),
                CornerRadius = new CornerRadius(2),
                Margin = new Thickness(0, 0, 0, 8),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            WinLabel number = new WinLabel
            {
                Text = "0",
                FontSize = 30,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(color)
            };
            _countTextByClass[classification] = number;

            StackPanel body = new StackPanel { Orientation = Orientation.Vertical };
            body.Children.Add(accent);
            body.Children.Add(number);
            body.Children.Add(new WinLabel
            {
                Text = title,
                FontSize = 12,
                Foreground = MutedForegroundBrush,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 0)
            });

            return new Border
            {
                Background = StatBackgroundBrush,
                BorderBrush = BorderBrushValue,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 10, 12, 12),
                Margin = new Thickness(0, 0, 8, 0),
                Child = body
            };
        }

        // The legend, doubling as the filter panel: color-coded checkboxes plus the preview controls.
        private UIElement BuildReviewFilters()
        {
            StackPanel body = new StackPanel { Orientation = Orientation.Vertical };
            body.Children.Add(new WinLabel
            {
                Text = "Checked classes are previewed, labelled and exported. Uncheck to exclude a class.",
                FontStyle = FontStyles.Italic,
                TextWrapping = TextWrapping.Wrap,
                Foreground = MutedForegroundBrush,
                Margin = new Thickness(0, 0, 0, 10)
            });

            System.Windows.Controls.Primitives.UniformGrid grid =
                new System.Windows.Controls.Primitives.UniformGrid { Columns = 2 };
            foreach (TerrainKoteCompareClassification classification in Enum.GetValues<TerrainKoteCompareClassification>())
            {
                grid.Children.Add(CreateLegendRow(
                    TerrainKoteCompareColors.GetMediaColor(classification),
                    TerrainKoteCompareExcelExport.DescribeClassification(classification),
                    classification));
            }
            body.Children.Add(grid);

            _showHideButton = CreateButton("Show");
            _showHideButton.Click += (_, _) => TogglePreview();
            _switchKoteButton = CreateButton("Switch kote");
            _switchKoteButton.Click += (_, _) => SwitchKote();

            WrapPanel controls = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
            controls.Children.Add(_showHideButton);
            controls.Children.Add(_switchKoteButton);
            body.Children.Add(controls);

            return body;
        }

        private static WinLabel CreateMutedText(string text)
        {
            return new WinLabel
            {
                Text = text,
                Foreground = MutedForegroundBrush,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12
            };
        }

        private UIElement CreateLegendRow(
            System.Windows.Media.Color color,
            string label,
            TerrainKoteCompareClassification classification)
        {
            StackPanel row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 4)
            };

            CheckBox checkBox = new CheckBox
            {
                IsChecked = _visibleClassifications.Contains(classification),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = ForegroundBrushValue,
                Margin = new Thickness(0, 0, 8, 0)
            };
            checkBox.Checked += (_, _) =>
            {
                _visibleClassifications.Add(classification);
                if (_previewVisible) RenderFilteredPreview();
            };
            checkBox.Unchecked += (_, _) =>
            {
                _visibleClassifications.Remove(classification);
                if (_previewVisible) RenderFilteredPreview();
            };
            row.Children.Add(checkBox);

            row.Children.Add(new Border
            {
                Width = 14,
                Height = 14,
                Background = new SolidColorBrush(color),
                BorderBrush = BorderBrushValue,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            });

            row.Children.Add(new WinLabel
            {
                Text = label,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = ForegroundBrushValue
            });

            return row;
        }

        private static Style CreateComboBoxStyle()
        {
            Style style = new Style(typeof(ComboBox));
            style.Setters.Add(new Setter(Control.TemplateProperty, CreateComboBoxTemplate()));
            style.Setters.Add(new Setter(ComboBox.ItemContainerStyleProperty, CreateComboBoxItemStyle()));
            return style;
        }

        private static Style CreateComboBoxItemStyle()
        {
            Style style = new Style(typeof(ComboBoxItem));
            style.Setters.Add(new Setter(Control.BackgroundProperty, InputBackgroundBrush));
            style.Setters.Add(new Setter(Control.ForegroundProperty, ForegroundBrushValue));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, BorderBrushValue));
            style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10, 6, 10, 6)));

            System.Windows.Controls.ControlTemplate template =
                new System.Windows.Controls.ControlTemplate(typeof(ComboBoxItem));
            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            border.SetValue(Border.MarginProperty, new Thickness(4, 2, 4, 2));

            FrameworkElementFactory presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetBinding(ContentPresenter.ContentProperty, new System.Windows.Data.Binding("Content") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            presenter.SetBinding(ContentPresenter.ContentTemplateProperty, new System.Windows.Data.Binding("ContentTemplate") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            presenter.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Left);
            presenter.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);

            border.AppendChild(presenter);
            template.VisualTree = border;

            Trigger highlightTrigger = new Trigger
            {
                Property = ComboBoxItem.IsHighlightedProperty,
                Value = true
            };
            highlightTrigger.Setters.Add(new Setter(Control.BackgroundProperty, ButtonBackgroundBrush));
            template.Triggers.Add(highlightTrigger);

            style.Setters.Add(new Setter(Control.TemplateProperty, template));
            return style;
        }

        // A ToggleButton that renders nothing but is still hit-testable (a Transparent brush is
        // hit-tested; a null Background is not).
        private static System.Windows.Controls.ControlTemplate CreateTransparentToggleTemplate()
        {
            System.Windows.Controls.ControlTemplate template =
                new System.Windows.Controls.ControlTemplate(typeof(ToggleButton));

            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });

            template.VisualTree = border;
            return template;
        }

        private static System.Windows.Controls.ControlTemplate CreateComboBoxTemplate()
        {
            System.Windows.Controls.ControlTemplate template =
                new System.Windows.Controls.ControlTemplate(typeof(ComboBox));

            FrameworkElementFactory grid = new FrameworkElementFactory(typeof(Grid));

            FrameworkElementFactory outerBorder = new FrameworkElementFactory(typeof(Border));
            outerBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            outerBorder.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            outerBorder.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            outerBorder.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding("BorderThickness") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            // Without this the control's Padding is dropped and the ComboBox renders ~18px tall,
            // noticeably shorter than the TextBoxes it sits next to.
            outerBorder.SetBinding(Border.PaddingProperty, new System.Windows.Data.Binding("Padding") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });

            FrameworkElementFactory innerGrid = new FrameworkElementFactory(typeof(Grid));
            FrameworkElementFactory textColumn = new FrameworkElementFactory(typeof(ColumnDefinition));
            textColumn.SetValue(ColumnDefinition.WidthProperty, new GridLength(1, GridUnitType.Star));
            FrameworkElementFactory iconColumn = new FrameworkElementFactory(typeof(ColumnDefinition));
            iconColumn.SetValue(ColumnDefinition.WidthProperty, new GridLength(28));
            innerGrid.AppendChild(textColumn);
            innerGrid.AppendChild(iconColumn);

            FrameworkElementFactory presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(Grid.ColumnProperty, 0);
            presenter.SetValue(ContentPresenter.MarginProperty, new Thickness(10, 0, 6, 0));
            presenter.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
            presenter.SetBinding(ContentPresenter.ContentProperty, new System.Windows.Data.Binding("SelectionBoxItem") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            presenter.SetBinding(ContentPresenter.ContentTemplateProperty, new System.Windows.Data.Binding("SelectionBoxItemTemplate") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });

            FrameworkElementFactory arrow = new FrameworkElementFactory(typeof(WinLabel));
            arrow.SetValue(Grid.ColumnProperty, 1);
            arrow.SetValue(WinLabel.TextProperty, "▼");
            arrow.SetValue(WinLabel.ForegroundProperty, ForegroundBrushValue);
            arrow.SetValue(WinLabel.FontSizeProperty, 10.0);
            arrow.SetValue(WinLabel.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            arrow.SetValue(WinLabel.VerticalAlignmentProperty, VerticalAlignment.Center);

            innerGrid.AppendChild(presenter);
            innerGrid.AppendChild(arrow);
            outerBorder.AppendChild(innerGrid);
            grid.AppendChild(outerBorder);

            // ComboBox does NOT open its own popup on click — the template is expected to supply a
            // ToggleButton two-way bound to IsDropDownOpen with ClickMode.Press. Omit it and the
            // control is dead to the mouse (only F4 / Alt+Down still work). It sits on top of the
            // chrome with a transparent, content-less template so it captures the click without
            // painting over the selected-item text.
            FrameworkElementFactory toggle = new FrameworkElementFactory(typeof(ToggleButton));
            toggle.SetValue(ToggleButton.ClickModeProperty, ClickMode.Press);
            toggle.SetValue(ToggleButton.FocusableProperty, false);
            toggle.SetValue(ToggleButton.BackgroundProperty, System.Windows.Media.Brushes.Transparent);
            toggle.SetValue(ToggleButton.TemplateProperty, CreateTransparentToggleTemplate());
            toggle.SetBinding(
                ToggleButton.IsCheckedProperty,
                new System.Windows.Data.Binding("IsDropDownOpen")
                {
                    RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent,
                    Mode = System.Windows.Data.BindingMode.TwoWay
                });
            grid.AppendChild(toggle);

            // PART_Popup is required by ComboBox's own code — without the named part the dropdown
            // never opens, which also means DropDownOpened (our refresh hook) never fires.
            FrameworkElementFactory popup = new FrameworkElementFactory(typeof(Popup));
            popup.SetValue(FrameworkElement.NameProperty, "PART_Popup");
            popup.SetValue(Popup.PlacementProperty, PlacementMode.Bottom);
            popup.SetValue(Popup.AllowsTransparencyProperty, true);
            popup.SetBinding(Popup.IsOpenProperty, new System.Windows.Data.Binding("IsDropDownOpen") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            popup.SetBinding(Popup.WidthProperty, new System.Windows.Data.Binding("ActualWidth") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });

            FrameworkElementFactory popupBorder = new FrameworkElementFactory(typeof(Border));
            popupBorder.SetValue(Border.MarginProperty, new Thickness(0, 4, 0, 0));
            popupBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            popupBorder.SetValue(Border.BackgroundProperty, PanelBackgroundBrush);
            popupBorder.SetValue(Border.BorderBrushProperty, BorderBrushValue);
            popupBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            popupBorder.SetValue(Border.PaddingProperty, new Thickness(4));

            FrameworkElementFactory scrollViewer = new FrameworkElementFactory(typeof(ScrollViewer));
            scrollViewer.SetValue(ScrollViewer.CanContentScrollProperty, true);
            scrollViewer.SetValue(ScrollViewer.MaxHeightProperty, 320.0);
            scrollViewer.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);

            FrameworkElementFactory itemsPresenter = new FrameworkElementFactory(typeof(ItemsPresenter));
            scrollViewer.AppendChild(itemsPresenter);
            popupBorder.AppendChild(scrollViewer);
            popup.AppendChild(popupBorder);
            grid.AppendChild(popup);

            template.VisualTree = grid;
            return template;
        }

        private static System.Windows.Controls.ControlTemplate CreateButtonTemplate()
        {
            System.Windows.Controls.ControlTemplate template = new System.Windows.Controls.ControlTemplate(typeof(Button));

            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            border.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            border.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding("BorderThickness") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            border.SetBinding(Border.PaddingProperty, new System.Windows.Data.Binding("Padding") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });

            FrameworkElementFactory presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
            presenter.SetBinding(ContentPresenter.ContentProperty, new System.Windows.Data.Binding("Content") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            presenter.SetBinding(ContentPresenter.ContentTemplateProperty, new System.Windows.Data.Binding("ContentTemplate") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });

            border.AppendChild(presenter);
            template.VisualTree = border;
            return template;
        }

        private static void ApplyButtonVisualState(Button button, bool isPressed)
        {
            bool isAccent = string.Equals(button.Tag as string, "accent", StringComparison.Ordinal);
            button.Background = isAccent
                ? (isPressed ? AccentButtonPressedBrush : AccentButtonHoverBrush)
                : (isPressed ? ButtonPressedBrush : ButtonHoverBrush);
        }

        private static void ResetButtonVisualState(Button button)
        {
            bool isAccent = string.Equals(button.Tag as string, "accent", StringComparison.Ordinal);
            button.Background = isAccent ? AccentButtonBackgroundBrush : ButtonBackgroundBrush;
        }

        #endregion
    }
}
