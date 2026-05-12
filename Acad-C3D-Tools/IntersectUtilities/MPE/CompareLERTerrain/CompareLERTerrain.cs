using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
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
using WinLabel = System.Windows.Forms.Label;

namespace IntersectUtilities
{
    public partial class Intersect
    {
        private const string CompareLerTerrainCommandName = "CompareLERTerrain";

        /// <command>CompareLERTerrain</command>
        /// <summary>
        /// Opens an MPE palette for comparing selected 3D LER polylines against a TIN terrain surface loaded from an
        /// external DWG. The tool previews copied 3D pipe segments classified by vertical surface clearance relative to a
        /// user-defined meter threshold and can bake the preview into layer-separated 3D polylines for "less", "more",
        /// and "outside surface" segments. The preview is drawn on the source 3D polyline geometry, not on the terrain.
        /// Re-run the command to change which 3D polylines are included in the palette session.
        /// </summary>
        /// <category>MPE</category>
        [CommandMethod(CompareLerTerrainCommandName, CommandFlags.Modal)]
        public void CompareLERTerrain()
        {
            DocumentCollection docCol = AcadApp.DocumentManager;
            Document doc = docCol.MdiActiveDocument;
            Editor editor = doc.Editor;
            Database localDb = doc.Database;

            PromptSelectionOptions options = new PromptSelectionOptions
            {
                MessageForAdding = "\nSelect 3D polylines to compare against terrain: "
            };

            SelectionFilter filter = new SelectionFilter(
                new[]
                {
                    new TypedValue((int)DxfCode.Start, "POLYLINE")
                });

            PromptSelectionResult selectionResult = editor.GetSelection(options, filter);
            if (selectionResult.Status != PromptStatus.OK || selectionResult.Value is null || selectionResult.Value.Count == 0)
            {
                editor.WriteMessage("\nNo 3D polylines selected.");
                return;
            }

            using Transaction tx = localDb.TransactionManager.StartTransaction();
            try
            {
                List<ObjectId> polylineIds = new List<ObjectId>();
                foreach (ObjectId objectId in selectionResult.Value.GetObjectIds())
                {
                    if (tx.GetObject(objectId, OpenMode.ForRead) is Polyline3d)
                    {
                        polylineIds.Add(objectId);
                    }
                }

                if (polylineIds.Count == 0)
                {
                    editor.WriteMessage("\nSelection did not contain any 3D polylines.");
                    tx.Commit();
                    return;
                }

                CompareLerTerrainPaletteHost.Show(doc, polylineIds);
                editor.WriteMessage($"\n{CompareLerTerrainCommandName} loaded {polylineIds.Count} 3D polylines into the palette.");
                tx.Commit();
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                tx.Abort();
                editor.WriteMessage($"\n{CompareLerTerrainCommandName} failed. See debug output for details.");
                return;
            }
        }
    }

    internal static class CompareLerTerrainPaletteHost
    {
        private static PaletteSet? _palette;
        private static CompareLerTerrainControl? _control;

        public static void Show(Document document, IReadOnlyList<ObjectId> polylineIds)
        {
            if (_palette == null)
            {
                _control = new CompareLerTerrainControl();
                _palette = new PaletteSet(
                    "Compare LER Terrain",
                    "COMPARE_LER_TERRAIN",
                    new Guid("5E98E32A-B975-4E2E-B313-4B6DCC932E82"))
                {
                    Style = PaletteSetStyles.ShowPropertiesMenu | PaletteSetStyles.ShowCloseButton,
                    MinimumSize = new Size(360, 260),
                    Size = new Size(460, 520)
                };

                _palette.Add("Compare", _control);
                _palette.DockEnabled = DockSides.Left | DockSides.Right | DockSides.None;
                _palette.StateChanged += (_, _) =>
                {
                    if (_palette != null && !_palette.Visible)
                    {
                        _control?.ClearPreview();
                    }
                };
            }

            _control!.SetSelection(document, polylineIds);
            _palette.Visible = true;
        }
    }

    internal sealed class CompareLerTerrainControl : UserControl
    {
        private readonly CompareLerTerrainTransientRenderer _renderer = new CompareLerTerrainTransientRenderer();
        private readonly TextBox _surfacePathTextBox;
        private readonly ComboBox _surfaceComboBox;
        private readonly NumericUpDown _thresholdNumericUpDown;
        private readonly WinLabel _selectionLabel;
        private readonly TextBox _statusTextBox;
        private readonly Button _loadButton;
        private readonly Button _previewButton;
        private readonly Button _bakeButton;
        private readonly Button _clearButton;

        private Document? _document;
        private List<ObjectId> _selectedPolylineIds = new List<ObjectId>();
        private Database? _surfaceDatabase;
        private List<CompareLerTerrainSurfaceDescriptor> _surfaces = new List<CompareLerTerrainSurfaceDescriptor>();
        private CompareLerTerrainPreviewResult? _lastPreviewResult;

        public CompareLerTerrainControl()
        {
            Dock = DockStyle.Fill;

            TableLayoutPanel root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 7,
                Padding = new Padding(10)
            };

            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            root.Controls.Add(new WinLabel
            {
                AutoSize = true,
                Text = "Surface DWG",
                Anchor = AnchorStyles.Left
            }, 0, 0);

            _surfacePathTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true
            };
            root.Controls.Add(_surfacePathTextBox, 1, 0);

            Button browseButton = new Button
            {
                Text = "Browse...",
                Dock = DockStyle.Fill
            };
            browseButton.Click += (_, _) => BrowseForSurfaceDwg();
            root.Controls.Add(browseButton, 2, 0);

            root.Controls.Add(new WinLabel
            {
                AutoSize = true,
                Text = "TIN Surface",
                Anchor = AnchorStyles.Left
            }, 0, 1);

            _surfaceComboBox = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _surfaceComboBox.SelectedIndexChanged += (_, _) => RefreshPreviewIfReady();
            root.Controls.Add(_surfaceComboBox, 1, 1);
            root.SetColumnSpan(_surfaceComboBox, 2);

            root.Controls.Add(new WinLabel
            {
                AutoSize = true,
                Text = "Threshold (m)",
                Anchor = AnchorStyles.Left
            }, 0, 2);

            _thresholdNumericUpDown = new NumericUpDown
            {
                DecimalPlaces = 3,
                Increment = 0.100M,
                Minimum = 0.001M,
                Maximum = 1000M,
                Value = 2.500M,
                Dock = DockStyle.Left,
                Width = 120
            };
            _thresholdNumericUpDown.ValueChanged += (_, _) => RefreshPreviewIfReady();
            root.Controls.Add(_thresholdNumericUpDown, 1, 2);

            _selectionLabel = new WinLabel
            {
                AutoSize = true,
                Text = "Selected 3D polylines: 0",
                Anchor = AnchorStyles.Left
            };
            root.Controls.Add(_selectionLabel, 0, 3);
            root.SetColumnSpan(_selectionLabel, 3);

            FlowLayoutPanel buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight,
                WrapContents = false
            };

            _loadButton = new Button
            {
                Text = "Load Surface",
                AutoSize = true
            };
            _loadButton.Click += (_, _) => LoadSurfaceFromCurrentPath();

            _previewButton = new Button
            {
                Text = "Preview",
                AutoSize = true
            };
            _previewButton.Click += (_, _) => RefreshPreview();

            _bakeButton = new Button
            {
                Text = "Bake",
                AutoSize = true
            };
            _bakeButton.Click += (_, _) => BakePreview();

            _clearButton = new Button
            {
                Text = "Clear Preview",
                AutoSize = true
            };
            _clearButton.Click += (_, _) => ClearPreview();

            buttonPanel.Controls.Add(_loadButton);
            buttonPanel.Controls.Add(_previewButton);
            buttonPanel.Controls.Add(_bakeButton);
            buttonPanel.Controls.Add(_clearButton);
            root.Controls.Add(buttonPanel, 0, 4);
            root.SetColumnSpan(buttonPanel, 3);

            _statusTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical
            };
            root.Controls.Add(_statusTextBox, 0, 5);
            root.SetColumnSpan(_statusTextBox, 3);

            root.Controls.Add(new WinLabel
            {
                AutoSize = true,
                Text = "Preview uses the selected 3D polylines, not the terrain surface.",
                Anchor = AnchorStyles.Left
            }, 0, 6);
            root.SetColumnSpan(root.GetControlFromPosition(0, 6), 3);

            Controls.Add(root);
            UpdateStatus("Load a terrain DWG and select a TIN surface to start previewing.");
        }

        public void SetSelection(Document document, IReadOnlyList<ObjectId> polylineIds)
        {
            _document = document;
            _selectedPolylineIds = polylineIds.Distinct().ToList();
            _selectionLabel.Text = $"Selected 3D polylines: {_selectedPolylineIds.Count}";
            RefreshPreviewIfReady();
        }

        public void ClearPreview()
        {
            _renderer.Clear();
            _lastPreviewResult = null;
            UpdateStatus("Preview cleared.");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _renderer.Clear();
                _surfaceDatabase?.Dispose();
                _surfaceDatabase = null;
            }

            base.Dispose(disposing);
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

            if (dialog.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            _surfacePathTextBox.Text = dialog.FileName;
            LoadSurfaceFromCurrentPath();
        }

        private void LoadSurfaceFromCurrentPath()
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

            try
            {
                _surfaceDatabase?.Dispose();
                _surfaceDatabase = new Database(false, true);
                _surfaceDatabase.ReadDwgFile(path, FileOpenMode.OpenForReadAndAllShare, false, null);

                using Transaction tx = _surfaceDatabase.TransactionManager.StartTransaction();
                _surfaces = _surfaceDatabase
                    .HashSetOfType<TinSurface>(tx)
                    .Select(surface => new CompareLerTerrainSurfaceDescriptor(surface.ObjectId, surface.Name))
                    .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                tx.Commit();

                _surfaceComboBox.BeginUpdate();
                _surfaceComboBox.Items.Clear();
                foreach (CompareLerTerrainSurfaceDescriptor surface in _surfaces)
                {
                    _surfaceComboBox.Items.Add(surface.Name);
                }
                _surfaceComboBox.EndUpdate();

                if (_surfaces.Count == 0)
                {
                    UpdateStatus("No TinSurface entities were found in the selected DWG.");
                    return;
                }

                _surfaceComboBox.SelectedIndex = 0;
                UpdateStatus($"Loaded {_surfaces.Count} terrain surface(s) from {Path.GetFileName(path)}.");
                RefreshPreviewIfReady();
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                UpdateStatus("Failed to load the terrain DWG. See debug output for details.");
            }
        }

        private void RefreshPreviewIfReady()
        {
            if (_surfaceDatabase == null || _surfaceComboBox.SelectedIndex < 0 || _document == null || _selectedPolylineIds.Count == 0)
            {
                return;
            }

            RefreshPreview();
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
                UpdateStatus("Load a terrain DWG and select a TIN surface first.");
                return;
            }

            if (_selectedPolylineIds.Count == 0)
            {
                UpdateStatus("No 3D polylines are loaded in the palette. Re-run CompareLERTerrain.");
                return;
            }

            try
            {
                using DocumentLock docLock = _document.LockDocument();
                using Transaction drawingTx = _document.Database.TransactionManager.StartTransaction();
                using Transaction surfaceTx = _surfaceDatabase.TransactionManager.StartTransaction();

                CompareLerTerrainSurfaceDescriptor descriptor = _surfaces[_surfaceComboBox.SelectedIndex];
                TinSurface? surface = surfaceTx.GetObject(descriptor.ObjectId, OpenMode.ForRead) as TinSurface;
                if (surface == null)
                {
                    UpdateStatus("The selected TIN surface could not be opened.");
                    drawingTx.Commit();
                    surfaceTx.Commit();
                    return;
                }

                double threshold = GetThreshold();
                CompareLerTerrainPreviewResult result = CompareLerTerrainAnalyzer.Analyze(
                    drawingTx,
                    surface,
                    _selectedPolylineIds,
                    threshold);

                _renderer.Show(result.Pieces);
                _lastPreviewResult = result;

                drawingTx.Commit();
                surfaceTx.Commit();

                UpdateStatus(BuildPreviewStatus(result, descriptor.Name, threshold));
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
                    CompareLerTerrainLayerNames.BuildLessLayerName(GetThreshold()),
                    tx,
                    _document.Database,
                    CadColor.FromColorIndex(ColorMethod.ByAci, 1));
                EnsureLayerExists(
                    CompareLerTerrainLayerNames.BuildMoreLayerName(GetThreshold()),
                    tx,
                    _document.Database,
                    CadColor.FromColorIndex(ColorMethod.ByAci, 3));
                EnsureLayerExists(
                    CompareLerTerrainLayerNames.OutsideLayerName,
                    tx,
                    _document.Database,
                    CadColor.FromColorIndex(ColorMethod.ByAci, 2));

                int createdCount = 0;
                foreach (CompareLerTerrainPiece piece in _lastPreviewResult.Pieces)
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
                    bakedPolyline.Layer = CompareLerTerrainLayerNames.GetLayerName(piece.Classification, GetThreshold());
                    bakedPolyline.Color = CadColor.FromColorIndex(ColorMethod.ByLayer, 256);

                    modelSpace.AppendEntity(bakedPolyline);
                    tx.AddNewlyCreatedDBObject(bakedPolyline, true);
                    createdCount++;
                }

                tx.Commit();
                _renderer.Clear();
                UpdateStatus(
                    $"Bake complete. Created {createdCount} 3D polyline piece(s) on layers "
                    + $"{CompareLerTerrainLayerNames.BuildLessLayerName(GetThreshold())}, "
                    + $"{CompareLerTerrainLayerNames.BuildMoreLayerName(GetThreshold())}, and "
                    + $"{CompareLerTerrainLayerNames.OutsideLayerName}.");
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                UpdateStatus("Bake failed. See debug output for details.");
            }
        }

        private double GetThreshold()
        {
            return decimal.ToDouble(_thresholdNumericUpDown.Value);
        }

        private void UpdateStatus(string message)
        {
            _statusTextBox.Text = message;
        }

        private static string BuildPreviewStatus(
            CompareLerTerrainPreviewResult result,
            string surfaceName,
            double threshold)
        {
            return
                $"Surface: {surfaceName}{Environment.NewLine}"
                + $"Threshold: {threshold.ToString("0.###", CultureInfo.InvariantCulture)} m{Environment.NewLine}"
                + $"Source 3D polylines analyzed: {result.AnalyzedPolylineCount}{Environment.NewLine}"
                + $"Preview pieces: {result.Pieces.Count}{Environment.NewLine}"
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
    }

    internal static class CompareLerTerrainLayerNames
    {
        public const string OutsideLayerName = "0 - Outside segment";

        public static string BuildLessLayerName(double threshold)
        {
            return $"0 - less then {FormatThreshold(threshold)}";
        }

        public static string BuildMoreLayerName(double threshold)
        {
            return $"0 - more than {FormatThreshold(threshold)}m";
        }

        public static string GetLayerName(CompareLerTerrainClassification classification, double threshold)
        {
            return classification switch
            {
                CompareLerTerrainClassification.LessOrEqualThreshold => BuildLessLayerName(threshold),
                CompareLerTerrainClassification.MoreThanThreshold => BuildMoreLayerName(threshold),
                _ => OutsideLayerName
            };
        }

        private static string FormatThreshold(double threshold)
        {
            return threshold.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }

    internal sealed class CompareLerTerrainTransientRenderer
    {
        private readonly TransientManager _transientManager = TransientManager.CurrentTransientManager;
        private readonly List<AcEntity> _currentEntities = new List<AcEntity>();

        public void Show(IReadOnlyList<CompareLerTerrainPiece> pieces)
        {
            Clear();

            foreach (CompareLerTerrainPiece piece in pieces)
            {
                if (piece.Points.Count < 2)
                {
                    continue;
                }

                short colorIndex = GetColorIndex(piece.Classification);
                for (int i = 0; i < piece.Points.Count - 1; i++)
                {
                    Line line = new Line(piece.Points[i], piece.Points[i + 1])
                    {
                        Color = CadColor.FromColorIndex(ColorMethod.ByAci, colorIndex)
                    };

                    _currentEntities.Add(line);
                    _transientManager.AddTransient(
                        line,
                        TransientDrawingMode.DirectShortTerm,
                        128,
                        new IntegerCollection());
                }
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

        private static short GetColorIndex(CompareLerTerrainClassification classification)
        {
            return classification switch
            {
                CompareLerTerrainClassification.LessOrEqualThreshold => 1,
                CompareLerTerrainClassification.MoreThanThreshold => 3,
                _ => 2
            };
        }
    }

    internal static class CompareLerTerrainAnalyzer
    {
        private const double ParameterTolerance = 1e-8;
        private const double PointTolerance = 1e-6;

        public static CompareLerTerrainPreviewResult Analyze(
            Transaction drawingTransaction,
            TinSurface surface,
            IReadOnlyList<ObjectId> polylineIds,
            double threshold)
        {
            List<CompareLerTerrainPiece> pieces = new List<CompareLerTerrainPiece>();
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

            return new CompareLerTerrainPreviewResult(analyzedPolylineCount, pieces);
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

        private static IReadOnlyList<CompareLerTerrainPiece> AnalyzePolyline(
            ObjectId sourceId,
            IReadOnlyList<Point3d> vertices,
            TinSurface surface,
            double threshold)
        {
            CompareLerTerrainPieceBuilder builder = new CompareLerTerrainPieceBuilder(sourceId);

            for (int i = 0; i < vertices.Count - 1; i++)
            {
                Point3d startPoint = vertices[i];
                Point3d endPoint = vertices[i + 1];
                foreach (CompareLerTerrainSpan span in AnalyzeSegment(startPoint, endPoint, surface, threshold))
                {
                    builder.Append(span.Classification, span.StartPoint, span.EndPoint);
                }
            }

            return builder.Finish();
        }

        private static IReadOnlyList<CompareLerTerrainSpan> AnalyzeSegment(
            Point3d startPoint,
            Point3d endPoint,
            TinSurface surface,
            double threshold)
        {
            List<CompareLerTerrainSpan> spans = new List<CompareLerTerrainSpan>();
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

                CompareLerTerrainEvaluation startEval = Evaluate(startPoint, endPoint, surface, intervalStart);
                CompareLerTerrainEvaluation endEval = Evaluate(startPoint, endPoint, surface, intervalEnd);
                CompareLerTerrainEvaluation midEval = Evaluate(startPoint, endPoint, surface, (intervalStart + intervalEnd) * 0.5);

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
            CompareLerTerrainEvaluation startEval,
            CompareLerTerrainEvaluation endEval,
            IList<CompareLerTerrainSpan> spans)
        {
            if (!startEval.IsOnSurface && !endEval.IsOnSurface)
            {
                AddSpan(spans, CompareLerTerrainClassification.OutsideSurface, Interpolate(startPoint, endPoint, intervalStart), Interpolate(startPoint, endPoint, intervalEnd));
                return;
            }

            if (startEval.IsOnSurface && !endEval.IsOnSurface)
            {
                double boundary = FindSurfaceBoundaryParameter(startPoint, endPoint, surface, intervalStart, intervalEnd);
                AddThresholdAwareInsideSpan(startPoint, endPoint, surface, threshold, intervalStart, boundary, spans);
                AddSpan(spans, CompareLerTerrainClassification.OutsideSurface, Interpolate(startPoint, endPoint, boundary), Interpolate(startPoint, endPoint, intervalEnd));
                return;
            }

            if (!startEval.IsOnSurface && endEval.IsOnSurface)
            {
                double boundary = FindSurfaceBoundaryParameter(startPoint, endPoint, surface, intervalEnd, intervalStart);
                AddSpan(spans, CompareLerTerrainClassification.OutsideSurface, Interpolate(startPoint, endPoint, intervalStart), Interpolate(startPoint, endPoint, boundary));
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
            AddSpan(spans, CompareLerTerrainClassification.OutsideSurface, Interpolate(startPoint, endPoint, leftBoundary), Interpolate(startPoint, endPoint, rightBoundary));
            AddThresholdAwareInsideSpan(startPoint, endPoint, surface, threshold, rightBoundary, intervalEnd, spans);
        }

        private static void ProcessMidInsideInterval(
            Point3d startPoint,
            Point3d endPoint,
            TinSurface surface,
            double threshold,
            double intervalStart,
            double intervalEnd,
            CompareLerTerrainEvaluation startEval,
            CompareLerTerrainEvaluation endEval,
            CompareLerTerrainEvaluation midEval,
            IList<CompareLerTerrainSpan> spans)
        {
            double insideStart = intervalStart;
            double insideEnd = intervalEnd;

            if (!startEval.IsOnSurface)
            {
                insideStart = FindSurfaceBoundaryParameter(startPoint, endPoint, surface, (intervalStart + intervalEnd) * 0.5, intervalStart);
                AddSpan(spans, CompareLerTerrainClassification.OutsideSurface, Interpolate(startPoint, endPoint, intervalStart), Interpolate(startPoint, endPoint, insideStart));
            }

            if (!endEval.IsOnSurface)
            {
                insideEnd = FindSurfaceBoundaryParameter(startPoint, endPoint, surface, (intervalStart + intervalEnd) * 0.5, intervalEnd);
                AddSpan(spans, CompareLerTerrainClassification.OutsideSurface, Interpolate(startPoint, endPoint, insideEnd), Interpolate(startPoint, endPoint, intervalEnd));
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
            IList<CompareLerTerrainSpan> spans)
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

            CompareLerTerrainEvaluation startEval = Evaluate(startPoint, endPoint, surface, sampleStart);
            CompareLerTerrainEvaluation endEval = Evaluate(startPoint, endPoint, surface, sampleEnd);
            if (!startEval.IsOnSurface || !endEval.IsOnSurface)
            {
                return;
            }

            CompareLerTerrainClassification startClass = Classify(startEval.Clearance, threshold);
            CompareLerTerrainClassification endClass = Classify(endEval.Clearance, threshold);
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

        private static CompareLerTerrainEvaluation Evaluate(Point3d startPoint, Point3d endPoint, TinSurface surface, double parameter)
        {
            Point3d point = Interpolate(startPoint, endPoint, parameter);

            try
            {
                double surfaceElevation = surface.FindElevationAtXY(point.X, point.Y);
                return CompareLerTerrainEvaluation.OnSurface(point, parameter, surfaceElevation - point.Z);
            }
            catch (Autodesk.Civil.PointNotOnEntityException)
            {
                return CompareLerTerrainEvaluation.Outside(point, parameter);
            }
            catch (System.ArgumentException)
            {
                return CompareLerTerrainEvaluation.Outside(point, parameter);
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
                CompareLerTerrainEvaluation evaluation = Evaluate(startPoint, endPoint, surface, middle);
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
            CompareLerTerrainClassification startClassification)
        {
            double low = startParameter;
            double high = endParameter;
            CompareLerTerrainClassification lowClassification = startClassification;

            for (int i = 0; i < 40; i++)
            {
                double middle = (low + high) * 0.5;
                CompareLerTerrainEvaluation evaluation = Evaluate(startPoint, endPoint, surface, middle);
                if (!evaluation.IsOnSurface)
                {
                    break;
                }

                CompareLerTerrainClassification middleClassification = Classify(evaluation.Clearance, threshold);
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

        private static CompareLerTerrainClassification Classify(double clearance, double threshold)
        {
            return clearance <= threshold + 1e-6
                ? CompareLerTerrainClassification.LessOrEqualThreshold
                : CompareLerTerrainClassification.MoreThanThreshold;
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
            IList<CompareLerTerrainSpan> spans,
            CompareLerTerrainClassification classification,
            Point3d startPoint,
            Point3d endPoint)
        {
            if (startPoint.DistanceTo(endPoint) <= PointTolerance)
            {
                return;
            }

            spans.Add(new CompareLerTerrainSpan(classification, startPoint, endPoint));
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

    internal sealed class CompareLerTerrainPreviewResult
    {
        public CompareLerTerrainPreviewResult(int analyzedPolylineCount, IReadOnlyList<CompareLerTerrainPiece> pieces)
        {
            AnalyzedPolylineCount = analyzedPolylineCount;
            Pieces = pieces.ToList();
        }

        public int AnalyzedPolylineCount { get; }

        public List<CompareLerTerrainPiece> Pieces { get; }

        public int LessOrEqualCount => Pieces.Count(piece => piece.Classification == CompareLerTerrainClassification.LessOrEqualThreshold);

        public int MoreCount => Pieces.Count(piece => piece.Classification == CompareLerTerrainClassification.MoreThanThreshold);

        public int OutsideCount => Pieces.Count(piece => piece.Classification == CompareLerTerrainClassification.OutsideSurface);
    }

    internal sealed class CompareLerTerrainPieceBuilder
    {
        private readonly ObjectId _sourceId;
        private readonly List<CompareLerTerrainPiece> _pieces = new List<CompareLerTerrainPiece>();
        private CompareLerTerrainClassification? _currentClassification;
        private List<Point3d>? _currentPoints;

        public CompareLerTerrainPieceBuilder(ObjectId sourceId)
        {
            _sourceId = sourceId;
        }

        public void Append(CompareLerTerrainClassification classification, Point3d startPoint, Point3d endPoint)
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

        public IReadOnlyList<CompareLerTerrainPiece> Finish()
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

            _pieces.Add(new CompareLerTerrainPiece(_sourceId, _currentClassification.Value, _currentPoints));
            _currentClassification = null;
            _currentPoints = null;
        }
    }

    internal sealed class CompareLerTerrainPiece
    {
        public CompareLerTerrainPiece(ObjectId sourceId, CompareLerTerrainClassification classification, IReadOnlyList<Point3d> points)
        {
            SourceId = sourceId;
            Classification = classification;
            Points = points.ToList();
        }

        public ObjectId SourceId { get; }

        public CompareLerTerrainClassification Classification { get; }

        public List<Point3d> Points { get; }
    }

    internal sealed class CompareLerTerrainSurfaceDescriptor
    {
        public CompareLerTerrainSurfaceDescriptor(ObjectId objectId, string name)
        {
            ObjectId = objectId;
            Name = name;
        }

        public ObjectId ObjectId { get; }

        public string Name { get; }
    }

    internal readonly struct CompareLerTerrainSpan
    {
        public CompareLerTerrainSpan(
            CompareLerTerrainClassification classification,
            Point3d startPoint,
            Point3d endPoint)
        {
            Classification = classification;
            StartPoint = startPoint;
            EndPoint = endPoint;
        }

        public CompareLerTerrainClassification Classification { get; }

        public Point3d StartPoint { get; }

        public Point3d EndPoint { get; }
    }

    internal readonly struct CompareLerTerrainEvaluation
    {
        private CompareLerTerrainEvaluation(Point3d point, double parameter, bool isOnSurface, double clearance)
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

        public static CompareLerTerrainEvaluation OnSurface(Point3d point, double parameter, double clearance)
        {
            return new CompareLerTerrainEvaluation(point, parameter, true, clearance);
        }

        public static CompareLerTerrainEvaluation Outside(Point3d point, double parameter)
        {
            return new CompareLerTerrainEvaluation(point, parameter, false, double.NaN);
        }
    }

    internal enum CompareLerTerrainClassification
    {
        LessOrEqualThreshold,
        MoreThanThreshold,
        OutsideSurface
    }
}
