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
                        _control?.HideSurfacePreviews();
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
        private readonly Button _terrainToggleButton;
        private readonly Button _depthToggleButton;
        private readonly LERCompareTerrainSurfaceRenderer _surfaceRenderer = new LERCompareTerrainSurfaceRenderer();

        private Document? _document;
        private List<ObjectId> _selectedPolylineIds = new List<ObjectId>();
        private Database? _surfaceDatabase;
        private List<LERCompareTerrainSurfaceDescriptor> _surfaces = new List<LERCompareTerrainSurfaceDescriptor>();
        private string? _loadedSurfaceName;
        private LERCompareTerrainPreviewResult? _lastPreviewResult;
        private bool _terrainVisible;
        private bool _depthVisible;
        private List<(Point3d A, Point3d B, Point3d C)>? _cachedSurfaceTriangles;
        private List<LERCompareTerrainBbox2d>? _terrainFocusBboxes;
        private List<LERCompareTerrainBbox2d>? _depthFocusBboxes;
        private readonly HashSet<LERCompareTerrainClassification> _visibleClassifications =
            new HashSet<LERCompareTerrainClassification>
            {
                LERCompareTerrainClassification.AboveTerrain,
                LERCompareTerrainClassification.LessOrEqualThreshold,
                LERCompareTerrainClassification.MoreThanThreshold,
                LERCompareTerrainClassification.OutsideSurface,
                LERCompareTerrainClassification.TwoDPolyline
            };

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

            _terrainToggleButton = CreateButton("Preview Terrain");
            _terrainToggleButton.Click += (_, _) => ToggleTerrain();
            _terrainToggleButton.IsEnabled = false;

            _depthToggleButton = CreateButton("Preview Depth");
            _depthToggleButton.Click += (_, _) => ToggleDepth();
            _depthToggleButton.IsEnabled = false;

            surfaceSelectionPanel.Children.Add(_loadButton);
            surfaceSelectionPanel.Children.Add(_unloadButton);
            surfaceSelectionPanel.Children.Add(_terrainToggleButton);
            surfaceSelectionPanel.Children.Add(_depthToggleButton);
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

            Grid statusContainer = new Grid
            {
                Margin = new Thickness(0, 0, 0, 6)
            };
            statusContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            statusContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _statusTextBox = new TextBox
            {
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Margin = new Thickness(0, 0, 6, 0),
                Background = PanelBackgroundBrush,
                Foreground = ForegroundBrushValue,
                BorderBrush = BorderBrushValue,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 8, 10, 8)
            };
            Grid.SetColumn(_statusTextBox, 0);
            statusContainer.Children.Add(_statusTextBox);

            UIElement legendPanel = CreateLegendPanel();
            Grid.SetColumn(legendPanel, 1);
            statusContainer.Children.Add(legendPanel);

            AddToGrid(root, statusContainer, 5, 0, 3);

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

        public void HideSurfacePreviews()
        {
            if (!_terrainVisible && !_depthVisible) return;
            _surfaceRenderer.Clear();
            if (_terrainVisible)
            {
                _terrainVisible = false;
                _terrainFocusBboxes = null;
                _terrainToggleButton.Content = "Preview Terrain";
            }
            if (_depthVisible)
            {
                _depthVisible = false;
                _depthFocusBboxes = null;
                _depthToggleButton.Content = "Preview Depth";
            }
        }

        private void ToggleTerrain()
        {
            if (_terrainVisible)
            {
                _surfaceRenderer.HideTerrain();
                _terrainVisible = false;
                _terrainFocusBboxes = null;
                _terrainToggleButton.Content = "Preview Terrain";
                return;
            }

            if (!EnsureSurfaceCacheBuilt())
            {
                return;
            }

            List<LERCompareTerrainBbox2d>? bboxes = PromptForFocusBboxes();
            if (bboxes == null || bboxes.Count == 0)
            {
                return;
            }

            double buffer = GetThreshold() * 2.0;
            _terrainFocusBboxes = bboxes;
            _surfaceRenderer.ShowTerrain(FilterTrianglesByBboxes(_cachedSurfaceTriangles!, bboxes, buffer));
            _terrainVisible = true;
            _terrainToggleButton.Content = "Hide Terrain";
        }

        private void ToggleDepth()
        {
            if (_depthVisible)
            {
                _surfaceRenderer.HideDepth();
                _depthVisible = false;
                _depthFocusBboxes = null;
                _depthToggleButton.Content = "Preview Depth";
                return;
            }

            if (!EnsureSurfaceCacheBuilt())
            {
                return;
            }

            List<LERCompareTerrainBbox2d>? bboxes = PromptForFocusBboxes();
            if (bboxes == null || bboxes.Count == 0)
            {
                return;
            }

            double threshold = GetThreshold();
            _depthFocusBboxes = bboxes;
            _surfaceRenderer.ShowDepth(FilterTrianglesByBboxes(_cachedSurfaceTriangles!, bboxes, threshold * 2.0), -threshold);
            _depthVisible = true;
            _depthToggleButton.Content = "Hide Depth";
        }

        private bool EnsureSurfaceCacheBuilt()
        {
            if (_cachedSurfaceTriangles != null)
            {
                return true;
            }

            if (_surfaceDatabase == null || _surfaceComboBox.SelectedIndex < 0)
            {
                UpdateStatus("Load a TIN surface first.");
                return false;
            }

            try
            {
                using Transaction tx = _surfaceDatabase.TransactionManager.StartTransaction();
                LERCompareTerrainSurfaceDescriptor descriptor = _surfaces[_surfaceComboBox.SelectedIndex];
                TinSurface? surface = tx.GetObject(descriptor.ObjectId, OpenMode.ForRead) as TinSurface;
                if (surface == null)
                {
                    tx.Commit();
                    UpdateStatus("Surface preview unavailable: TIN could not be opened.");
                    return false;
                }

                List<(Point3d, Point3d, Point3d)> triangles = new List<(Point3d, Point3d, Point3d)>();
                TinSurfaceTriangleCollection sourceTriangles = surface.GetTriangles(false);
                try
                {
                    foreach (TinSurfaceTriangle triangle in sourceTriangles)
                    {
                        triangles.Add((triangle.Vertex1.Location, triangle.Vertex2.Location, triangle.Vertex3.Location));
                        triangle.Dispose();
                    }
                }
                finally
                {
                    sourceTriangles.Dispose();
                }

                tx.Commit();
                _cachedSurfaceTriangles = triangles;
                return true;
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                UpdateStatus("Failed to extract surface triangles. See debug output for details.");
                return false;
            }
        }

        private List<LERCompareTerrainBbox2d>? PromptForFocusBboxes()
        {
            if (_document == null)
            {
                UpdateStatus("No active document is attached to the palette.");
                return null;
            }

            try
            {
                Editor editor = _document.Editor;
                PromptSelectionOptions options = new PromptSelectionOptions
                {
                    MessageForAdding = "\nSelect objects where the terrain will be previewed: "
                };

                PromptSelectionResult selectionResult = editor.GetSelection(options);
                if (selectionResult.Status != PromptStatus.OK || selectionResult.Value == null || selectionResult.Value.Count == 0)
                {
                    UpdateStatus("Selection cancelled or empty.");
                    return null;
                }

                using DocumentLock docLock = _document.LockDocument();
                using Transaction tx = _document.Database.TransactionManager.StartTransaction();

                List<LERCompareTerrainBbox2d> bboxes = new List<LERCompareTerrainBbox2d>();
                foreach (ObjectId id in selectionResult.Value.GetObjectIds())
                {
                    if (tx.GetObject(id, OpenMode.ForRead, false) is not AcEntity entity) continue;
                    Extents3d? extents = entity.Bounds;
                    if (!extents.HasValue) continue;
                    Extents3d e = extents.Value;
                    bboxes.Add(new LERCompareTerrainBbox2d(e.MinPoint.X, e.MinPoint.Y, e.MaxPoint.X, e.MaxPoint.Y));
                }
                tx.Commit();

                if (bboxes.Count == 0)
                {
                    UpdateStatus("Selected objects have no bounding boxes available.");
                    return null;
                }

                return bboxes;
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                UpdateStatus("Selection failed. See debug output for details.");
                return null;
            }
        }

        private static List<(Point3d A, Point3d B, Point3d C)> FilterTrianglesByBboxes(
            IReadOnlyList<(Point3d A, Point3d B, Point3d C)> triangles,
            IReadOnlyList<LERCompareTerrainBbox2d> bboxes,
            double buffer)
        {
            List<(Point3d A, Point3d B, Point3d C)> filtered = new List<(Point3d, Point3d, Point3d)>();
            foreach ((Point3d a, Point3d b, Point3d c) in triangles)
            {
                double trMinX = Math.Min(a.X, Math.Min(b.X, c.X));
                double trMaxX = Math.Max(a.X, Math.Max(b.X, c.X));
                double trMinY = Math.Min(a.Y, Math.Min(b.Y, c.Y));
                double trMaxY = Math.Max(a.Y, Math.Max(b.Y, c.Y));

                foreach (LERCompareTerrainBbox2d bbox in bboxes)
                {
                    if (trMaxX < bbox.MinX - buffer || trMinX > bbox.MaxX + buffer) continue;
                    if (trMaxY < bbox.MinY - buffer || trMinY > bbox.MaxY + buffer) continue;
                    filtered.Add((a, b, c));
                    break;
                }
            }
            return filtered;
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
            _surfaceRenderer.Clear();
            _terrainVisible = false;
            _depthVisible = false;
            _terrainToggleButton.Content = "Preview Terrain";
            _depthToggleButton.Content = "Preview Depth";
            _terrainFocusBboxes = null;
            _depthFocusBboxes = null;
            _cachedSurfaceTriangles = null;
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

        private bool TryEnsurePreviewResult()
        {
            if (_lastPreviewResult != null && _lastPreviewResult.Pieces.Count > 0)
            {
                return true;
            }

            RefreshPreview();
            return _lastPreviewResult != null && _lastPreviewResult.Pieces.Count > 0;
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

                _lastPreviewResult = result;
                RenderFilteredPreview();

                drawingTx.Commit();
                surfaceTx.Commit();

                UpdateStatus(BuildPreviewStatus(result, _loadedSurfaceName ?? descriptor.Name, threshold));

                double buffer = threshold * 2.0;
                if (_terrainVisible && _cachedSurfaceTriangles != null && _terrainFocusBboxes != null)
                {
                    _surfaceRenderer.ShowTerrain(FilterTrianglesByBboxes(_cachedSurfaceTriangles, _terrainFocusBboxes, buffer));
                }

                if (_depthVisible && _cachedSurfaceTriangles != null && _depthFocusBboxes != null)
                {
                    _surfaceRenderer.ShowDepth(FilterTrianglesByBboxes(_cachedSurfaceTriangles, _depthFocusBboxes, buffer), -threshold);
                }
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

            if (!TryEnsurePreviewResult())
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
                    LERCompareTerrainColors.GetCadColor(LERCompareTerrainClassification.AboveTerrain));
                EnsureLayerExists(
                    LERCompareTerrainLayerNames.BuildWithinLayerName(GetThreshold()),
                    tx,
                    _document.Database,
                    LERCompareTerrainColors.GetCadColor(LERCompareTerrainClassification.LessOrEqualThreshold));
                EnsureLayerExists(
                    LERCompareTerrainLayerNames.BuildDeeperLayerName(GetThreshold()),
                    tx,
                    _document.Database,
                    LERCompareTerrainColors.GetCadColor(LERCompareTerrainClassification.MoreThanThreshold));
                EnsureLayerExists(
                    LERCompareTerrainLayerNames.OutsideLayerName,
                    tx,
                    _document.Database,
                    LERCompareTerrainColors.GetCadColor(LERCompareTerrainClassification.OutsideSurface));
                EnsureLayerExists(
                    LERCompareTerrainLayerNames.TwoDPolylineLayerName,
                    tx,
                    _document.Database,
                    LERCompareTerrainColors.GetCadColor(LERCompareTerrainClassification.TwoDPolyline));

                ClearExistingExportedGeometry(
                    modelSpace,
                    tx,
                    new[]
                    {
                        LERCompareTerrainLayerNames.AboveTerrainLayerName,
                        LERCompareTerrainLayerNames.BuildWithinLayerName(GetThreshold()),
                        LERCompareTerrainLayerNames.BuildDeeperLayerName(GetThreshold()),
                        LERCompareTerrainLayerNames.OutsideLayerName,
                        LERCompareTerrainLayerNames.TwoDPolylineLayerName
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
                    + $"{LERCompareTerrainLayerNames.BuildWithinLayerName(GetThreshold())}, "
                    + $"{LERCompareTerrainLayerNames.BuildDeeperLayerName(GetThreshold())}, "
                    + $"{LERCompareTerrainLayerNames.OutsideLayerName}, and "
                    + $"{LERCompareTerrainLayerNames.TwoDPolylineLayerName}.");
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

            bool surfaceLoaded = _surfaceDatabase != null;
            _terrainToggleButton.IsEnabled = surfaceLoaded;
            _depthToggleButton.IsEnabled = surfaceLoaded;
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

        private UIElement CreateLegendPanel()
        {
            StackPanel rows = new StackPanel { Orientation = Orientation.Vertical };
            rows.Children.Add(new WinLabel
            {
                Text = "Legend (toggle to filter)",
                FontWeight = FontWeights.Bold,
                Foreground = ForegroundBrushValue,
                Margin = new Thickness(0, 0, 0, 6)
            });
            rows.Children.Add(CreateLegendRow(LERCompareTerrainColors.GetMediaColor(LERCompareTerrainClassification.AboveTerrain), "Above terrain", LERCompareTerrainClassification.AboveTerrain));
            rows.Children.Add(CreateLegendRow(LERCompareTerrainColors.GetMediaColor(LERCompareTerrainClassification.LessOrEqualThreshold), "Within threshold", LERCompareTerrainClassification.LessOrEqualThreshold));
            rows.Children.Add(CreateLegendRow(LERCompareTerrainColors.GetMediaColor(LERCompareTerrainClassification.MoreThanThreshold), "Deeper than threshold", LERCompareTerrainClassification.MoreThanThreshold));
            rows.Children.Add(CreateLegendRow(LERCompareTerrainColors.GetMediaColor(LERCompareTerrainClassification.TwoDPolyline), "2D polyline (Z = -99)", LERCompareTerrainClassification.TwoDPolyline));
            rows.Children.Add(CreateLegendRow(LERCompareTerrainColors.GetMediaColor(LERCompareTerrainClassification.OutsideSurface), "Outside surface", LERCompareTerrainClassification.OutsideSurface));

            return new Border
            {
                Background = PanelBackgroundBrush,
                BorderBrush = BorderBrushValue,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 8, 10, 8),
                CornerRadius = new CornerRadius(2),
                MinWidth = 200,
                Child = rows
            };
        }

        private UIElement CreateLegendRow(System.Windows.Media.Color color, string label, LERCompareTerrainClassification classification)
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
                RenderFilteredPreview();
            };
            checkBox.Unchecked += (_, _) =>
            {
                _visibleClassifications.Remove(classification);
                RenderFilteredPreview();
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

        private void RenderFilteredPreview()
        {
            if (_lastPreviewResult == null)
            {
                _renderer.Clear();
                return;
            }

            List<LERCompareTerrainPiece> visiblePieces = _lastPreviewResult.Pieces
                .Where(piece => _visibleClassifications.Contains(piece.Classification))
                .ToList();
            _renderer.Show(visiblePieces);
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
            string status =
                $"Surface: {surfaceName}{Environment.NewLine}"
                + $"Threshold: {threshold.ToString("0.###", CultureInfo.InvariantCulture)} m{Environment.NewLine}"
                + $"Source 3D polylines analyzed: {result.AnalyzedPolylineCount}{Environment.NewLine}"
                + $"Preview pieces: {result.Pieces.Count}{Environment.NewLine}"
                + $"Above terrain pieces: {result.AboveTerrainCount}{Environment.NewLine}"
                + $"Within threshold pieces: {result.LessOrEqualCount}{Environment.NewLine}"
                + $"Deeper than threshold pieces: {result.MoreCount}{Environment.NewLine}"
                + $"Outside pieces: {result.OutsideCount}{Environment.NewLine}"
                + $"2D polyline pieces: {result.TwoDPolylineCount}";

            if (result.SuspectIntervalCount > 0)
            {
                status += Environment.NewLine
                    + $"Suspect intervals (surface query failed mid-span): {result.SuspectIntervalCount}";
            }

            return status;
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

    internal static class LERCompareTerrainColors
    {
        // High-contrast colorblind-safe palette. Anchors are Paul Tol "vibrant" hues (red, teal, blue) plus a vivid violet
        // for AboveTerrain and a saturated yellow for OutsideSurface. Higher saturation than Okabe-Ito for readability on
        // AutoCAD's black background; hue separation preserved under deuteranopia, protanopia, and tritanopia.
        // Drives the WPF legend, transient preview, and baked layer colors from one source.
        private static (byte R, byte G, byte B) GetRgb(LERCompareTerrainClassification classification)
        {
            return classification switch
            {
                LERCompareTerrainClassification.AboveTerrain => (156, 70, 255),
                LERCompareTerrainClassification.LessOrEqualThreshold => (0, 153, 136),
                LERCompareTerrainClassification.MoreThanThreshold => (204, 51, 17),
                LERCompareTerrainClassification.TwoDPolyline => (0, 119, 187),
                _ => (238, 221, 0)
            };
        }

        public static CadColor GetCadColor(LERCompareTerrainClassification classification)
        {
            (byte r, byte g, byte b) = GetRgb(classification);
            return CadColor.FromColor(System.Drawing.Color.FromArgb(r, g, b));
        }

        public static System.Windows.Media.Color GetMediaColor(LERCompareTerrainClassification classification)
        {
            (byte r, byte g, byte b) = GetRgb(classification);
            return System.Windows.Media.Color.FromRgb(r, g, b);
        }
    }

    internal static class LERCompareTerrainLayerNames
    {
        public const string AboveTerrainLayerName = "0 - above Terrain";
        public const string OutsideLayerName = "0 - Outside segment";
        public const string TwoDPolylineLayerName = "0 - 2D polyline";

        public static string BuildWithinLayerName(double threshold)
        {
            return $"0 - within {FormatThreshold(threshold)}m";
        }

        public static string BuildDeeperLayerName(double threshold)
        {
            return $"0 - deeper than {FormatThreshold(threshold)}m";
        }

        public static string GetLayerName(LERCompareTerrainClassification classification, double threshold)
        {
            return classification switch
            {
                LERCompareTerrainClassification.AboveTerrain => AboveTerrainLayerName,
                LERCompareTerrainClassification.LessOrEqualThreshold => BuildWithinLayerName(threshold),
                LERCompareTerrainClassification.MoreThanThreshold => BuildDeeperLayerName(threshold),
                LERCompareTerrainClassification.TwoDPolyline => TwoDPolylineLayerName,
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

                Polyline3d previewPolyline = new Polyline3d(
                    Poly3dType.SimplePoly,
                    new Point3dCollection(piece.Points.ToArray()),
                    false)
                {
                    Color = LERCompareTerrainColors.GetCadColor(piece.Classification)
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
    }

    internal sealed class LERCompareTerrainSurfaceRenderer
    {
        private static readonly CadColor TerrainColor = CadColor.FromColor(System.Drawing.Color.FromArgb(0x88, 0x88, 0x99));
        private static readonly CadColor DepthColor = CadColor.FromColor(System.Drawing.Color.FromArgb(0xCC, 0x99, 0x66));
        private readonly TransientManager _transientManager = TransientManager.CurrentTransientManager;
        private readonly List<AcEntity> _terrainEntities = new List<AcEntity>();
        private readonly List<AcEntity> _depthEntities = new List<AcEntity>();

        public void ShowTerrain(IReadOnlyList<(Point3d A, Point3d B, Point3d C)> triangles)
        {
            HideTerrain();
            Populate(_terrainEntities, triangles, 0.0, TerrainColor);
        }

        public void ShowDepth(IReadOnlyList<(Point3d A, Point3d B, Point3d C)> triangles, double zOffset)
        {
            HideDepth();
            Populate(_depthEntities, triangles, zOffset, DepthColor);
        }

        public void HideTerrain()
        {
            Erase(_terrainEntities);
        }

        public void HideDepth()
        {
            Erase(_depthEntities);
        }

        public void Clear()
        {
            HideTerrain();
            HideDepth();
        }

        private void Populate(
            List<AcEntity> bucket,
            IReadOnlyList<(Point3d A, Point3d B, Point3d C)> triangles,
            double zOffset,
            CadColor color)
        {
            if (triangles.Count == 0)
            {
                return;
            }

            Point3dCollection vertexArray = new Point3dCollection();
            Autodesk.AutoCAD.Geometry.Int32Collection faceArray = new Autodesk.AutoCAD.Geometry.Int32Collection();

            foreach ((Point3d a, Point3d b, Point3d c) in triangles)
            {
                int i0 = vertexArray.Count;
                vertexArray.Add(new Point3d(a.X, a.Y, a.Z + zOffset));
                int i1 = vertexArray.Count;
                vertexArray.Add(new Point3d(b.X, b.Y, b.Z + zOffset));
                int i2 = vertexArray.Count;
                vertexArray.Add(new Point3d(c.X, c.Y, c.Z + zOffset));

                faceArray.Add(3);
                faceArray.Add(i0);
                faceArray.Add(i1);
                faceArray.Add(i2);
            }

            SubDMesh mesh = new SubDMesh();
            mesh.SetSubDMesh(vertexArray, faceArray, 0);
            mesh.Color = color;

            bucket.Add(mesh);
            _transientManager.AddTransient(
                mesh,
                TransientDrawingMode.DirectShortTerm,
                128,
                new IntegerCollection());
        }

        private void Erase(List<AcEntity> bucket)
        {
            foreach (AcEntity entity in bucket)
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

            bucket.Clear();
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
            int suspectIntervals = 0;

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
                pieces.AddRange(AnalyzePolyline(polylineId, vertices, surface, threshold, ref suspectIntervals));
            }

            return new LERCompareTerrainPreviewResult(analyzedPolylineCount, pieces, suspectIntervals);
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
            double threshold,
            ref int suspectIntervals)
        {
            if (IsTwoDimensionalPolyline(vertices))
            {
                return new[]
                {
                    new LERCompareTerrainPiece(
                        sourceId,
                        LERCompareTerrainClassification.TwoDPolyline,
                        vertices.ToList())
                };
            }

            LERCompareTerrainPieceBuilder builder = new LERCompareTerrainPieceBuilder(sourceId);

            for (int i = 0; i < vertices.Count - 1; i++)
            {
                Point3d startPoint = vertices[i];
                Point3d endPoint = vertices[i + 1];
                foreach (LERCompareTerrainSpan span in AnalyzeSegment(startPoint, endPoint, surface, threshold, ref suspectIntervals))
                {
                    builder.Append(span.Classification, span.StartPoint, span.EndPoint);
                }
            }

            return builder.Finish();
        }

        private static bool IsTwoDimensionalPolyline(IReadOnlyList<Point3d> vertices)
        {
            if (vertices.Count == 0)
            {
                return false;
            }

            foreach (Point3d vertex in vertices)
            {
                if (Math.Abs(vertex.Z - (-99.0)) > 1e-6)
                {
                    return false;
                }
            }

            return true;
        }

        private static IReadOnlyList<LERCompareTerrainSpan> AnalyzeSegment(
            Point3d startPoint,
            Point3d endPoint,
            TinSurface surface,
            double threshold,
            ref int suspectIntervals)
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
                    ProcessMidOutsideInterval(startPoint, endPoint, surface, threshold, intervalStart, intervalEnd, startEval, endEval, spans, ref suspectIntervals);
                }
                else
                {
                    ProcessMidInsideInterval(startPoint, endPoint, surface, threshold, intervalStart, intervalEnd, startEval, endEval, midEval, spans, ref suspectIntervals);
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
            IList<LERCompareTerrainSpan> spans,
            ref int suspectIntervals)
        {
            if (!startEval.IsOnSurface && !endEval.IsOnSurface)
            {
                AddSpan(spans, LERCompareTerrainClassification.OutsideSurface, Interpolate(startPoint, endPoint, intervalStart), Interpolate(startPoint, endPoint, intervalEnd));
                return;
            }

            if (startEval.IsOnSurface && !endEval.IsOnSurface)
            {
                double boundary = FindSurfaceBoundaryParameter(startPoint, endPoint, surface, intervalStart, intervalEnd);
                AddThresholdAwareInsideSpan(startPoint, endPoint, surface, threshold, intervalStart, boundary, spans, ref suspectIntervals);
                AddSpan(spans, LERCompareTerrainClassification.OutsideSurface, Interpolate(startPoint, endPoint, boundary), Interpolate(startPoint, endPoint, intervalEnd));
                return;
            }

            if (!startEval.IsOnSurface && endEval.IsOnSurface)
            {
                double boundary = FindSurfaceBoundaryParameter(startPoint, endPoint, surface, intervalEnd, intervalStart);
                AddSpan(spans, LERCompareTerrainClassification.OutsideSurface, Interpolate(startPoint, endPoint, intervalStart), Interpolate(startPoint, endPoint, boundary));
                AddThresholdAwareInsideSpan(startPoint, endPoint, surface, threshold, boundary, intervalEnd, spans, ref suspectIntervals);
                return;
            }

            double leftBoundary = FindSurfaceBoundaryParameter(startPoint, endPoint, surface, intervalStart, (intervalStart + intervalEnd) * 0.5);
            double rightBoundary = FindSurfaceBoundaryParameter(startPoint, endPoint, surface, intervalEnd, (intervalStart + intervalEnd) * 0.5);

            if (rightBoundary < leftBoundary)
            {
                (leftBoundary, rightBoundary) = (rightBoundary, leftBoundary);
            }

            AddThresholdAwareInsideSpan(startPoint, endPoint, surface, threshold, intervalStart, leftBoundary, spans, ref suspectIntervals);
            AddSpan(spans, LERCompareTerrainClassification.OutsideSurface, Interpolate(startPoint, endPoint, leftBoundary), Interpolate(startPoint, endPoint, rightBoundary));
            AddThresholdAwareInsideSpan(startPoint, endPoint, surface, threshold, rightBoundary, intervalEnd, spans, ref suspectIntervals);
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
            IList<LERCompareTerrainSpan> spans,
            ref int suspectIntervals)
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

            AddThresholdAwareInsideSpan(startPoint, endPoint, surface, threshold, insideStart, insideEnd, spans, ref suspectIntervals);
        }

        private static void AddThresholdAwareInsideSpan(
            Point3d startPoint,
            Point3d endPoint,
            TinSurface surface,
            double threshold,
            double intervalStart,
            double intervalEnd,
            IList<LERCompareTerrainSpan> spans,
            ref int suspectIntervals)
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

            double sampleMid = (sampleStart + sampleEnd) * 0.5;
            LERCompareTerrainEvaluation midEval = Evaluate(startPoint, endPoint, surface, sampleMid);

            if (midEval.IsOnSurface)
            {
                LERCompareTerrainClassification midClass = Classify(midEval.Clearance, threshold);
                if (startClass != midClass && midClass != endClass && startClass != endClass)
                {
                    double leftBoundary = FindThresholdBoundaryParameter(
                        startPoint,
                        endPoint,
                        surface,
                        threshold,
                        sampleStart,
                        sampleMid,
                        startClass);
                    double rightBoundary = FindThresholdBoundaryParameter(
                        startPoint,
                        endPoint,
                        surface,
                        threshold,
                        sampleMid,
                        sampleEnd,
                        midClass);

                    AddSpan(spans, startClass, Interpolate(startPoint, endPoint, intervalStart), Interpolate(startPoint, endPoint, leftBoundary));
                    AddSpan(spans, midClass, Interpolate(startPoint, endPoint, leftBoundary), Interpolate(startPoint, endPoint, rightBoundary));
                    AddSpan(spans, endClass, Interpolate(startPoint, endPoint, rightBoundary), Interpolate(startPoint, endPoint, intervalEnd));
                    return;
                }
            }
            else
            {
                // Both endpoints sit on the surface, but the midpoint does not — geometrically impossible inside a single
                // convex TIN triangle, so this signals either a TIN hole that SampleElevations did not break the segment
                // at, or numerical edge-case behavior from FindElevationAtXY. The single-bisection fallback below can
                // still return a fabricated split parameter in that case (Codex review HIGH #2). Surfacing the count to
                // the user is the diagnostic; the underlying hole-handling bug is intentionally left for a separate pass.
                suspectIntervals++;
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
        public LERCompareTerrainPreviewResult(int analyzedPolylineCount, IReadOnlyList<LERCompareTerrainPiece> pieces, int suspectIntervalCount)
        {
            AnalyzedPolylineCount = analyzedPolylineCount;
            Pieces = pieces.ToList();
            SuspectIntervalCount = suspectIntervalCount;
        }

        public int AnalyzedPolylineCount { get; }

        public List<LERCompareTerrainPiece> Pieces { get; }

        public int SuspectIntervalCount { get; }

        public int AboveTerrainCount => Pieces.Count(piece => piece.Classification == LERCompareTerrainClassification.AboveTerrain);

        public int LessOrEqualCount => Pieces.Count(piece => piece.Classification == LERCompareTerrainClassification.LessOrEqualThreshold);

        public int MoreCount => Pieces.Count(piece => piece.Classification == LERCompareTerrainClassification.MoreThanThreshold);

        public int OutsideCount => Pieces.Count(piece => piece.Classification == LERCompareTerrainClassification.OutsideSurface);

        public int TwoDPolylineCount => Pieces.Count(piece => piece.Classification == LERCompareTerrainClassification.TwoDPolyline);
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

    internal readonly record struct LERCompareTerrainBbox2d(double MinX, double MinY, double MaxX, double MaxY);

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
        OutsideSurface,
        TwoDPolyline
    }
}

