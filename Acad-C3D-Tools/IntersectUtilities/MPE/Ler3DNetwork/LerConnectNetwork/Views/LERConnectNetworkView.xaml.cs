using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using IntersectUtilities.MPE.Ler3DNetwork.LerConnectNetwork.ViewModels;

namespace IntersectUtilities.MPE.Ler3DNetwork.LerConnectNetwork.Views
{
    internal partial class LERConnectNetworkView : UserControl
    {
        public LERConnectNetworkView(LERConnectNetworkViewModel viewModel)
        {
            // Reuse PipePlan's embedded dark theme — it is generic (brushes +
            // control styles only) and already shipped as a manifest resource,
            // so no .csproj change is needed for this feature.
            Resources = LoadTheme();
            InitializeComponent();
            DataContext = viewModel;
        }

        private static ResourceDictionary LoadTheme()
        {
            var asm = typeof(LERConnectNetworkView).Assembly;
            using var stream = asm.GetManifestResourceStream("IntersectUtilities.MPE.PipePlan.DarkTheme.xaml")
                ?? throw new InvalidOperationException("Embedded DarkTheme.xaml not found");
            return (ResourceDictionary)XamlReader.Load(stream);
        }
    }
}
