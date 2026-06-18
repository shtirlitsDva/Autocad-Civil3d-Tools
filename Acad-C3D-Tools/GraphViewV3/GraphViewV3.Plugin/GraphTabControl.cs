using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

using GraphViewV3.Core;

namespace GraphViewV3;

/// <summary>
/// The "Graph" palette tab: a live, floating, pan/zoom network canvas. Built in C# (no XAML)
/// to avoid cross-ALC XAML resolution under DevReload. Added to the PaletteSet as its own
/// visual so AutoCAD renders it as a side tab. Node labels are screen-fixed; the overview is
/// dots + edges, with labels appearing once zoomed in. Illegal pipe-to-pipe edges are red.
/// </summary>
internal sealed class GraphTabControl : UserControl, IDisposable
{
    private readonly GraphViewModel _vm;
    private readonly GraphSettings _settings;
    private readonly TextBlock _status;
    private readonly Border _header;
    private readonly TextBlock _hint;
    private readonly Canvas _canvas;
    private bool _maximized;

    private double _scale = 1, _offX, _offY, _minX, _minY;
    private double _labelScale = double.MaxValue;
    private bool _hasView;
    private Point _lastDrag;
    private bool _dragging;

    public GraphTabControl(GraphViewModel vm, GraphSettings settings)
    {
        _vm = vm;
        _settings = settings;
        Background = Theme.BgPanel;

        var root = new DockPanel { LastChildFill = true };

        _status = new TextBlock
        {
            Foreground = Theme.TextPrimary, FontFamily = new FontFamily("Segoe UI"),
            FontSize = 12, Margin = new Thickness(10, 8, 10, 8), TextWrapping = TextWrapping.Wrap,
        };
        _header = new Border
        {
            Background = Theme.BgSurface, BorderBrush = Theme.BorderSubtle,
            BorderThickness = new Thickness(0, 0, 0, 1), Child = _status,
        };
        DockPanel.SetDock(_header, Dock.Top);
        root.Children.Add(_header);

        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Background = Theme.BgSurface };
        toolbar.Children.Add(ToolButton("Fit", () => { FitView(); RedrawGraph(); }));
        toolbar.Children.Add(ToolButton("Refresh", () => { _hasView = false; OnUpdated(); }));
        toolbar.Children.Add(ToolButton("⛶ Max", ToggleMaximize));
        _hint = new TextBlock
        {
            Text = "wheel = zoom · drag = pan · click node = select+zoom in CAD",
            Foreground = Theme.TextMuted, FontSize = 10, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
        };
        toolbar.Children.Add(_hint);
        DockPanel.SetDock(toolbar, Dock.Top);
        root.Children.Add(toolbar);

        _canvas = new Canvas { Background = Theme.BgDeep, ClipToBounds = true };
        _canvas.SizeChanged += (_, _) => { if (!_hasView) FitView(); RedrawGraph(); };
        _canvas.MouseWheel += OnWheel;
        _canvas.MouseLeftButtonDown += OnCanvasDown;
        _canvas.MouseMove += OnCanvasMove;
        _canvas.MouseLeftButtonUp += OnCanvasUp;
        root.Children.Add(_canvas);

        Content = root;

        _vm.Updated += OnUpdated;
        _settings.Changed += RedrawGraph;
        OnUpdated();
    }

    private void ToggleMaximize()
    {
        _maximized = !_maximized;
        var vis = _maximized ? Visibility.Collapsed : Visibility.Visible;
        _header.Visibility = vis;
        _hint.Visibility = vis;
    }

    private Button ToolButton(string text, Action onClick)
    {
        var b = new Button
        {
            Content = text, Foreground = Theme.TextPrimary, Background = Theme.BgElevated,
            BorderBrush = Theme.BorderDefault, BorderThickness = new Thickness(1),
            Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(4, 4, 0, 4),
            FontSize = 11, Cursor = Cursors.Hand,
        };
        b.Click += (_, _) => onClick();
        return b;
    }

    private void OnUpdated()
    {
        _status.Text = _vm.Status;
        if (!_hasView) FitView();
        RedrawGraph();
    }

    private void FitView()
    {
        var graph = _vm.Latest.Graph;
        if (graph.Nodes.Count == 0) { _hasView = false; return; }
        double w = _canvas.ActualWidth, h = _canvas.ActualHeight;
        if (w < 5 || h < 5) return;

        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        foreach (var n in graph.Nodes)
        {
            minX = Math.Min(minX, n.X); minY = Math.Min(minY, n.Y);
            maxX = Math.Max(maxX, n.X); maxY = Math.Max(maxY, n.Y);
        }
        _minX = minX; _minY = minY;
        double cw = Math.Max(maxX - minX, 1), ch = Math.Max(maxY - minY, 1);
        const double pad = 30;
        _scale = Math.Min((w - pad * 2) / cw, (h - pad * 2) / ch);
        if (_scale <= 0 || double.IsInfinity(_scale) || double.IsNaN(_scale)) _scale = 1;
        _offX = (w - cw * _scale) / 2;
        _offY = (h - ch * _scale) / 2;
        _labelScale = _scale * 2.2;
        _hasView = true;
    }

    private double Sx(double x) => (x - _minX) * _scale + _offX;
    private double Sy(double y) => _canvas.ActualHeight - ((y - _minY) * _scale + _offY);

    private void OnWheel(object sender, MouseWheelEventArgs e)
    {
        var p = e.GetPosition(_canvas);
        double factor = e.Delta > 0 ? 1.15 : 1 / 1.15;
        _offX = p.X - (p.X - _offX) * factor;
        double H = _canvas.ActualHeight;
        _offY = (H - p.Y) - ((H - p.Y) - _offY) * factor;
        _scale *= factor;
        RedrawGraph();
    }

    private void OnCanvasDown(object sender, MouseButtonEventArgs e)
    {
        _dragging = true; _lastDrag = e.GetPosition(_canvas); _canvas.CaptureMouse();
    }

    private void OnCanvasMove(object sender, MouseEventArgs e)
    {
        if (!_dragging) return;
        var p = e.GetPosition(_canvas);
        _offX += p.X - _lastDrag.X;
        _offY -= p.Y - _lastDrag.Y;
        _lastDrag = p;
        RedrawGraph();
    }

    private void OnCanvasUp(object sender, MouseButtonEventArgs e)
    {
        _dragging = false; _canvas.ReleaseMouseCapture();
    }

    private void RedrawGraph()
    {
        _canvas.Children.Clear();
        var graph = _vm.Latest.Graph;
        if (graph.Nodes.Count == 0 || _canvas.ActualWidth < 5) return;

        foreach (var e in graph.Edges)
        {
            _canvas.Children.Add(new Line
            {
                X1 = Sx(e.A.X), Y1 = Sy(e.A.Y), X2 = Sx(e.B.X), Y2 = Sy(e.B.Y),
                Stroke = e.IsError ? Theme.ErrBrush : Theme.EdgeBrush,
                StrokeThickness = e.IsError ? 2.4 : 1.2,
                ToolTip = e.IsError ? e.ErrorReason : null,
            });
        }

        bool showLabels = _scale >= _labelScale || _settings.AlwaysLabels;
        foreach (var n in graph.Nodes)
        {
            double sx = Sx(n.X), sy = Sy(n.Y);
            if (sx < -200 || sy < -200 || sx > _canvas.ActualWidth + 200 || sy > _canvas.ActualHeight + 200)
                continue;

            var (fill, stroke) = NodeBrushes(n);

            if (!showLabels)
            {
                double r = n.Kind == NodeKind.Component ? 5 : 4;
                var dot = new Ellipse
                {
                    Width = r * 2, Height = r * 2, Fill = stroke,
                    Stroke = Theme.BgDeep, StrokeThickness = 1, Cursor = Cursors.Hand,
                    ToolTip = $"{n.Label}  ({n.Kind})  #{n.Handle}",
                };
                dot.MouseLeftButtonDown += (_, ev) => { ev.Handled = true; AcadSelect.SelectAndZoom(n.Handle); };
                Canvas.SetLeft(dot, sx - r);
                Canvas.SetTop(dot, sy - r);
                _canvas.Children.Add(dot);
                continue;
            }

            var box = new Border
            {
                Background = fill, BorderBrush = stroke, BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(6),
                Child = new TextBlock
                {
                    Text = n.Label, Foreground = Theme.TextPrimary,
                    FontFamily = new FontFamily("Segoe UI"), FontSize = 10, Margin = new Thickness(6, 2, 6, 2),
                },
                Tag = n.Handle, Cursor = Cursors.Hand,
                ToolTip = $"{n.Label}  ({n.Kind})  #{n.Handle}",
            };
            box.MouseLeftButtonDown += (_, ev) => { ev.Handled = true; AcadSelect.SelectAndZoom(n.Handle); };
            box.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(box, sx - box.DesiredSize.Width / 2);
            Canvas.SetTop(box, sy - box.DesiredSize.Height / 2);
            _canvas.Children.Add(box);
        }
    }

    private (Brush Fill, Brush Stroke) NodeBrushes(GraphNode n)
    {
        if (n.Kind == NodeKind.Component) return (Theme.Hex("#332A40"), Theme.CompStroke);
        string key = _settings.ColorBy == ColorMode.BySystem ? n.System : n.Size;
        return (Theme.Hex("#2C3A47"), Theme.SizeBrush(key));
    }

    public void Dispose()
    {
        _vm.Updated -= OnUpdated;
        _settings.Changed -= RedrawGraph;
    }
}
