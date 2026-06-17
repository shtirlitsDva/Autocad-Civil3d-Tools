using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using IntersectUtilities.MPE.PipePlanDE.ViewModels;

namespace IntersectUtilities.MPE.PipePlanDE.Views;

internal partial class PipePlanDEView : UserControl
{
    public PipePlanDEView(PipePlanDEViewModel viewModel)
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
        var asm = typeof(PipePlanDEView).Assembly;
        using var stream = asm.GetManifestResourceStream("IntersectUtilities.MPE.PipePlan.DarkTheme.xaml")
            ?? throw new InvalidOperationException("Embedded DarkTheme.xaml not found");
        return (ResourceDictionary)XamlReader.Load(stream);
    }
}
