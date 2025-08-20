using CommunityToolkit.Mvvm.ComponentModel;

using IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.App.Contracts;

namespace IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.UI.ViewModels
{
	public partial class OffsetPaletteViewModel : ObservableObject
	{
		[ObservableProperty]
		private double currentLength;

		[ObservableProperty]
		private string? chosenSideLabel;

		public void Update(SamplerSnapshot snapshot)
		{
			CurrentLength = snapshot.Length;
			ChosenSideLabel = snapshot.ChosenSideLabel;
		}
	}
}



