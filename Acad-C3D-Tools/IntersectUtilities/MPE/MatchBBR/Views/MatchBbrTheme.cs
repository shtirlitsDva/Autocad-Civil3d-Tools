using System;
using System.IO;
using System.Windows;
using System.Windows.Markup;

namespace IntersectUtilities.MPE.MatchBBR.Views
{
    // Builds the resource dictionary both MatchBBR views run on.
    //
    // Everything is loaded from embedded manifest resources and merged in code rather than
    // through XAML Source="..." references. WPF's pack-URI resolver goes through Assembly.Load,
    // which only sees the default AssemblyLoadContext — and IntersectUtilities is hot-loaded into
    // a collectible ALC by NSLOAD/DevReload, so a pack URI resolves to nothing and the styles
    // silently vanish. GetManifestResourceStream sidesteps the whole mechanism.
    //
    // The base is PipePlan's DarkTheme (brushes plus the common control styles); MatchBbrTheme
    // adds the DataGrid chrome it does not cover. MatchBbrTheme is merged second so its keys win
    // on any overlap.
    internal static class MatchBbrTheme
    {
        private const string BaseThemeResourceName = "IntersectUtilities.MPE.PipePlan.DarkTheme.xaml";
        private const string GridThemeResourceName = "IntersectUtilities.MPE.MatchBBR.MatchBbrTheme.xaml";

        public static ResourceDictionary Load()
        {
            ResourceDictionary dictionary = LoadEmbedded(BaseThemeResourceName);
            dictionary.MergedDictionaries.Add(LoadEmbedded(GridThemeResourceName));
            return dictionary;
        }

        private static ResourceDictionary LoadEmbedded(string resourceName)
        {
            var assembly = typeof(MatchBbrTheme).Assembly;
            using Stream stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException(
                    $"Embedded resource '{resourceName}' was not found. "
                    + "Check the EmbeddedResource/LogicalName entry in IntersectUtilities.csproj.");

            return (ResourceDictionary)XamlReader.Load(stream);
        }
    }
}
