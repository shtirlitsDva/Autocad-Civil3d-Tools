using System.Windows;
using System.Windows.Markup;

namespace IntersectUtilities.MPE.PipePlanDE.Views;

/// <summary>
/// Loads the shared PipePlan dark theme for the PipePlanDE views. The theme is an
/// embedded resource (a plugin DLL has no pack:// application context), so it is
/// streamed from the manifest. Assign the result to a view's <c>Resources</c> before
/// <c>InitializeComponent</c> so the StaticResource lookups in its XAML resolve.
/// </summary>
internal static class PipePlanDETheme
{
    public static ResourceDictionary LoadDarkTheme()
    {
        var asm = typeof(PipePlanDETheme).Assembly;
        using var stream = asm.GetManifestResourceStream("IntersectUtilities.MPE.PipePlan.DarkTheme.xaml")
            ?? throw new InvalidOperationException("Embedded DarkTheme.xaml not found");
        return (ResourceDictionary)XamlReader.Load(stream);
    }
}
