using System.Windows.Controls;

using IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.UI.ViewModels;

namespace IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.UI.Views
{
	public partial class OffsetPaletteView : UserControl
	{
		public OffsetPaletteView()
		{
			InitializeComponent();
			DataContext = new OffsetPaletteViewModel();
		}
	}
}



