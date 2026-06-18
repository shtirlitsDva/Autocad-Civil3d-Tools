using System.Windows.Controls;
using IntersectUtilities.MPE.PipePlanDE.ViewModels;

namespace IntersectUtilities.MPE.PipePlanDE.Views;

internal partial class PipePlanDESizeView : UserControl
{
    public PipePlanDESizeView(PipePlanDESizeViewModel viewModel)
    {
        // Resources must be assigned before InitializeComponent so the StaticResource
        // lookups in the XAML resolve.
        Resources = PipePlanDETheme.LoadDarkTheme();
        InitializeComponent();
        DataContext = viewModel;
    }
}
