using System.Windows;

namespace NorsynDistrictZones.UI;

public partial class PriceEditorWindow : Window
{
    public PriceEditorWindow(PriceEditorViewModel vm)
    {
        InitializeComponent();
        DarkTitleBar.Apply(this);
        DataContext = vm;
        vm.RequestClose += Close;
        vm.RenameRequested = current =>
        {
            var dlg = new RenameCatalogWindow(current) { Owner = this };
            return dlg.ShowDialog() == true ? dlg.CatalogName : null;
        };
    }
}
