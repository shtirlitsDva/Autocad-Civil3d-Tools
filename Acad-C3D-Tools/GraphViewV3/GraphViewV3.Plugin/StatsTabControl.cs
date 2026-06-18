using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GraphViewV3;

/// <summary>
/// The "Statistics" palette tab: a live dashboard (element counts, total length, length-by-size
/// bars, system split, components-by-type). Its own PaletteSet visual (side tab), sharing the
/// same GraphViewModel as the graph tab so one live loop updates both.
/// </summary>
internal sealed class StatsTabControl : UserControl, IDisposable
{
    private readonly GraphViewModel _vm;
    private readonly TextBlock _status;
    private readonly StackPanel _stats;

    public StatsTabControl(GraphViewModel vm)
    {
        _vm = vm;
        Background = Theme.BgSurface;

        var root = new DockPanel { LastChildFill = true };

        _status = new TextBlock
        {
            Foreground = Theme.TextPrimary, FontFamily = new FontFamily("Segoe UI"),
            FontSize = 12, Margin = new Thickness(10, 8, 10, 8), TextWrapping = TextWrapping.Wrap,
        };
        var header = new Border
        {
            Background = Theme.BgPanel, BorderBrush = Theme.BorderSubtle,
            BorderThickness = new Thickness(0, 0, 0, 1), Child = _status,
        };
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        _stats = new StackPanel { Margin = new Thickness(12) };
        root.Children.Add(new ScrollViewer
        {
            Content = _stats, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = Theme.BgSurface,
        });

        Content = root;

        _vm.Updated += OnUpdated;
        OnUpdated();
    }

    private void OnUpdated()
    {
        _status.Text = _vm.Status;
        _stats.Children.Clear();
        var s = _vm.Latest.Stats;

        AddLine($"Elements: {s.ElementCount}   (pipes {s.PipeCount}, components {s.ComponentCount})");
        AddLine($"Total pipe length: {s.TotalLength:N1} m");

        AddHeader("Length by size");
        double maxLen = s.LengthBySize.Count > 0 ? s.LengthBySize.Max(x => x.Length) : 1;
        foreach (var item in s.LengthBySize)
            _stats.Children.Add(Bar(item.Size, item.Length, maxLen, Theme.SizeBrush(item.Size), $"{item.Length:N0} m"));

        AddHeader("System");
        double maxSys = s.BySystemClass.Count > 0 ? s.BySystemClass.Max(x => x.Length) : 1;
        foreach (var item in s.BySystemClass)
            _stats.Children.Add(Bar(item.Class, item.Length, maxSys, Theme.CompStroke, $"{item.Length:N0} m ({item.PipeCount})"));

        AddHeader("Components by type");
        foreach (var item in s.ComponentsByType)
            AddLine($"  {item.Name}: {item.Count}");
    }

    private void AddLine(string text) => _stats.Children.Add(new TextBlock
    {
        Text = text, Foreground = Theme.TextPrimary, FontFamily = new FontFamily("Segoe UI"),
        FontSize = 12, Margin = new Thickness(0, 2, 0, 2), TextWrapping = TextWrapping.Wrap,
    });

    private void AddHeader(string text) => _stats.Children.Add(new TextBlock
    {
        Text = text.ToUpperInvariant(), Foreground = Theme.TextMuted, FontFamily = new FontFamily("Segoe UI"),
        FontSize = 10, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 12, 0, 4),
    });

    private Grid Bar(string label, double value, double max, Brush fill, string valueText)
    {
        var g = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });

        var lbl = new TextBlock { Text = label, Foreground = Theme.TextPrimary, FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(lbl, 0); g.Children.Add(lbl);

        var holder = new Grid();
        holder.Children.Add(new Border { Background = Theme.BgDeep, CornerRadius = new CornerRadius(4), Height = 14 });
        holder.Children.Add(new Border
        {
            Background = fill, CornerRadius = new CornerRadius(4), Height = 14,
            HorizontalAlignment = HorizontalAlignment.Left,
            Width = Math.Max(2, 160 * (max > 0 ? value / max : 0)),
        });
        Grid.SetColumn(holder, 1); g.Children.Add(holder);

        var val = new TextBlock { Text = valueText, Foreground = Theme.TextMuted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
        Grid.SetColumn(val, 2); g.Children.Add(val);
        return g;
    }

    public void Dispose() => _vm.Updated -= OnUpdated;
}
