using IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.App.Contracts;
using IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.UI.Models;
using IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.UI.ViewModels;

namespace IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.UI.Views
{
	internal sealed class OffsetPaletteViewModelVisualizer : IVisualizer<OffsetInspectorModel>
	{
		private readonly OffsetPaletteViewModel _vm = new();

		public void Show()
		{
			// Palette hosting TBD; for now no-op beyond VM lifetime
		}

		public void Update(OffsetInspectorModel model)
		{
			_vm.Update(model);
		}

		public void Hide()
		{
			// No-op for now
		}
	}
}



