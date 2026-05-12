using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using Autodesk.Civil;
using Autodesk.Civil.DatabaseServices;
using IntersectUtilities.UtilsCommon;
using static IntersectUtilities.UtilsCommon.Utils;
using AcEntity = Autodesk.AutoCAD.DatabaseServices.Entity;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;
using CadColor = Autodesk.AutoCAD.Colors.Color;
using FormsOpenFileDialog = System.Windows.Forms.OpenFileDialog;
using WinFormsDialogResult = System.Windows.Forms.DialogResult;
using WinLabel = System.Windows.Controls.TextBlock;

namespace IntersectUtilities
{
    public partial class Intersect
    {
        private const string LERCompareTerrainCommandName = "LERCompareTerrain";

        /// <command>LERCompareTerrain</command>
        /// <summary>
        /// Opens an MPE palette for comparing selected 3D LER polylines against a TIN terrain surface loaded from an
        /// external DWG. The tool previews copied 3D pipe segments classified by vertical surface clearance relative to a
        /// user-defined meter threshold and can bake the preview into layer-separated 3D polylines for "less", "more",
        /// and "outside surface" segments. The preview is drawn on the source 3D polyline geometry, not on the terrain.
        /// Re-run the command to change which 3D polylines are included in the palette session.
        /// </summary>
        /// <category>MPE</category>
        [CommandMethod(LERCompareTerrainCommandName, CommandFlags.Modal)]
        public void LERCompareTerrain()
        {
            DocumentCollection docCol = AcadApp.DocumentManager;
            Document doc = docCol.MdiActiveDocument;
            Editor editor = doc.Editor;
            try
            {
                LERCompareTerrainPaletteHost.Show(doc);
                editor.WriteMessage($"\n{LERCompareTerrainCommandName} opened the palette.");
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                editor.WriteMessage($"\n{LERCompareTerrainCommandName} failed. See debug output for details.");
                return;
            }
        }
    }

    internal static class LERCompareTerrainPaletteHost
    {
        private static PaletteSet? _palette;
        private static LERCompareTerrainControl? _control;

        public static void Show(Document document)
        {
            if (_palette == null)
            {
                _control = new LERCompareTerrainControl();
                _palette = new PaletteSet(
                    "Compare LER Terrain",
                    "COMPARE_LER_TERRAIN",
                    new Guid("5E98E32A-B975-4E2E-B313-4B6DCC932E82"))
                {
                    Style = PaletteSetStyles.ShowPropertiesMenu | PaletteSetStyles.ShowCloseButton,
                    MinimumSize = new System.Drawing.Size(360, 260),
                    Size = new System.Drawing.Size(460, 520)
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
        }
    }

    internal sealed class LERCompareTerrainControl : UserControl
    {
        private static readonly System.Windows.Media.Brush PageBackgroundBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(35, 43, 58));
        private static readonly System.Windows.Media.Brush PanelBackgroundBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(46, 56, 74));
        private static readonly System.Windows.Media.Brush InputBackgroundBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(49, 59, 79));
        private static readonly System.Windows.Media.Brush ButtonBackgroundBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(85, 98, 122));
        private static readonly System.Windows.Media.Brush AccentButtonBackgroundBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(95, 166, 220));
        private static readonly System.Windows.Media.Brush ButtonHoverBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(98, 112, 138));
        private static readonly System.Windows.Media.Brush ButtonPressedBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(70, 82, 104));
        private static readonly System.Windows.Media.Brush AccentButtonHoverBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(109, 181, 235));
        private static readonly System.Windows.Media.Brush AccentButtonPressedBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(74, 145, 198));
        private static readonly System.Windows.Media.Brush BorderBrushValue = new SolidColorBrush(System.Windows.Media.Color.FromRgb(92, 108, 136));
        private static readonly System.Windows.Media.Brush ForegroundBrushValue = new SolidColorBrush(System.Windows.Media.Color.FromRgb(238, 243, 250));
        private readonly LERCompareTerrainTransientRenderer _renderer = new LERCompareTerrainTransientRenderer();
        private readonly TextBox _surfacePathTextBox;
        private readonly ComboBox _surfaceComboBox;
        private readonly TextBox _thresholdNumericUpDown;
        private readonly Button _applyButton;
        private readonly WinLabel _selectionLabel;
        private readonly TextBox _statusTextBox;
        private readonly Button _loadAllButton;
        private readonly Button _selectButton;
        private readonly Button _loadButton;
        private readonly Button _unloadButton;
        private readonly Button _previewButton;
        private readonly Button _bakeButton;
        private readonly Button _clearButton;

        private Document? _document;
        private List<ObjectId> _selectedPolylineIds = new List<ObjectId>();
        private Database? _surfaceDatabase;
        private List<LERCompareTerrainSurfaceDescriptor> _surfaces = new List<LERCompareTerrainSurfaceDescriptor>();
        private string? _loadedSurfaceName;
        private LERCompareTerrainPreviewResult? _lastPreviewResult;

        public LERCompareTerrainControl()
        {
            Background = PageBackgroundBrush;

            Grid root = new Grid
            {
                Margin = new Thickness(10),
                Background = Background
            };

            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(92) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });

            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            AddToGrid(root, CreateLabel("Surface DWG"), 0, 0);

            _surfacePathTextBox = new TextBox
            {
                IsReadOnly = true,
                Margin = new Thickness(0, 0, 6, 6),
                VerticalContentAlignment = VerticalAlignment.Center,
                Background = InputBackgroundBrush,
                Foreground = ForegroundBrushValue,
                BorderBrush = BorderBrushValue,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 6, 8, 6)
            };
            AddToGrid(root, _surfacePathTextBox, 0, 1);

            Button browseButton = CreateButton("Browse...");
            browseButton.Click += (_, _) => BrowseForSurfaceDwg();
            AddToGrid(root, browseButton, 0, 2);

            AddToGrid(root, CreateLabel("TIN Surface"), 1, 0);

            WrapPanel surfaceSelectionPanel = CreateHorizontalPanel();

            _surfaceComboBox = new ComboBox
            {
                Margin = new Thickness(0, 0, 6, 6),
                IsEditable = false,
                Background = InputBackgroundBrush,
                Foreground = ForegroundBrushValue,
                BorderBrush = BorderBrushValue,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 6, 10, 6),
                Style = CreateComboBoxStyle(),
                MinWidth = 220
            };
            surfaceSelectionPanel.Children.Add(_surfaceComboBox);

            _loadButton = CreateButton("Load Surface", true);
            _loadButton.Click += (_, _) => LoadSelectedSurface();

            _unloadButton = CreateButton("Unload Surface");
            _unloadButton.Click += (_, _) => UnloadSurface();

            surfaceSelectionPanel.Children.Add(_loadButton);
            surfaceSelectionPanel.Children.Add(_unloadButton);
            AddToGrid(root, surfaceSelectionPanel, 1, 1, 2);

            AddToGrid(root, CreateLabel("Threshold (m)"), 2, 0);

            WrapPanel thresholdPanel = CreateHorizontalPanel();

            _thresholdNumericUpDown = new TextBox
            {
                Text = "2.5",
                Width = 120,
                Margin = new Thickness(0, 0, 8, 6),
                VerticalContentAlignment = VerticalAlignment.Center,
                Background = InputBackgroundBrush,
                Foreground = ForegroundBrushValue,
                BorderBrush = BorderBrushValue,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 6, 8, 6)
            };
            thresholdPanel.Children.Add(_thresholdNumericUpDown);

            _applyButton = CreateButton("Apply");
            _applyButton.Click += (_, _) => RefreshPreview();
            thresholdPanel.Children.Add(_applyButton);
            AddToGrid(root, thresholdPanel, 2, 1, 2);

            WrapPanel buttonPanel = CreateHorizontalPanel();

            _loadAllButton = CreateButton("Load all");
            _loadAllButton.Click += (_, _) => LoadAllPolylines();

            _selectButton = CreateButton("Select");
            _selectButton.Click += (_, _) => SelectPolylines();

            _previewButton = CreateButton("Compute and Preview");
            _previewButton.Click += (_, _) => RefreshPreview();

            _clearButton = CreateButton("Clear Preview");
            _clearButton.Click += (_, _) => ClearPreview();

            _bakeButton = CreateButton("Export to Civil");
            _bakeButton.Click += (_, _) => BakePreview();

            buttonPanel.Children.Add(_loadAllButton);
            buttonPanel.Children.Add(_selectButton);
            buttonPanel.Children.Add(_previewButton);
            buttonPanel.Children.Add(_clearButton);
            buttonPanel.Children.Add(_bakeButton);
            AddToGrid(root, buttonPanel, 3, 0, 3);

            _selectionLabel = CreateLabel("Selected 3D polylines: 0");
            AddToGrid(root, _selectionLabel, 4, 0, 3);

            _statusTextBox = new TextBox
            {
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Margin = new Thickness(0, 0, 0, 6),
                Background = PanelBackgroundBrush,
                Foreground = ForegroundBrushValue,
                BorderBrush = BorderBrushValue,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 8, 10, 8)
            };
            AddToGrid(root, _statusTextBox, 5, 0, 3);

            AddToGrid(root, CreateLabel("Preview uses the selected 3D polylines, not the terrain surface."), 6, 0, 3);

            Content = root;
            UpdateStatus("Use Load all or Select, then load a terrain DWG and choose a TIN surface.");
        }

        public void AttachDocument(Document document)
        {
            _document = document;
            UpdateSelectionLabel();
            if (_selectedPolylineIds.Count == 0)
            {
                UpdateStatus("Palette opened. Use Load all or Select to choose 3D polylines.");
            }
        }

        private void SetSelection(IReadOnlyList<ObjectId> polylineIds)
        {
            _selectedPolylineIds = polylineIds
                .Where(id => id.IsValid && !id.IsErased)
                .Distinct()
                .ToList();
            _lastPreviewResult = null;
            _renderer.Clear();
            UpdateSelectionLabel();
        }

        private void UpdateSelectionLabel()
        {
            _selectionLabel.Text = $"Selected 3D polylines: {_selectedPolylineIds.Count}";
        }

        public void ClearPreview()
        {
            _renderer.Clear();
            _lastPreviewResult = null;
            UpdateStatus("Preview cleared.");
        }

        private void BrowseForSurfaceDwg()
        {
            using FormsOpenFileDialog dialog = new FormsOpenFileDialog
            {
                Title = "Select Terrain DWG",
                Filter = "Drawing Files (*.dwg)|*.dwg|All Files (*.*)|*.*",
                CheckFileExists = true,
                CheckPathExists = true,
                Multiselect = false
            };

            if (dialog.ShowDialog() != WinFormsDialogResult.OK)
            {
                return;
            }

            _surfacePathTextBox.Text = dialog.FileName;
            InspectSurfaceFile(dialog.FileName);
        }

        private void LoadAllPolylines()
        {
            if (_document == null)
            {
                UpdateStatus("No active document is attached to the palette.");
                return;
            }

            try
            {
                using DocumentLock docLock = _document.LockDocument();
                using Transaction tx = _document.Database.TransactionManager.StartTransaction();

                List<ObjectId> polylineIds = _document.Database
                    .HashSetOfType<Polyline3d>(tx)
                    .Where(polyline => IsVisiblePolyline(polyline, tx))
                    .Select(polyline => polyline.ObjectId)
                    .ToList();

                tx.Commit();
                SetSelection(polylineIds);
                UpdateStatus($"Loaded {polylineIds.Count} visible 3D polylines from the active drawing.");
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                UpdateStatus("Loading all 3D polylines failed. See debug output for details.");
            }
        }

        private void SelectPolylines()
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
                        new TypedValue((int)DxfCode.Start, "POLYLINE")
                    });
                PromptSelectionOptions options = new PromptSelectionOptions
                {
                    MessageForAdding = "\nSelect 3D polylines to compare against terrain: "
                };

                PromptSelectionResult selectionResult = editor.GetSelection(options, filter);
                if (selectionResult.Status != PromptStatus.OK || selectionResult.Value == null || selectionResult.Value.Count == 0)
                {
                    UpdateStatus("Selection cancelled or empty.");
                    return;
                }

                using DocumentLock docLock = _document.LockDocument();
                using Transaction tx = _document.Database.TransactionManager.StartTransaction();

                List<ObjectId> polylineIds = new List<ObjectId>();
                foreach (ObjectId objectId in selectionResult.Value.GetObjectIds())
                {
                    if (tx.GetObject(objectId, OpenMode.ForRead, false) is Polyline3d)
                    {
                        polylineIds.Add(objectId);
                    }
                }

                tx.Commit();

                if (polylineIds.Count == 0)
                {
                    UpdateStatus("Selection did not contain any 3D polylines.");
                    return;
                }

                SetSelection(polylineIds);
                UpdateStatus($"Loaded {polylineIds.Count} selected 3D polylines.");
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                UpdateStatus("Selection failed. See debug output for details.");
            }
        }

        private static bool IsVisiblePolyline(Polyline3d polyline, Transaction tx)
        {
            if (!polyline.Visible)
            {
                return false;
            }

            LayerTableRecord layer = (LayerTableRecord)tx.GetObject(polyline.LayerId, OpenMode.ForRead);
            return !layer.IsFrozen && !layer.IsOff;
        }

        private void InspectSurfaceFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                UpdateStatus("Select a terrain DWG path first.");
                return;
            }

            if (!File.Exists(path))
            {
                UpdateStatus("Terrain DWG file not found.");
                return;
            }

            try
            {
                UnloadSurface(clearStatus: false, clearAvailableSurfaces: true);

                using Database surfaceInspectionDatabase = new Database(false, true);
                surfaceInspectionDatabase.ReadDwgFile(path, FileOpenMode.OpenForReadAndAllShare, false, null);

                using Transaction tx = surfaceInspectionDatabase.TransactionManager.StartTransaction();
                _surfaces = surfaceInspectionDatabase
                    .HashSetOfType<TinSurface>(tx)
                    .Select(surface => new LERCompareTerrainSurfaceDescriptor(surface.ObjectId, surface.Name))
                    .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                tx.Commit();

                _surfaceComboBox.Items.Clear();
                foreach (LERCompareTerrainSurfaceDescriptor surface in _surfaces)
                {
                    _surfaceComboBox.Items.Add(surface.Name);
                }

                UpdateSurfaceSelectionState();

                if (_surfaces.Count == 0)
                {
                    UpdateStatus("No TinSurface entities were found in the selected DWG.");
                    return;
                }

                _surfaceComboBox.SelectedIndex = 0;
                UpdateStatus(_surfaces.Count == 1
                    ? $"Found 1 terrain surface in {Path.GetFileName(path)}. Click Load Surface to use it."
                    : $"Found {_surfaces.Count} terrain surface(s) in {Path.GetFileName(path)}. Select one and click Load Surface.");
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                UpdateStatus("Failed to inspect the terrain DWG. See debug output for details.");
            }
        }

        private void LoadSelectedSurface()
        {
            string path = _surfacePathTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(path))
            {
                UpdateStatus("Select a terrain DWG path first.");
                return;
            }

            if (!File.Exists(path))
            {
                UpdateStatus("Terrain DWG file not found.");
                return;
            }

            if (_surfaces.Count == 0 || _surfaceComboBox.SelectedIndex < 0)
            {
                UpdateStatus("Browse to a terrain DWG and select a TIN surface first.");
                return;
            }

            string selectedSurfaceName = _surfaces[_surfaceComboBox.SelectedIndex].Name;

            try
            {
                UnloadSurface(clearStatus: false);

                _surfaceDatabase = new Database(false, true);
                _surfaceDatabase.ReadDwgFile(path, FileOpenMode.OpenForReadAndAllShare, false, null);

                using Transaction tx = _surfaceDatabase.TransactionManager.StartTransaction();
                _surfaces = _surfaceDatabase
                    .HashSetOfType<TinSurface>(tx)
                    .Select(surface => new LERCompareTerrainSurfaceDescriptor(surface.ObjectId, surface.Name))
                    .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                tx.Commit();

                int selectedIndex = _surfaces.FindIndex(surface => string.Equals(surface.Name, selectedSurfaceName, StringComparison.Ordinal));
                if (selectedIndex < 0)
                {
                    UnloadSurface(clearStatus: false, clearAvailableSurfaces: true);
                    _surfacePathTextBox.Text = path;
                    InspectSurfaceFile(path);
                    UpdateStatus("The selected TIN surface could not be loaded from the DWG.");
                    return;
                }

                _surfaceComboBox.Items.Clear();
                foreach (LERCompareTerrainSurfaceDescriptor surface in _surfaces)
                {
                    _surfaceComboBox.Items.Add(surface.Name);
                }
                _surfaceComboBox.SelectedIndex = selectedIndex;
                UpdateSurfaceSelectionState();
                _loadedSurfaceName = _surfaces[selectedIndex].Name;

                UpdateStatus($"Loaded terrain surface \"{_loadedSurfaceName}\". Use Compute and Preview after loading 3D polylines.");
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                UnloadSurface(clearStatus: false);
                UpdateStatus("Failed to load the selected terrain surface. See debug output for details.");
            }
        }

        private void UnloadSurface(bool clearStatus = true, bool clearAvailableSurfaces = false)
        {
            _renderer.Clear();
            _lastPreviewResult = null;
            _surfaceDatabase?.Dispose();
            _surfaceDatabase = null;
            _loadedSurfaceName = null;
            if (clearAvailableSurfaces)
            {
                _surfaceComboBox.Items.Clear();
                _surfaces = new List<LERCompareTerrainSurfaceDescriptor>();
            }

            UpdateSurfaceSelectionState();

            if (clearStatus)
            {
                UpdateStatus("Surface unloaded.");
            }
        }

        private void RefreshPreview()
        {
            if (_document == null)
            {
                UpdateStatus("No active document is attached to the palette.");
                return;
            }

            if (_surfaceDatabase == null || _surfaces.Count == 0 || _surfaceComboBox.SelectedIndex < 0)
            {
                UpdateStatus("Browse to a terrain DWG, select a surface, and click Load Surface first.");
                return;
            }

            if (_selectedPolylineIds.Count == 0)
            {
                UpdateStatus("No 3D polylines are loaded in the palette. Use Load all or Select first.");
                return;
            }

            try
            {
                using DocumentLock docLock = _document.LockDocument();
                using Transaction drawingTx = _document.Database.TransactionManager.StartTransaction();
                using Transaction surfaceTx = _surfaceDatabase.TransactionManager.StartTransaction();

                LERCompareTerrainSurfaceDescriptor descriptor = _surfaces[_surfaceComboBox.SelectedIndex];
                TinSurface? surface = surfaceTx.GetObject(descriptor.ObjectId, OpenMode.ForRead) as TinSurface;
                if (surface == null)
                {
                    UpdateStatus("The selected TIN surface could not be opened.");
                    drawingTx.Commit();
                    surfaceTx.Commit();
                    return;
                }

                double threshold = GetThreshold();
                LERCompareTerrainPreviewResult result = LERCompareTerrainAnalyzer.Analyze(
                    drawingTx,
                    surface,
                    _selectedPolylineIds,
                    threshold);

                _renderer.Show(result.Pieces);
                _lastPreviewResult = result;

                drawingTx.Commit();
                surfaceTx.Commit();

                UpdateStatus(BuildPreviewStatus(result, _loadedSurfaceName ?? descriptor.Name, threshold));
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                UpdateStatus("Preview failed. See debug output for details.");
            }
        }

        private void BakePreview()
        {
            if (_document == null)
            {
                UpdateStatus("No active document is attached to the palette.");
                return;
            }

            if (_lastPreviewResult == null || _lastPreviewResult.Pieces.Count == 0)
            {
                RefreshPreview();
            }

            if (_lastPreviewResult == null || _lastPreviewResult.Pieces.Count == 0)
            {
                UpdateStatus("Nothing to bake. Generate a preview first.");
                return;
            }

            try
            {
                using DocumentLock docLock = _document.LockDocument();
                using Transaction tx = _document.Database.TransactionManager.StartTransaction();

                BlockTable blockTable = (BlockTable)tx.GetObject(_document.Database.BlockTableId, OpenMode.ForRead);
                BlockTableRecord modelSpace =
                    (BlockTableRecord)tx.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                EnsureLayerExists(
                    LERCompareTerrainLayerNames.AboveTerrainLayerName,
                    tx,
                    _document.Database,
                    CadColor.FromColorIndex(ColorMethod.ByAci, 6));
                EnsureLayerExists(
                    LERCompareTerrainLayerNames.BuildLessLayerName(GetThreshold()),
                    tx,
                    _document.Database,
                    CadColor.FromColorIndex(ColorMethod.ByAci, 1));
                EnsureLayerExists(
                    LERCompareTerrainLayerNames.BuildMoreLayerName(GetThreshold()),
                    tx,
                    _document.Database,
                    CadColor.FromColorIndex(ColorMethod.ByAci, 3));
                EnsureLayerExists(
                    LERCompareTerrainLayerNames.OutsideLayerName,
                    tx,
                    _document.Database,
                    CadColor.FromColorIndex(ColorMethod.ByAci, 2));

                ClearExistingExportedGeometry(
                    modelSpace,
                    tx,
                    new[]
                    {
                        LERCompareTerrainLayerNames.AboveTerrainLayerName,
                        LERCompareTerrainLayerNames.BuildLessLayerName(GetThreshold()),
                        LERCompareTerrainLayerNames.BuildMoreLayerName(GetThreshold()),
                        LERCompareTerrainLayerNames.OutsideLayerName
                    });

                int createdCount = 0;
                foreach (LERCompareTerrainPiece piece in _lastPreviewResult.Pieces)
                {
                    if (piece.Points.Count < 2)
                    {
                        continue;
                    }

                    if (tx.GetObject(piece.SourceId, OpenMode.ForRead, false) is not Polyline3d sourcePolyline)
                    {
                        continue;
                    }

                    Polyline3d bakedPolyline = new Polyline3d(
                        Poly3dType.SimplePoly,
                        new Point3dCollection(piece.Points.ToArray()),
                        false);
                    bakedPolyline.SetPropertiesFrom(sourcePolyline);
                    bakedPolyline.Layer = LERCompareTerrainLayerNames.GetLayerName(piece.Classification, GetThreshold());
                    bakedPolyline.Color = CadColor.FromColorIndex(ColorMethod.ByLayer, 256);

                    modelSpace.AppendEntity(bakedPolyline);
                    tx.AddNewlyCreatedDBObject(bakedPolyline, true);
                    createdCount++;
                }

                tx.Commit();
                _renderer.Clear();
                UpdateStatus(
                    $"Export complete. Created {createdCount} 3D polyline piece(s) on layers "
                    + $"{LERCompareTerrainLayerNames.AboveTerrainLayerName}, "
                    + $"{LERCompareTerrainLayerNames.BuildLessLayerName(GetThreshold())}, "
                    + $"{LERCompareTerrainLayerNames.BuildMoreLayerName(GetThreshold())}, and "
                    + $"{LERCompareTerrainLayerNames.OutsideLayerName}.");
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                UpdateStatus("Bake failed. See debug output for details.");
            }
        }

        private double GetThreshold()
        {
            if (double.TryParse(_thresholdNumericUpDown.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double invariantValue)
                && invariantValue > 0.0)
            {
                return invariantValue;
            }

            if (double.TryParse(_thresholdNumericUpDown.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out double localValue)
                && localValue > 0.0)
            {
                return localValue;
            }

            _thresholdNumericUpDown.Text = "2.5";
            return 2.5;
        }

        private void UpdateStatus(string message)
        {
            _statusTextBox.Text = message;
        }

        private void UpdateSurfaceSelectionState()
        {
            bool hasSurfaces = _surfaces.Count > 0;
            _surfaceComboBox.IsEnabled = hasSurfaces;
            _loadButton.IsEnabled = hasSurfaces;
        }

        private static void AddToGrid(Grid grid, UIElement element, int row, int column, int columnSpan = 1)
        {
            Grid.SetRow(element, row);
            Grid.SetColumn(element, column);
            if (columnSpan > 1)
            {
                Grid.SetColumnSpan(element, columnSpan);
            }

            grid.Children.Add(element);
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

        private static WinLabel CreateLabel(string text)
        {
            return new WinLabel
            {
                Text = text,
                Margin = new Thickness(0, 6, 8, 10),
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Foreground = ForegroundBrushValue,
                FontWeight = FontWeights.SemiBold
            };
        }

        private static WrapPanel CreateHorizontalPanel()
        {
            return new WrapPanel
            {
                Margin = new Thickness(0, 0, 0, 8)
            };
        }

        private static ControlTemplate CreateButtonTemplate()
        {
            ControlTemplate template = new ControlTemplate(typeof(Button));

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

            ControlTemplate template = new ControlTemplate(typeof(ComboBoxItem));
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

            Trigger selectedTrigger = new Trigger
            {
                Property = ComboBoxItem.IsHighlightedProperty,
                Value = true
            };
            selectedTrigger.Setters.Add(new Setter(Control.BackgroundProperty, ButtonBackgroundBrush));
            template.Triggers.Add(selectedTrigger);

            style.Setters.Add(new Setter(Control.TemplateProperty, template));
            return style;
        }

        private static ControlTemplate CreateComboBoxTemplate()
        {
            ControlTemplate template = new ControlTemplate(typeof(ComboBox));

            FrameworkElementFactory grid = new FrameworkElementFactory(typeof(Grid));

            FrameworkElementFactory outerBorder = new FrameworkElementFactory(typeof(Border));
            outerBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            outerBorder.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            outerBorder.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            outerBorder.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding("BorderThickness") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });

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

            FrameworkElementFactory arrow = new FrameworkElementFactory(typeof(System.Windows.Controls.TextBlock));
            arrow.SetValue(Grid.ColumnProperty, 1);
            arrow.SetValue(System.Windows.Controls.TextBlock.TextProperty, "▼");
            arrow.SetValue(System.Windows.Controls.TextBlock.ForegroundProperty, ForegroundBrushValue);
            arrow.SetValue(System.Windows.Controls.TextBlock.FontSizeProperty, 10.0);
            arrow.SetValue(System.Windows.Controls.TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            arrow.SetValue(System.Windows.Controls.TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);

            innerGrid.AppendChild(presenter);
            innerGrid.AppendChild(arrow);
            outerBorder.AppendChild(innerGrid);
            grid.AppendChild(outerBorder);

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
            scrollViewer.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);

            FrameworkElementFactory itemsPresenter = new FrameworkElementFactory(typeof(ItemsPresenter));
            scrollViewer.AppendChild(itemsPresenter);
            popupBorder.AppendChild(scrollViewer);
            popup.AppendChild(popupBorder);
            grid.AppendChild(popup);

            template.VisualTree = grid;
            return template;
        }

        private static string BuildPreviewStatus(
            LERCompareTerrainPreviewResult result,
            string surfaceName,
            double threshold)
        {
            return
                $"Surface: {surfaceName}{Environment.NewLine}"
                + $"Threshold: {threshold.ToString("0.###", CultureInfo.InvariantCulture)} m{Environment.NewLine}"
                + $"Source 3D polylines analyzed: {result.AnalyzedPolylineCount}{Environment.NewLine}"
                + $"Preview pieces: {result.Pieces.Count}{Environment.NewLine}"
                + $"Above terrain pieces: {result.AboveTerrainCount}{Environment.NewLine}"
                + $"Less/equal pieces: {result.LessOrEqualCount}{Environment.NewLine}"
                + $"More pieces: {result.MoreCount}{Environment.NewLine}"
                + $"Outside pieces: {result.OutsideCount}";
        }

        private static void EnsureLayerExists(string layerName, Transaction transaction, Database database, CadColor? color)
        {
            LayerTable layerTable = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);
            if (layerTable.Has(layerName))
            {
                if (color == null)
                {
                    return;
                }

                LayerTableRecord existingLayer = (LayerTableRecord)transaction.GetObject(layerTable[layerName], OpenMode.ForWrite);
                existingLayer.Color = color;
                return;
            }

            layerTable.UpgradeOpen();
            LayerTableRecord layer = new LayerTableRecord
            {
                Name = layerName
            };

            if (color != null)
            {
                layer.Color = color;
            }

            layerTable.Add(layer);
            transaction.AddNewlyCreatedDBObject(layer, true);
        }

        private static void ClearExistingExportedGeometry(
            BlockTableRecord modelSpace,
            Transaction transaction,
            IEnumerable<string> layerNames)
        {
            HashSet<string> targetLayers = new HashSet<string>(layerNames, StringComparer.OrdinalIgnoreCase);
            foreach (ObjectId entityId in modelSpace)
            {
                if (transaction.GetObject(entityId, OpenMode.ForRead, false) is not AcEntity entity)
                {
                    continue;
                }

                if (!targetLayers.Contains(entity.Layer))
                {
                    continue;
                }

                entity.UpgradeOpen();
                entity.Erase();
            }
        }
    }

    internal static class LERCompareTerrainLayerNames
    {
        public const string AboveTerrainLayerName = "0 - above Terrain";
        public const string OutsideLayerName = "0 - Outside segment";

        public static string BuildLessLayerName(double threshold)
        {
            return $"0 - less then {FormatThreshold(threshold)}";
        }

        public static string BuildMoreLayerName(double threshold)
        {
            return $"0 - more than {FormatThreshold(threshold)}m";
        }

        public static string GetLayerName(LERCompareTerrainClassification classification, double threshold)
        {
            return classification switch
            {
                LERCompareTerrainClassification.AboveTerrain => AboveTerrainLayerName,
                LERCompareTerrainClassification.LessOrEqualThreshold => BuildLessLayerName(threshold),
                LERCompareTerrainClassification.MoreThanThreshold => BuildMoreLayerName(threshold),
                _ => OutsideLayerName
            };
        }

        private static string FormatThreshold(double threshold)
        {
            return threshold.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }

    internal sealed class LERCompareTerrainTransientRenderer
    {
        private readonly TransientManager _transientManager = TransientManager.CurrentTransientManager;
        private readonly List<AcEntity> _currentEntities = new List<AcEntity>();

        public void Show(IReadOnlyList<LERCompareTerrainPiece> pieces)
        {
            Clear();

            foreach (LERCompareTerrainPiece piece in pieces)
            {
                if (piece.Points.Count < 2)
                {
                    continue;
                }

                short colorIndex = GetColorIndex(piece.Classification);
                Polyline3d previewPolyline = new Polyline3d(
                    Poly3dType.SimplePoly,
                    new Point3dCollection(piece.Points.ToArray()),
                    false)
                {
                    Color = CadColor.FromColorIndex(ColorMethod.ByAci, colorIndex)
                };

                _currentEntities.Add(previewPolyline);
                _transientManager.AddTransient(
                    previewPolyline,
                    TransientDrawingMode.DirectShortTerm,
                    128,
                    new IntegerCollection());
            }
        }

        public void Clear()
        {
            foreach (AcEntity entity in _currentEntities)
            {
                try
                {
                    _transientManager.EraseTransient(entity, new IntegerCollection());
                }
                catch
                {
                    // Intentionally ignored during transient cleanup.
                }

                entity.Dispose();
            }

            _currentEntities.Clear();
        }

        private static short GetColorIndex(LERCompareTerrainClassification classification)
        {
            return classification switch
            {
                LERCompareTerrainClassification.AboveTerrain => 6,
                LERCompareTerrainClassification.LessOrEqualThreshold => 1,
                LERCompareTerrainClassification.MoreThanThreshold => 3,
                _ => 2
            };
        }
    }

    internal static class LERCompareTerrainAnalyzer
    {
        private const double ParameterTolerance = 1e-8;
        private const double PointTolerance = 1e-6;

        public static LERCompareTerrainPreviewResult Analyze(
            Transaction drawingTransaction,
            TinSurface surface,
            IReadOnlyList<ObjectId> polylineIds,
            double threshold)
        {
            List<LERCompareTerrainPiece> pieces = new List<LERCompareTerrainPiece>();
            int analyzedPolylineCount = 0;

            foreach (ObjectId polylineId in polylineIds.Distinct())
            {
                if (!polylineId.IsValid || polylineId.IsErased)
                {
                    continue;
                }

                if (drawingTransaction.GetObject(polylineId, OpenMode.ForRead, false) is not Polyline3d polyline)
                {
                    continue;
                }

                List<Point3d> vertices = GetVertices(polyline, drawingTransaction);
                if (vertices.Count < 2)
                {
                    continue;
                }

                analyzedPolylineCount++;
                pieces.AddRange(AnalyzePolyline(polylineId, vertices, surface, threshold));
            }

            return new LERCompareTerrainPreviewResult(analyzedPolylineCount, pieces);
        }

        private static List<Point3d> GetVertices(Polyline3d polyline, Transaction transaction)
        {
            List<Point3d> points = new List<Point3d>();
            foreach (ObjectId vertexId in polyline)
            {
                if (transaction.GetObject(vertexId, OpenMode.ForRead, false) is PolylineVertex3d vertex)
                {
                    points.Add(vertex.Position);
                }
            }

            return points;
        }

        private static IReadOnlyList<LERCompareTerrainPiece> AnalyzePolyline(
            ObjectId sourceId,
            IReadOnlyList<Point3d> vertices,
            TinSurface surface,
            double threshold)
        {
            LERCompareTerrainPieceBuilder builder = new LERCompareTerrainPieceBuilder(sourceId);

            for (int i = 0; i < vertices.Count - 1; i++)
            {
                Point3d startPoint = vertices[i];
                Point3d endPoint = vertices[i + 1];
                foreach (LERCompareTerrainSpan span in AnalyzeSegment(startPoint, endPoint, surface, threshold))
                {
                    builder.Append(span.Classification, span.StartPoint, span.EndPoint);
                }
            }

            return builder.Finish();
        }

        private static IReadOnlyList<LERCompareTerrainSpan> AnalyzeSegment(
            Point3d startPoint,
            Point3d endPoint,
            TinSurface surface,
            double threshold)
        {
            List<LERCompareTerrainSpan> spans = new List<LERCompareTerrainSpan>();
            if (startPoint.DistanceTo(endPoint) <= PointTolerance)
            {
                return spans;
            }

            List<double> parameters = CollectBreakParameters(startPoint, endPoint, surface);
            parameters.Sort();

            for (int i = 0; i < parameters.Count - 1; i++)
            {
                double intervalStart = parameters[i];
                double intervalEnd = parameters[i + 1];
                if (intervalEnd - intervalStart <= ParameterTolerance)
                {
                    continue;
                }

                LERCompareTerrainEvaluation startEval = Evaluate(startPoint, endPoint, surface, intervalStart);
                LERCompareTerrainEvaluation endEval = Evaluate(startPoint, endPoint, surface, intervalEnd);
                LERCompareTerrainEvaluation midEval = Evaluate(startPoint, endPoint, surface, (intervalStart + intervalEnd) * 0.5);

                if (!midEval.IsOnSurface)
                {
                    ProcessMidOutsideInterval(startPoint, endPoint, surface, threshold, intervalStart, intervalEnd, startEval, endEval, spans);
                }
                else
                {
                    ProcessMidInsideInterval(startPoint, endPoint, surface, threshold, intervalStart, intervalEnd, startEval, endEval, midEval, spans);
                }
            }

            return spans;
        }

        private static void ProcessMidOutsideInterval(
            Point3d startPoint,
            Point3d endPoint,
            TinSurface surface,
            double threshold,
            double intervalStart,
            double intervalEnd,
            LERCompareTerrainEvaluation startEval,
            LERCompareTerrainEvaluation endEval,
            IList<LERCompareTerrainSpan> spans)
        {
            if (!startEval.IsOnSurface && !endEval.IsOnSurface)
            {
                AddSpan(spans, LERCompareTerrainClassification.OutsideSurface, Interpolate(startPoint, endPoint, intervalStart), Interpolate(startPoint, endPoint, intervalEnd));
                return;
            }

            if (startEval.IsOnSurface && !endEval.IsOnSurface)
            {
                double boundary = FindSurfaceBoundaryParameter(startPoint, endPoint, surface, intervalStart, intervalEnd);
                AddThresholdAwareInsideSpan(startPoint, endPoint, surface, threshold, intervalStart, boundary, spans);
                AddSpan(spans, LERCompareTerrainClassification.OutsideSurface, Interpolate(startPoint, endPoint, boundary), Interpolate(startPoint, endPoint, intervalEnd));
                return;
            }

            if (!startEval.IsOnSurface && endEval.IsOnSurface)
            {
                double boundary = FindSurfaceBoundaryParameter(startPoint, endPoint, surface, intervalEnd, intervalStart);
                AddSpan(spans, LERCompareTerrainClassification.OutsideSurface, Interpolate(startPoint, endPoint, intervalStart), Interpolate(startPoint, endPoint, boundary));
                AddThresholdAwareInsideSpan(startPoint, endPoint, surface, threshold, boundary, intervalEnd, spans);
                return;
            }

            double leftBoundary = FindSurfaceBoundaryParameter(startPoint, endPoint, surface, intervalStart, (intervalStart + intervalEnd) * 0.5);
            double rightBoundary = FindSurfaceBoundaryParameter(startPoint, endPoint, surface, intervalEnd, (intervalStart + intervalEnd) * 0.5);

            if (rightBoundary < leftBoundary)
            {
                (leftBoundary, rightBoundary) = (rightBoundary, leftBoundary);
            }

            AddThresholdAwareInsideSpan(startPoint, endPoint, surface, threshold, intervalStart, leftBoundary, spans);
            AddSpan(spans, LERCompareTerrainClassification.OutsideSurface, Interpolate(startPoint, endPoint, leftBoundary), Interpolate(startPoint, endPoint, rightBoundary));
            AddThresholdAwareInsideSpan(startPoint, endPoint, surface, threshold, rightBoundary, intervalEnd, spans);
        }

        private static void ProcessMidInsideInterval(
            Point3d startPoint,
            Point3d endPoint,
            TinSurface surface,
            double threshold,
            double intervalStart,
            double intervalEnd,
            LERCompareTerrainEvaluation startEval,
            LERCompareTerrainEvaluation endEval,
            LERCompareTerrainEvaluation midEval,
            IList<LERCompareTerrainSpan> spans)
        {
            double insideStart = intervalStart;
            double insideEnd = intervalEnd;

            if (!startEval.IsOnSurface)
            {
                insideStart = FindSurfaceBoundaryParameter(startPoint, endPoint, surface, (intervalStart + intervalEnd) * 0.5, intervalStart);
                AddSpan(spans, LERCompareTerrainClassification.OutsideSurface, Interpolate(startPoint, endPoint, intervalStart), Interpolate(startPoint, endPoint, insideStart));
            }

            if (!endEval.IsOnSurface)
            {
                insideEnd = FindSurfaceBoundaryParameter(startPoint, endPoint, surface, (intervalStart + intervalEnd) * 0.5, intervalEnd);
                AddSpan(spans, LERCompareTerrainClassification.OutsideSurface, Interpolate(startPoint, endPoint, insideEnd), Interpolate(startPoint, endPoint, intervalEnd));
            }

            if (insideEnd - insideStart <= ParameterTolerance)
            {
                return;
            }

            AddThresholdAwareInsideSpan(startPoint, endPoint, surface, threshold, insideStart, insideEnd, spans);
        }

        private static void AddThresholdAwareInsideSpan(
            Point3d startPoint,
            Point3d endPoint,
            TinSurface surface,
            double threshold,
            double intervalStart,
            double intervalEnd,
            IList<LERCompareTerrainSpan> spans)
        {
            if (intervalEnd - intervalStart <= ParameterTolerance)
            {
                return;
            }

            double sampleStart = MoveInside(intervalStart, intervalEnd);
            double sampleEnd = MoveInside(intervalEnd, intervalStart);
            if (sampleEnd - sampleStart <= ParameterTolerance)
            {
                sampleStart = intervalStart;
                sampleEnd = intervalEnd;
            }

            LERCompareTerrainEvaluation startEval = Evaluate(startPoint, endPoint, surface, sampleStart);
            LERCompareTerrainEvaluation endEval = Evaluate(startPoint, endPoint, surface, sampleEnd);
            if (!startEval.IsOnSurface || !endEval.IsOnSurface)
            {
                return;
            }

            LERCompareTerrainClassification startClass = Classify(startEval.Clearance, threshold);
            LERCompareTerrainClassification endClass = Classify(endEval.Clearance, threshold);
            if (startClass == endClass)
            {
                AddSpan(spans, startClass, Interpolate(startPoint, endPoint, intervalStart), Interpolate(startPoint, endPoint, intervalEnd));
                return;
            }

            double thresholdBoundary = FindThresholdBoundaryParameter(
                startPoint,
                endPoint,
                surface,
                threshold,
                sampleStart,
                sampleEnd,
                startClass);

            AddSpan(spans, startClass, Interpolate(startPoint, endPoint, intervalStart), Interpolate(startPoint, endPoint, thresholdBoundary));
            AddSpan(spans, endClass, Interpolate(startPoint, endPoint, thresholdBoundary), Interpolate(startPoint, endPoint, intervalEnd));
        }

        private static List<double> CollectBreakParameters(Point3d startPoint, Point3d endPoint, TinSurface surface)
        {
            List<double> parameters = new List<double> { 0.0, 1.0 };

            if (HasPlanLength(startPoint, endPoint))
            {
                try
                {
                    Point3dCollection samplePoints = surface.SampleElevations(startPoint, endPoint);
                    foreach (Point3d samplePoint in samplePoints)
                    {
                        double parameter = CalculatePlanParameter(startPoint, endPoint, samplePoint);
                        AddDistinctParameter(parameters, parameter);
                    }
                }
                catch (Autodesk.Civil.SurfaceException)
                {
                    // Surface sampling may fail on segments that do not intersect the surface. Midpoint classification handles that case.
                }
                catch (System.ArgumentException)
                {
                    // Invalid sample input is treated as "no additional breakpoints".
                }
            }

            return parameters;
        }

        private static void AddDistinctParameter(ICollection<double> parameters, double parameter)
        {
            double clamped = Math.Max(0.0, Math.Min(1.0, parameter));
            if (parameters.Any(existing => Math.Abs(existing - clamped) <= ParameterTolerance))
            {
                return;
            }

            parameters.Add(clamped);
        }

        private static LERCompareTerrainEvaluation Evaluate(Point3d startPoint, Point3d endPoint, TinSurface surface, double parameter)
        {
            Point3d point = Interpolate(startPoint, endPoint, parameter);

            try
            {
                double surfaceElevation = surface.FindElevationAtXY(point.X, point.Y);
                return LERCompareTerrainEvaluation.OnSurface(point, parameter, surfaceElevation - point.Z);
            }
            catch (Autodesk.Civil.PointNotOnEntityException)
            {
                return LERCompareTerrainEvaluation.Outside(point, parameter);
            }
            catch (System.ArgumentException)
            {
                return LERCompareTerrainEvaluation.Outside(point, parameter);
            }
        }

        private static double FindSurfaceBoundaryParameter(
            Point3d startPoint,
            Point3d endPoint,
            TinSurface surface,
            double insideParameter,
            double outsideParameter)
        {
            double inside = insideParameter;
            double outside = outsideParameter;

            for (int i = 0; i < 40; i++)
            {
                double middle = (inside + outside) * 0.5;
                LERCompareTerrainEvaluation evaluation = Evaluate(startPoint, endPoint, surface, middle);
                if (evaluation.IsOnSurface)
                {
                    inside = middle;
                }
                else
                {
                    outside = middle;
                }
            }

            return (inside + outside) * 0.5;
        }

        private static double FindThresholdBoundaryParameter(
            Point3d startPoint,
            Point3d endPoint,
            TinSurface surface,
            double threshold,
            double startParameter,
            double endParameter,
            LERCompareTerrainClassification startClassification)
        {
            double low = startParameter;
            double high = endParameter;
            LERCompareTerrainClassification lowClassification = startClassification;

            for (int i = 0; i < 40; i++)
            {
                double middle = (low + high) * 0.5;
                LERCompareTerrainEvaluation evaluation = Evaluate(startPoint, endPoint, surface, middle);
                if (!evaluation.IsOnSurface)
                {
                    break;
                }

                LERCompareTerrainClassification middleClassification = Classify(evaluation.Clearance, threshold);
                if (middleClassification == lowClassification)
                {
                    low = middle;
                }
                else
                {
                    high = middle;
                }
            }

            return (low + high) * 0.5;
        }

        private static LERCompareTerrainClassification Classify(double clearance, double threshold)
        {
            if (clearance < 0.0)
            {
                return LERCompareTerrainClassification.AboveTerrain;
            }

            return clearance <= threshold + 1e-6
                ? LERCompareTerrainClassification.LessOrEqualThreshold
                : LERCompareTerrainClassification.MoreThanThreshold;
        }

        private static double MoveInside(double boundaryParameter, double otherParameter)
        {
            double parameter = boundaryParameter + ((otherParameter - boundaryParameter) * 1e-6);
            if (Math.Abs(parameter - boundaryParameter) <= ParameterTolerance)
            {
                return (boundaryParameter + otherParameter) * 0.5;
            }

            return parameter;
        }

        private static void AddSpan(
            IList<LERCompareTerrainSpan> spans,
            LERCompareTerrainClassification classification,
            Point3d startPoint,
            Point3d endPoint)
        {
            if (startPoint.DistanceTo(endPoint) <= PointTolerance)
            {
                return;
            }

            spans.Add(new LERCompareTerrainSpan(classification, startPoint, endPoint));
        }

        private static bool HasPlanLength(Point3d startPoint, Point3d endPoint)
        {
            return new Vector2d(endPoint.X - startPoint.X, endPoint.Y - startPoint.Y).Length > PointTolerance;
        }

        private static double CalculatePlanParameter(Point3d startPoint, Point3d endPoint, Point3d samplePoint)
        {
            double dx = endPoint.X - startPoint.X;
            double dy = endPoint.Y - startPoint.Y;
            double planLengthSquared = (dx * dx) + (dy * dy);
            if (planLengthSquared <= PointTolerance)
            {
                return 0.0;
            }

            double sampleDx = samplePoint.X - startPoint.X;
            double sampleDy = samplePoint.Y - startPoint.Y;
            return ((sampleDx * dx) + (sampleDy * dy)) / planLengthSquared;
        }

        private static Point3d Interpolate(Point3d startPoint, Point3d endPoint, double parameter)
        {
            return new Point3d(
                startPoint.X + ((endPoint.X - startPoint.X) * parameter),
                startPoint.Y + ((endPoint.Y - startPoint.Y) * parameter),
                startPoint.Z + ((endPoint.Z - startPoint.Z) * parameter));
        }
    }

    internal sealed class LERCompareTerrainPreviewResult
    {
        public LERCompareTerrainPreviewResult(int analyzedPolylineCount, IReadOnlyList<LERCompareTerrainPiece> pieces)
        {
            AnalyzedPolylineCount = analyzedPolylineCount;
            Pieces = pieces.ToList();
        }

        public int AnalyzedPolylineCount { get; }

        public List<LERCompareTerrainPiece> Pieces { get; }

        public int AboveTerrainCount => Pieces.Count(piece => piece.Classification == LERCompareTerrainClassification.AboveTerrain);

        public int LessOrEqualCount => Pieces.Count(piece => piece.Classification == LERCompareTerrainClassification.LessOrEqualThreshold);

        public int MoreCount => Pieces.Count(piece => piece.Classification == LERCompareTerrainClassification.MoreThanThreshold);

        public int OutsideCount => Pieces.Count(piece => piece.Classification == LERCompareTerrainClassification.OutsideSurface);
    }

    internal sealed class LERCompareTerrainPieceBuilder
    {
        private const double SimplifyPointTolerance = 1e-6;
        private const double SimplifyDistanceTolerance = 1e-4;
        private readonly ObjectId _sourceId;
        private readonly List<LERCompareTerrainPiece> _pieces = new List<LERCompareTerrainPiece>();
        private LERCompareTerrainClassification? _currentClassification;
        private List<Point3d>? _currentPoints;

        public LERCompareTerrainPieceBuilder(ObjectId sourceId)
        {
            _sourceId = sourceId;
        }

        public void Append(LERCompareTerrainClassification classification, Point3d startPoint, Point3d endPoint)
        {
            if (startPoint.DistanceTo(endPoint) <= 1e-6)
            {
                return;
            }

            if (_currentClassification == classification
                && _currentPoints != null
                && _currentPoints.Count > 0
                && _currentPoints[^1].DistanceTo(startPoint) <= 1e-6)
            {
                if (_currentPoints[^1].DistanceTo(endPoint) > 1e-6)
                {
                    _currentPoints.Add(endPoint);
                }

                return;
            }

            Flush();
            _currentClassification = classification;
            _currentPoints = new List<Point3d> { startPoint, endPoint };
        }

        public IReadOnlyList<LERCompareTerrainPiece> Finish()
        {
            Flush();
            return _pieces;
        }

        private void Flush()
        {
            if (_currentClassification == null || _currentPoints == null || _currentPoints.Count < 2)
            {
                _currentClassification = null;
                _currentPoints = null;
                return;
            }

            List<Point3d> simplifiedPoints = SimplifyPoints(_currentPoints);
            if (simplifiedPoints.Count >= 2)
            {
                _pieces.Add(new LERCompareTerrainPiece(_sourceId, _currentClassification.Value, simplifiedPoints));
            }

            _currentClassification = null;
            _currentPoints = null;
        }

        private static List<Point3d> SimplifyPoints(IReadOnlyList<Point3d> points)
        {
            List<Point3d> distinctPoints = new List<Point3d>();
            foreach (Point3d point in points)
            {
                if (distinctPoints.Count == 0 || distinctPoints[^1].DistanceTo(point) > SimplifyPointTolerance)
                {
                    distinctPoints.Add(point);
                }
            }

            if (distinctPoints.Count <= 2)
            {
                return distinctPoints;
            }

            List<Point3d> simplifiedPoints = new List<Point3d> { distinctPoints[0] };
            for (int i = 1; i < distinctPoints.Count - 1; i++)
            {
                Point3d previousPoint = simplifiedPoints[^1];
                Point3d currentPoint = distinctPoints[i];
                Point3d nextPoint = distinctPoints[i + 1];

                if (CanRemovePoint(previousPoint, currentPoint, nextPoint))
                {
                    continue;
                }

                simplifiedPoints.Add(currentPoint);
            }

            simplifiedPoints.Add(distinctPoints[^1]);
            return simplifiedPoints;
        }

        private static bool CanRemovePoint(Point3d previousPoint, Point3d currentPoint, Point3d nextPoint)
        {
            Vector3d segmentVector = nextPoint - previousPoint;
            double lengthSquared = segmentVector.LengthSqrd;
            if (lengthSquared <= SimplifyPointTolerance * SimplifyPointTolerance)
            {
                return currentPoint.DistanceTo(previousPoint) <= SimplifyPointTolerance;
            }

            double parameter = (currentPoint - previousPoint).DotProduct(segmentVector) / lengthSquared;
            if (parameter <= 0.0 || parameter >= 1.0)
            {
                return false;
            }

            Point3d projectedPoint = previousPoint + (segmentVector * parameter);
            return currentPoint.DistanceTo(projectedPoint) <= SimplifyDistanceTolerance;
        }
    }

    internal sealed class LERCompareTerrainPiece
    {
        public LERCompareTerrainPiece(ObjectId sourceId, LERCompareTerrainClassification classification, IReadOnlyList<Point3d> points)
        {
            SourceId = sourceId;
            Classification = classification;
            Points = points.ToList();
        }

        public ObjectId SourceId { get; }

        public LERCompareTerrainClassification Classification { get; }

        public List<Point3d> Points { get; }
    }

    internal sealed class LERCompareTerrainSurfaceDescriptor
    {
        public LERCompareTerrainSurfaceDescriptor(ObjectId objectId, string name)
        {
            ObjectId = objectId;
            Name = name;
        }

        public ObjectId ObjectId { get; }

        public string Name { get; }
    }

    internal readonly struct LERCompareTerrainSpan
    {
        public LERCompareTerrainSpan(
            LERCompareTerrainClassification classification,
            Point3d startPoint,
            Point3d endPoint)
        {
            Classification = classification;
            StartPoint = startPoint;
            EndPoint = endPoint;
        }

        public LERCompareTerrainClassification Classification { get; }

        public Point3d StartPoint { get; }

        public Point3d EndPoint { get; }
    }

    internal readonly struct LERCompareTerrainEvaluation
    {
        private LERCompareTerrainEvaluation(Point3d point, double parameter, bool isOnSurface, double clearance)
        {
            Point = point;
            Parameter = parameter;
            IsOnSurface = isOnSurface;
            Clearance = clearance;
        }

        public Point3d Point { get; }

        public double Parameter { get; }

        public bool IsOnSurface { get; }

        public double Clearance { get; }

        public static LERCompareTerrainEvaluation OnSurface(Point3d point, double parameter, double clearance)
        {
            return new LERCompareTerrainEvaluation(point, parameter, true, clearance);
        }

        public static LERCompareTerrainEvaluation Outside(Point3d point, double parameter)
        {
            return new LERCompareTerrainEvaluation(point, parameter, false, double.NaN);
        }
    }

    internal enum LERCompareTerrainClassification
    {
        AboveTerrain,
        LessOrEqualThreshold,
        MoreThanThreshold,
        OutsideSurface
    }
}

