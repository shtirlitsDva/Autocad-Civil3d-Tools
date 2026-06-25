using System.Windows;

namespace NorsynDistrictZones.UI;

/// <summary>Tiny dark modal for renaming a price catalog. Returns the trimmed name via <see cref="CatalogName"/>.</summary>
public partial class RenameCatalogWindow : Window
{
    public string CatalogName { get; private set; }

    public RenameCatalogWindow(string current)
    {
        InitializeComponent();
        DarkTitleBar.Apply(this);
        CatalogName = current;
        NameBox.Text = current;
        Loaded += (_, _) => { NameBox.Focus(); NameBox.SelectAll(); };
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        string name = NameBox.Text?.Trim() ?? string.Empty;
        if (name.Length == 0) return; // OK is a no-op on an empty name; the dialog stays open
        CatalogName = name;
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
