using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media.Imaging;
using IntersectUtilities.MPE.PipePlanDE.ViewModels;

namespace IntersectUtilities.MPE.PipePlanDE.Views;

internal partial class PipePlanDESettingsView : UserControl
{
    public PipePlanDESettingsView(PipePlanDESettingsViewModel viewModel)
    {
        // Reuse the PipePlan dark theme (embedded in this same assembly). Resources
        // must be assigned before InitializeComponent so the StaticResource lookups
        // in the XAML resolve.
        Resources = LoadTheme();
        InitializeComponent();
        DataContext = viewModel;
        DiagramImage.Source = LoadDiagram();
    }

    private static ResourceDictionary LoadTheme()
    {
        var asm = typeof(PipePlanDESettingsView).Assembly;
        using var stream = asm.GetManifestResourceStream("IntersectUtilities.MPE.PipePlan.DarkTheme.xaml")
            ?? throw new InvalidOperationException("Embedded DarkTheme.xaml not found");
        return (ResourceDictionary)XamlReader.Load(stream);
    }

    // The Regel-Grabenprofil diagram is an embedded PNG (a plugin DLL has no
    // pack:// application context, so we stream it from the manifest like the theme).
    // CacheOption.OnLoad fully decodes before the stream is disposed.
    private static BitmapImage? LoadDiagram()
    {
        var asm = typeof(PipePlanDESettingsView).Assembly;
        using var stream = asm.GetManifestResourceStream("IntersectUtilities.MPE.PipePlanDE.RegelGrabenprofil.png");
        if (stream is null)
        {
            return null;
        }

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}
