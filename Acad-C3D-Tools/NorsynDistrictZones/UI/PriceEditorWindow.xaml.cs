using System.Windows;

namespace NorsynDistrictZones.UI;

public partial class PriceEditorWindow : Window
{
    public PriceEditorWindow(PriceEditorViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.RequestClose += Close;
    }
}
