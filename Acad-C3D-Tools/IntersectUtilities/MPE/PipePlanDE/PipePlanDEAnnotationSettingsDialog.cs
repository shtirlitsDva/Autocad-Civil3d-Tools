using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Autodesk.AutoCAD.DatabaseServices;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace IntersectUtilities.MPE.PipePlanDE;

/// <summary>
/// Small modal dialog for the PDANNOTATE "Settings" (S) option: pick the dimension style from the
/// drawing's DimStyleTable and set the dimension-line offset. Built in code (no XAML) to keep it
/// self-contained. On OK the choice is persisted via <see cref="PipePlanDEAnnotationStore"/>.
/// </summary>
internal static class PipePlanDEAnnotationSettingsDialog
{
    public static void Show(Database db)
    {
        List<string> styleNames = ReadDimStyleNames(db);
        PipePlanDEAnnotationSettings current = PipePlanDEAnnotationStore.Get(db);

        ComboBox styleBox = new() { Margin = new Thickness(0, 2, 0, 10), IsEditable = false };
        foreach (string name in styleNames)
        {
            styleBox.Items.Add(name);
        }

        // Empty stored style = "current DIMSTYLE"; select it if present, else first entry.
        int index = string.IsNullOrEmpty(current.StyleName) ? -1 : styleNames.IndexOf(current.StyleName);
        styleBox.SelectedIndex = index >= 0 ? index : (styleNames.Count > 0 ? 0 : -1);

        TextBox offsetBox = new()
        {
            Text = current.Offset.ToString("0.###", CultureInfo.InvariantCulture),
            Margin = new Thickness(0, 2, 0, 10),
        };

        Button ok = new() { Content = "OK", Width = 80, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
        Button cancel = new() { Content = "Annuller", Width = 80, IsCancel = true };

        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);

        StackPanel panel = new() { Margin = new Thickness(14) };
        panel.Children.Add(new TextBlock { Text = "Dimensionsstil:" });
        panel.Children.Add(styleBox);
        panel.Children.Add(new TextBlock { Text = "Afstand fra akse (m):" });
        panel.Children.Add(offsetBox);
        panel.Children.Add(buttons);

        Window window = new()
        {
            Title = "PDANNOTATE – indstillinger",
            Content = panel,
            SizeToContent = SizeToContent.WidthAndHeight,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            MinWidth = 300,
        };

        ok.Click += (_, _) =>
        {
            if (!double.TryParse(offsetBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double offset) || offset <= 0.0)
            {
                MessageBox.Show(window, "Afstanden skal være et positivt tal.", "PDANNOTATE", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string styleName = styleBox.SelectedItem as string ?? string.Empty;
            PipePlanDEAnnotationStore.Set(db, new PipePlanDEAnnotationSettings(styleName, offset));
            window.DialogResult = true;
        };

        AcadApp.ShowModalWindow(window);
    }

    private static List<string> ReadDimStyleNames(Database db)
    {
        List<string> names = new();
        using Transaction tx = db.TransactionManager.StartTransaction();
        try
        {
            DimStyleTable table = (DimStyleTable)tx.GetObject(db.DimStyleTableId, OpenMode.ForRead);
            foreach (ObjectId id in table)
            {
                DimStyleTableRecord record = (DimStyleTableRecord)tx.GetObject(id, OpenMode.ForRead);
                names.Add(record.Name);
            }

            tx.Commit();
        }
        catch
        {
            tx.Abort();
            throw;
        }

        names.Sort(StringComparer.OrdinalIgnoreCase);
        return names;
    }
}
