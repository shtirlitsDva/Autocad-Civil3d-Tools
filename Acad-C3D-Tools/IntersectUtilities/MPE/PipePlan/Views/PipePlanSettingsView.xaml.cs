using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using IntersectUtilities.MPE.PipePlan.ViewModels;

namespace IntersectUtilities.MPE.PipePlan.Views;

internal partial class PipePlanSettingsView : UserControl
{
    public PipePlanSettingsView(PipePlanSettingsViewModel viewModel)
    {
        Resources = LoadTheme();
        InitializeComponent();
        DataContext = viewModel;
    }

    private static ResourceDictionary LoadTheme()
    {
        var asm = typeof(PipePlanSettingsView).Assembly;
        using var stream = asm.GetManifestResourceStream("IntersectUtilities.MPE.PipePlan.DarkTheme.xaml")
            ?? throw new InvalidOperationException("Embedded DarkTheme.xaml not found");
        return (ResourceDictionary)XamlReader.Load(stream);
    }
}
