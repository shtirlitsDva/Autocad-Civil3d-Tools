using IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.App.Contracts;
using IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.UI.ViewModels;

namespace IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.UI.Views
{
	internal sealed class OffsetPaletteViewModelVisualizer : IWpfVisualizer
	{
		private readonly OffsetPaletteViewModel _vm = new();

		public void Show()
		{
			// Palette hosting TBD; for now no-op beyond VM lifetime
		}

		public void Update(SamplerSnapshot snapshot)
		{
			_vm.Update(snapshot);
		}

		public void Hide()
		{
			// No-op for now
		}
	}
}


