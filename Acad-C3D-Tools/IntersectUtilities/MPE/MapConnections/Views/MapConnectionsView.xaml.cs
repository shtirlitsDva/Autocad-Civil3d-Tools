using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;

using WpfPoint = System.Windows.Point;

namespace IntersectUtilities.MPE.MapConnections.Views;

/// <summary>
/// Draws the connection map on a plain Canvas. Both modes keep their geometry in world coordinates (layout units
/// for "Graf", plan metres for "Net") and are redrawn through a camera (scale + offset) on every pan or zoom:
/// wheel zooms at the cursor, dragging the background pans, "Tilpas" or a double-click refits.
/// </summary>
internal partial class MapConnectionsView : UserControl
{
    private const double FitMargin = 0.9;
    private const double WheelStep = 1.2;
    private const double LabelAboveChild = 24.0; // layout units from a tree edge's label centre to the child box

    private static readonly Brush BranchBrush = Frozen(Color.FromRgb(0xF6, 0xE0, 0x5E));
    private static readonly Brush EndToEndBrush = Frozen(Color.FromRgb(0x68, 0xD3, 0x91));
    private static readonly Brush NaBrush = Frozen(Color.FromRgb(0x71, 0x80, 0x96));

    private LpNetworkSnapshot? _snapshot;
    private MapConnectionsGraphLayout? _graph;
    private double _scale = 1.0;
    private double _offsetX;
    private double _offsetY;
    private bool _fitPending;
    private bool _cameraMoved; // the user panned or zoomed, so a resize must not refit under them
    private WpfPoint? _dragStart;

    public MapConnectionsView()
    {
        Resources = LoadTheme();
        InitializeComponent();
        Surface.MouseWheel += OnWheel;
        Surface.MouseLeftButtonDown += OnSurfaceDown;
        Surface.MouseMove += OnSurfaceMove;
        Surface.MouseLeftButtonUp += OnSurfaceUp;
        Host.SizeChanged += (_, _) =>
        {
            if (_fitPending || !_cameraMoved)
            {
                Fit();
            }

            Render();
        };
    }

    private bool IsNet => NetMode?.IsChecked == true;

    // Reuse PipePlan's embedded dark theme, like the other MPE palettes.
    private static ResourceDictionary LoadTheme()
    {
        var asm = typeof(MapConnectionsView).Assembly;
        using var stream = asm.GetManifestResourceStream("IntersectUtilities.MPE.PipePlan.DarkTheme.xaml")
            ?? throw new InvalidOperationException("Embedded DarkTheme.xaml not found");
        return (ResourceDictionary)XamlReader.Load(stream);
    }

    public void Load(LpNetworkSnapshot? snapshot)
    {
        _snapshot = snapshot;
        _graph = snapshot is null ? null : MapConnectionsGraphLayout.Build(snapshot);
        Fit();
        Render();
    }

    public void SetStatus(string status) => Status.Text = status;

    private void OnModeChanged(object sender, RoutedEventArgs e)
    {
        if (Surface is null)
        {
            return; // fired by IsChecked="True" during InitializeComponent
        }

        Fit();
        Render();
    }

    private void OnRefresh(object sender, RoutedEventArgs e) => MapConnectionsRuntime.Refresh();

    private void OnFit(object sender, RoutedEventArgs e)
    {
        Fit();
        Render();
    }

    // ---- Camera ---------------------------------------------------------------------------------

    // World → screen. "Net" is plan space (Y up), "Graf" is layout space (Y down).
    private WpfPoint ToScreen(double x, double y) =>
        new(x * _scale + _offsetX, (IsNet ? -y : y) * _scale + _offsetY);

    private Rect WorldBounds()
    {
        List<WpfPoint> points = [];
        if (_snapshot is not null && IsNet)
        {
            points.AddRange(_snapshot.Lines.Values.SelectMany(l => l.PlanSamples).Select(p => new WpfPoint(p.X, -p.Y)));
        }
        else if (_graph is not null)
        {
            foreach (GraphNode node in _graph.Nodes.Values)
            {
                points.Add(new WpfPoint(node.Center.X - MapConnectionsGraphLayout.NodeWidth, node.Center.Y - MapConnectionsGraphLayout.NodeHeight));
                points.Add(new WpfPoint(node.Center.X + MapConnectionsGraphLayout.NodeWidth, node.Center.Y + MapConnectionsGraphLayout.NodeHeight));
            }
        }

        if (points.Count == 0)
        {
            return new Rect(0, 0, 1, 1);
        }

        double minX = points.Min(p => p.X), maxX = points.Max(p => p.X);
        double minY = points.Min(p => p.Y), maxY = points.Max(p => p.Y);
        return new Rect(minX, minY, Math.Max(maxX - minX, 1.0), Math.Max(maxY - minY, 1.0));
    }

    private void Fit()
    {
        double w = Host.ActualWidth, h = Host.ActualHeight;
        if (w < 10 || h < 10)
        {
            _fitPending = true;
            return;
        }

        _fitPending = false;
        _cameraMoved = false;
        Rect b = WorldBounds();
        _scale = Math.Min(w / b.Width, h / b.Height) * FitMargin;
        if (!IsNet)
        {
            _scale = Math.Min(_scale, 1.5); // a small graph shouldn't blow up to poster size
        }

        _offsetX = w / 2.0 - (b.X + b.Width / 2.0) * _scale;
        _offsetY = h / 2.0 - (b.Y + b.Height / 2.0) * _scale;
    }

    private void OnWheel(object sender, MouseWheelEventArgs e)
    {
        WpfPoint at = e.GetPosition(Surface);
        double factor = e.Delta > 0 ? WheelStep : 1.0 / WheelStep;
        _offsetX = at.X - (at.X - _offsetX) * factor;
        _offsetY = at.Y - (at.Y - _offsetY) * factor;
        _scale *= factor;
        _cameraMoved = true;
        Render();
        e.Handled = true;
    }

    private void OnSurfaceDown(object sender, MouseButtonEventArgs e)
    {
        if (!ReferenceEquals(e.OriginalSource, Surface))
        {
            return; // an LP, dot or chip was hit
        }

        if (e.ClickCount == 2)
        {
            Fit();
            Render();
            return;
        }

        _dragStart = e.GetPosition(Surface);
        Surface.CaptureMouse();
    }

    private void OnSurfaceMove(object sender, MouseEventArgs e)
    {
        if (_dragStart is not WpfPoint start)
        {
            return;
        }

        WpfPoint now = e.GetPosition(Surface);
        _offsetX += now.X - start.X;
        _offsetY += now.Y - start.Y;
        _dragStart = now;
        _cameraMoved = true;
        Render();
    }

    private void OnSurfaceUp(object sender, MouseButtonEventArgs e)
    {
        _dragStart = null;
        Surface.ReleaseMouseCapture();
    }

    // ---- Drawing --------------------------------------------------------------------------------

    private void Render()
    {
        Surface.Children.Clear();
        if (_snapshot is null)
        {
            return;
        }

        if (IsNet)
        {
            RenderNet(_snapshot);
        }
        else if (_graph is not null)
        {
            RenderGraph(_graph);
        }
    }

    private void RenderGraph(MapConnectionsGraphLayout graph)
    {
        double s = _scale;
        double halfW = MapConnectionsGraphLayout.NodeWidth / 2.0, halfH = MapConnectionsGraphLayout.NodeHeight / 2.0;

        foreach (GraphEdge edge in graph.Edges)
        {
            GraphNode from = graph.Nodes[edge.Connection.From];
            GraphNode to = graph.Nodes[edge.Connection.To];
            Brush brush = EdgeBrush(edge.Connection.Kind);

            if (!edge.IsTreeEdge)
            {
                // Cross edges go centre to centre, dashed, under the boxes.
                WpfPoint c1 = ToScreen(from.Center.X, from.Center.Y), c2 = ToScreen(to.Center.X, to.Center.Y);
                Surface.Children.Add(new Line
                {
                    X1 = c1.X, Y1 = c1.Y, X2 = c2.X, Y2 = c2.Y,
                    Stroke = brush,
                    StrokeThickness = 1.5,
                    StrokeDashArray = new DoubleCollection { 4, 3 },
                    IsHitTestVisible = false,
                });
                continue;
            }

            // Tree edges run from the bottom of the upper box to the top of the lower one; the arrowhead sits
            // at the connection's To end, pointing down or up.
            GraphNode upper = edge.TreeParent == from.Name ? from : to;
            GraphNode lower = ReferenceEquals(upper, from) ? to : from;
            WpfPoint top = ToScreen(upper.Center.X, upper.Center.Y + halfH);
            WpfPoint bottom = ToScreen(lower.Center.X, lower.Center.Y - halfH);
            Surface.Children.Add(new Line
            {
                X1 = top.X, Y1 = top.Y, X2 = bottom.X, Y2 = bottom.Y,
                Stroke = brush,
                StrokeThickness = 1.5,
                IsHitTestVisible = false,
            });
            double arrow = 8.0 * Math.Clamp(s, 0.6, 1.5);
            Surface.Children.Add(ReferenceEquals(lower, to)
                ? Arrowhead(top, bottom, brush, arrow)
                : Arrowhead(bottom, top, brush, arrow));
        }

        // Labels after all lines so no edge is drawn across a chip.
        foreach (GraphEdge edge in graph.Edges)
        {
            GraphNode from = graph.Nodes[edge.Connection.From];
            GraphNode to = graph.Nodes[edge.Connection.To];
            WpfPoint mid;
            if (edge.IsTreeEdge)
            {
                // Straight above the lower box: siblings are a whole slot apart there, so a fan of branches
                // doesn't stack its labels on top of each other near the parent.
                GraphNode lower = edge.TreeParent == from.Name ? to : from;
                mid = ToScreen(lower.Center.X, lower.Center.Y - halfH - LabelAboveChild);
            }
            else
            {
                mid = ToScreen((from.Center.X + to.Center.X) / 2.0, (from.Center.Y + to.Center.Y) / 2.0);
            }

            FrameworkElement label = ConnectionLabel(edge.Connection, s);
            label.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(label, mid.X - label.DesiredSize.Width / 2.0);
            Canvas.SetTop(label, mid.Y - label.DesiredSize.Height / 2.0);
            Surface.Children.Add(label);
        }

        foreach (GraphNode node in graph.Nodes.Values)
        {
            WpfPoint topLeft = ToScreen(node.Center.X - halfW, node.Center.Y - halfH);
            Border box = new()
            {
                Width = MapConnectionsGraphLayout.NodeWidth * s,
                Height = MapConnectionsGraphLayout.NodeHeight * s,
                CornerRadius = new CornerRadius(4 * s),
                BorderThickness = new Thickness(1.5),
                BorderBrush = node.IsNa ? NaBrush : ThemeBrush("AccentBrush"),
                Background = ThemeBrush("BackgroundBrush"),
                Child = new TextBlock
                {
                    Text = node.Name,
                    FontSize = Math.Max(1.0, 13.0 * s),
                    FontWeight = FontWeights.SemiBold,
                    Foreground = node.IsNa ? NaBrush : ThemeBrush("ForegroundBrush"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
                ToolTip = node.IsNa ? $"{node.Name} — intet længdeprofil" : $"Zoom til {node.Name}",
            };
            Canvas.SetLeft(box, topLeft.X);
            Canvas.SetTop(box, topLeft.Y);
            if (!node.IsNa)
            {
                MakeClickable(box, () => MapConnectionsRuntime.ZoomToLp(node.Name), on =>
                {
                    box.BorderBrush = ThemeBrush(on ? "ForegroundBrush" : "AccentBrush");
                    box.Background = ThemeBrush(on ? "SurfaceHoverBrush" : "BackgroundBrush");
                });
            }

            Surface.Children.Add(box);
        }
    }

    /// <summary>Block type over the two station chips; the NA side is plain text since it has no LP.</summary>
    private FrameworkElement ConnectionLabel(LpConnection c, double s)
    {
        double font = Math.Max(1.0, 10.5 * s);
        StackPanel chips = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        chips.Children.Add(Chip(c.From, c.FromStation, font));
        chips.Children.Add(c.Kind == LpConnectionKind.Na
            ? new TextBlock { Text = c.To, FontSize = font, Foreground = NaBrush, Margin = new Thickness(4 * s, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center }
            : Chip(c.To, c.ToStation, font, new Thickness(4 * s, 0, 0, 0)));

        StackPanel panel = new() { Background = ThemeBrush("SurfaceBrush") };
        panel.Children.Add(new TextBlock
        {
            Text = c.Label ?? (c.Kind == LpConnectionKind.EndToEnd ? "Ende mod ende" : "Uden blok"),
            FontSize = font,
            Foreground = ThemeBrush("ForegroundMutedBrush"),
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        panel.Children.Add(chips);
        return panel;
    }

    private Border Chip(string lp, double station, double font, Thickness margin = default)
    {
        TextBlock text = new() { Text = StationText(lp, station), FontSize = font, Foreground = ThemeBrush("ForegroundBrush") };
        Border chip = new()
        {
            BorderBrush = ThemeBrush("AccentBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(font * 0.35, 0, font * 0.35, 0),
            Margin = margin,
            Background = ThemeBrush("BackgroundBrush"),
            Child = text,
            ToolTip = $"Hop til {lp} st. {station.ToString("0.00", CultureInfo.InvariantCulture)}",
        };
        MakeClickable(chip, () => MapConnectionsRuntime.JumpTo(lp, station), on =>
        {
            chip.Background = ThemeBrush(on ? "AccentBrush" : "BackgroundBrush");
            text.Foreground = ThemeBrush(on ? "BackgroundBrush" : "ForegroundBrush");
        });
        return chip;
    }

    private void RenderNet(LpNetworkSnapshot snapshot)
    {
        Brush lineBrush = ThemeBrush("AccentBrush");
        foreach (LpLine line in snapshot.Lines.Values)
        {
            PointCollection points = new(line.PlanSamples.Select(p => ToScreen(p.X, p.Y)));
            Polyline visible = new() { Points = points, Stroke = lineBrush, StrokeThickness = 2, IsHitTestVisible = false };
            Surface.Children.Add(visible);

            // A wide invisible twin makes the thin line easy to click.
            Polyline hit = new()
            {
                Points = points,
                Stroke = Brushes.Transparent,
                StrokeThickness = 12,
                ToolTip = line.View is null ? $"{line.Name} — intet profile view" : $"Zoom til {line.Name}",
            };
            MakeClickable(hit, () => MapConnectionsRuntime.ZoomToLp(line.Name), on =>
            {
                visible.Stroke = on ? ThemeBrush("ForegroundBrush") : lineBrush;
                visible.StrokeThickness = on ? 4 : 2;
            });
            Surface.Children.Add(hit);
        }

        foreach (LpLine line in snapshot.Lines.Values)
        {
            Autodesk.AutoCAD.Geometry.Point2d mid = line.PlanSamples[line.PlanSamples.Count / 2];
            WpfPoint at = ToScreen(mid.X, mid.Y);
            TextBlock name = new()
            {
                Text = line.Name,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = ThemeBrush("ForegroundBrush"),
                Background = ThemeBrush("SurfaceBrush"),
                IsHitTestVisible = false,
            };
            Canvas.SetLeft(name, at.X + 4);
            Canvas.SetTop(name, at.Y - 8);
            Surface.Children.Add(name);
        }

        foreach (LpConnection c in snapshot.Connections)
        {
            WpfPoint at = ToScreen(c.PlanPoint.X, c.PlanPoint.Y);
            double r = c.Kind == LpConnectionKind.Na ? 4.0 : 5.5;
            Ellipse dot = new()
            {
                Width = 2 * r,
                Height = 2 * r,
                Fill = EdgeBrush(c.Kind),
                Stroke = ThemeBrush("SurfaceBrush"),
                StrokeThickness = 1,
                ToolTip = $"{c.Label ?? KindText(c.Kind)}: {c.From} / {c.To}",
            };
            Canvas.SetLeft(dot, at.X - r);
            Canvas.SetTop(dot, at.Y - r);
            MakeClickable(dot, () => ShowChipMenu(dot, c), on =>
            {
                double size = 2 * (on ? r * 1.6 : r);
                dot.Width = dot.Height = size;
                dot.Stroke = ThemeBrush(on ? "ForegroundBrush" : "SurfaceBrush");
                dot.StrokeThickness = on ? 2 : 1;
                Canvas.SetLeft(dot, at.X - size / 2);
                Canvas.SetTop(dot, at.Y - size / 2);
            });
            Surface.Children.Add(dot);
        }
    }

    private void ShowChipMenu(FrameworkElement anchor, LpConnection c)
    {
        ContextMenu menu = new() { PlacementTarget = anchor };
        menu.Items.Add(new MenuItem { Header = c.Label ?? KindText(c.Kind), IsEnabled = false });
        menu.Items.Add(MenuChip(c.From, c.FromStation));
        menu.Items.Add(c.Kind == LpConnectionKind.Na
            ? new MenuItem { Header = $"{c.To} — intet længdeprofil", IsEnabled = false }
            : MenuChip(c.To, c.ToStation));
        menu.IsOpen = true;
    }

    private static MenuItem MenuChip(string lp, double station)
    {
        MenuItem item = new() { Header = StationText(lp, station) };
        item.Click += (_, _) => MapConnectionsRuntime.JumpTo(lp, station);
        return item;
    }

    // ---- Helpers --------------------------------------------------------------------------------

    /// <summary>Hand cursor, a hover highlight (so it's clear what a click will hit) and the click itself.</summary>
    private static void MakeClickable(FrameworkElement element, Action onClick, Action<bool> highlight)
    {
        element.Cursor = Cursors.Hand;
        element.MouseEnter += (_, _) => highlight(true);
        element.MouseLeave += (_, _) => highlight(false);
        element.MouseLeftButtonDown += (_, e) => e.Handled = true; // don't start a pan
        element.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            onClick();
        };
    }

    private static Polygon Arrowhead(WpfPoint from, WpfPoint to, Brush brush, double size)
    {
        Vector dir = to - from;
        if (dir.Length < 1e-6)
        {
            return new Polygon();
        }

        dir.Normalize();
        Vector normal = new(-dir.Y, dir.X);
        WpfPoint back = to - dir * size;
        return new Polygon
        {
            Points = new PointCollection { to, back + normal * size * 0.45, back - normal * size * 0.45 },
            Fill = brush,
            IsHitTestVisible = false,
        };
    }

    private static string StationText(string lp, double station) =>
        $"{lp} @ {station.ToString("0.0", CultureInfo.InvariantCulture)}";

    private static string KindText(LpConnectionKind kind) => kind switch
    {
        LpConnectionKind.EndToEnd => "Ende mod ende",
        LpConnectionKind.Na => "NA",
        _ => "Afgrening",
    };

    private static Brush EdgeBrush(LpConnectionKind kind) => kind switch
    {
        LpConnectionKind.EndToEnd => EndToEndBrush,
        LpConnectionKind.Na => NaBrush,
        _ => BranchBrush,
    };

    private Brush ThemeBrush(string key) => (Brush)FindResource(key);

    private static Brush Frozen(Color color)
    {
        SolidColorBrush brush = new(color);
        brush.Freeze();
        return brush;
    }
}
