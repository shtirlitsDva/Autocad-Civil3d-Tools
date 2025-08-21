using CommunityToolkit.Mvvm.ComponentModel;

using IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.UI.Models;

namespace IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.UI.ViewModels
{
	public partial class OffsetPaletteViewModel : ObservableObject
	{
		[ObservableProperty]
		private double currentLength;

		[ObservableProperty]
		private string? chosenSideLabel;

		public void Update(OffsetInspectorModel model)
		{
			CurrentLength = model.Length;
			ChosenSideLabel = model.ChosenSideLabel;
		}
	}
}



