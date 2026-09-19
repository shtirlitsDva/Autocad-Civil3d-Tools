using IntersectUtilities.UtilsCommon.Enums;

using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace IntersectUtilities.NdhTrace;

/// <summary>
/// The NDHFROMFJV questions as small modal WPF windows shown through
/// AutoCAD (Application.ShowModalWindow), built in code like the other
/// IntersectUtilities settings dialogs. Danish, as the command's report is.
/// Each window preselects the reading the legacy drawing supports best, so OK
/// is a sensible answer; Annuller stops the import.
/// </summary>
internal sealed class WpfImportDialogs : IImportDialogs
{
    private const string Title = "NDHFROMFJV";

    public string? ChooseProducer(IReadOnlyDictionary<string, SortedDictionary<string, int>> named)
    {
        StackPanel panel = new StackPanel { Margin = new Thickness(14) };
        panel.Children.Add(new TextBlock
        {
            Text = "Den gamle tegning nævner flere producenter. Vælg den, tegningen skal bruge:",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10),
            MaxWidth = 460,
        });

        //Preselect the producer the most parts name.
        string likeliest = named.OrderByDescending(x => x.Value.Values.Sum()).First().Key;
        List<(RadioButton Button, string Token)> choices = new List<(RadioButton, string)>();
        foreach ((string token, SortedDictionary<string, int> parts) in named)
        {
            RadioButton button = new RadioButton
            {
                GroupName = "producer",
                IsChecked = token == likeliest,
                Margin = new Thickness(0, 2, 0, 6),
                Content = new TextBlock
                {
                    Text = $"{token}  ({LegacySettingsFacts.Describe(parts)})",
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 440,
                },
            };
            choices.Add((button, token));
            panel.Children.Add(button);
        }

        bool ok = ShowModal(panel, "Producent");
        return ok ? choices.First(x => x.Button.IsChecked == true).Token : null;
    }

    public IReadOnlyDictionary<SeriesKey, PipeSeriesEnum>? SettleSeries(IReadOnlyList<AmbiguousSize> sizes)
    {
        StackPanel panel = new StackPanel { Margin = new Thickness(14) };
        panel.Children.Add(new TextBlock
        {
            Text = "Disse rørstørrelser er tegnet i flere serier i den gamle tegning. " +
                "Vælg den serie, tegningens seriematrix skal have for hver:",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10),
            MaxWidth = 520,
        });

        Grid grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        List<(SeriesKey Key, ComboBox Box, List<PipeSeriesEnum> Options)> rows = new();
        foreach (AmbiguousSize size in sizes)
        {
            int row = grid.RowDefinitions.Count;
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            List<PipeSeriesEnum> options = size.Seen.Keys.Where(s => s != PipeSeriesEnum.Undefined).ToList();
            //Preselect the series most of the size's length is drawn in.
            PipeSeriesEnum likeliest = options.OrderByDescending(s => size.Seen[s].Length).First();
            ComboBox box = new ComboBox { Margin = new Thickness(8, 2, 8, 2), MinWidth = 70 };
            foreach (PipeSeriesEnum s in options) box.Items.Add(s.ToString());
            box.SelectedIndex = options.IndexOf(likeliest);

            AddCell(grid, new TextBlock { Text = size.Key.ToString(), VerticalAlignment = VerticalAlignment.Center }, row, 0);
            AddCell(grid, box, row, 1);
            AddCell(grid, new TextBlock
            {
                Text = string.Join(", ", size.Seen.Select(x =>
                    $"{(x.Key == PipeSeriesEnum.Undefined ? "ukendt" : x.Key.ToString())}: {x.Value}")),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = System.Windows.Media.Brushes.DimGray,
            }, row, 2);
            rows.Add((size.Key, box, options));
        }

        panel.Children.Add(new ScrollViewer
        {
            Content = grid,
            MaxHeight = 420,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        });

        if (!ShowModal(panel, "Serier")) return null;
        return rows.ToDictionary(x => x.Key, x => x.Options[x.Box.SelectedIndex]);
    }

    private static void AddCell(Grid grid, UIElement element, int row, int column)
    {
        Grid.SetRow(element, row);
        Grid.SetColumn(element, column);
        grid.Children.Add(element);
    }

    /// <summary>Shows the panel with OK / Annuller; true on OK.</summary>
    private static bool ShowModal(StackPanel panel, string subject)
    {
        Button ok = new Button { Content = "OK", Width = 80, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
        Button cancel = new Button { Content = "Annuller", Width = 80, IsCancel = true };
        StackPanel buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0),
        };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        panel.Children.Add(buttons);

        Window window = new Window
        {
            Title = $"{Title} – {subject}",
            Content = panel,
            SizeToContent = SizeToContent.WidthAndHeight,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            MinWidth = 320,
        };
        ok.Click += (_, _) => window.DialogResult = true;

        return AcadApp.ShowModalWindow(window) == true;
    }
}
