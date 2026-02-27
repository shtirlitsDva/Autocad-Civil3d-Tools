using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

using NSPaletteSet.ViewModels;

namespace NSPaletteSet.Views
{
    public partial class PipePaletteView : UserControl
    {
        public PipePaletteView()
        {
            // Load theme from embedded resource BEFORE InitializeComponent.
            // MergedDictionaries with Source="..." fail in isolated ALCs because
            // WPF's pack URI resolver uses Assembly.Load (default ALC only).
            // Loading from EmbeddedResource via GetManifestResourceStream bypasses
            // pack URIs entirely — it uses the assembly object directly.
            Resources = LoadTheme();

            InitializeComponent();
            DataContext = new PipePaletteViewModel();
        }

        private static ResourceDictionary LoadTheme()
        {
            var asm = typeof(PipePaletteView).Assembly;
            using var stream = asm.GetManifestResourceStream("DarkTheme.xaml");
            return (ResourceDictionary)XamlReader.Load(stream!);
        }
    }
}
