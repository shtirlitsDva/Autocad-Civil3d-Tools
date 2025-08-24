using System.Windows.Controls;

using IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.UI.ViewModels;

namespace IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.UI.Views
{
	public partial class OffsetPaletteView : UserControl
	{
        IntersectionVisualizationViewModel vm = new();
		public OffsetPaletteView()
		{
			InitializeComponent();
			DataContext = vm;
		}
	}
}
