using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using IntersectUtilities.MPE.PipePlanDE.ViewModels;

namespace IntersectUtilities.MPE.PipePlanDE.Views;

internal partial class PipePlanDESizeView : UserControl
{
    public PipePlanDESizeView(PipePlanDESizeViewModel viewModel)
    {
        // Reuse the PipePlan dark theme (embedded in this same assembly). Resources
        // must be assigned before InitializeComponent so the StaticResource lookups
        // in the XAML resolve.
        Resources = LoadTheme();
        InitializeComponent();
        DataContext = viewModel;
    }

    private static ResourceDictionary LoadTheme()
    {
        var asm = typeof(PipePlanDESizeView).Assembly;
        using var stream = asm.GetManifestResourceStream("IntersectUtilities.MPE.PipePlan.DarkTheme.xaml")
            ?? throw new InvalidOperationException("Embedded DarkTheme.xaml not found");
        return (ResourceDictionary)XamlReader.Load(stream);
    }
}
