using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace GraphViewV3;

/// <summary>
/// The "Settings" palette tab (own visual). Writes to the shared <see cref="GraphSettings"/>
/// and raises Changed; the live loop picks up tolerance, the graph tab picks up colour mode
/// and label behaviour. Built in C# (no XAML), styled to the dark theme.
/// </summary>
internal sealed class SettingsTabControl : UserControl
{
    private readonly GraphSettings _settings;
    private Button _dnButton = null!;
    private Button _sysButton = null!;
    private Button _labelButton = null!;

    public SettingsTabControl(GraphSettings settings)
    {
        _settings = settings;
        Background = Theme.BgSurface;

        var panel = new StackPanel { Margin = new Thickness(14) };

        // --- Tolerance ---
        panel.Children.Add(Header("Connection tolerance (m)"));
        var tolBox = new TextBox
        {
            Text = settings.Tolerance.ToString(CultureInfo.InvariantCulture),
            Background = Theme.BgDeep, Foreground = Theme.TextPrimary,
            BorderBrush = Theme.BorderDefault, BorderThickness = new Thickness(1),
            Padding = new Thickness(6, 4, 6, 4), Width = 90, FontSize = 13,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        var applyRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
        applyRow.Children.Add(tolBox);
        applyRow.Children.Add(Btn("Apply", () =>
        {
            if (double.TryParse(tolBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var t) && t > 0)
            {
                _settings.Tolerance = t;
                _settings.RaiseChanged();
            }
            else
            {
                tolBox.Text = _settings.Tolerance.ToString(CultureInfo.InvariantCulture);
            }
        }));
        panel.Children.Add(applyRow);

        // --- Node colour ---
        panel.Children.Add(Header("Node colour"));
        var colorRow = new StackPanel { Orientation = Orientation.Horizontal };
        _dnButton = Btn("By DN", () => SetColor(ColorMode.ByDn));
        _sysButton = Btn("By system", () => SetColor(ColorMode.BySystem));
        colorRow.Children.Add(_dnButton);
        colorRow.Children.Add(_sysButton);
        panel.Children.Add(colorRow);

        // --- Labels ---
        panel.Children.Add(Header("Labels"));
        _labelButton = Btn(LabelText(), ToggleLabels);
        _labelButton.HorizontalAlignment = HorizontalAlignment.Left;
        panel.Children.Add(_labelButton);

        UpdateColorVisuals();

        Content = new ScrollViewer
        {
            Content = panel, Background = Theme.BgSurface,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
    }

    private void SetColor(ColorMode mode)
    {
        _settings.ColorBy = mode;
        UpdateColorVisuals();
        _settings.RaiseChanged();
    }

    private void ToggleLabels()
    {
        _settings.AlwaysLabels = !_settings.AlwaysLabels;
        _labelButton.Content = LabelText();
        _settings.RaiseChanged();
    }

    private string LabelText() => _settings.AlwaysLabels ? "Labels: always" : "Labels: on zoom";

    private void UpdateColorVisuals()
    {
        Highlight(_dnButton, _settings.ColorBy == ColorMode.ByDn);
        Highlight(_sysButton, _settings.ColorBy == ColorMode.BySystem);
    }

    private static void Highlight(Button b, bool on)
    {
        b.Background = on ? Theme.Hex("#4A6B8A") : Theme.BgElevated;
        b.Foreground = on ? Brushes.White : Theme.TextPrimary;
    }

    private static TextBlock Header(string text) => new()
    {
        Text = text.ToUpperInvariant(), Foreground = Theme.TextMuted, FontFamily = new FontFamily("Segoe UI"),
        FontSize = 10, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 14, 0, 4),
    };

    private static Button Btn(string text, Action onClick)
    {
        var b = new Button
        {
            Content = text, Foreground = Theme.TextPrimary, Background = Theme.BgElevated,
            BorderBrush = Theme.BorderDefault, BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(0, 0, 6, 0),
            FontSize = 12, Cursor = Cursors.Hand,
        };
        b.Click += (_, _) => onClick();
        return b;
    }
}
