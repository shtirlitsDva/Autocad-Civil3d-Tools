using Autodesk.AutoCAD.DatabaseServices;

namespace IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.App.Contracts
{
	public interface ITransientRenderer
	{
		void Show(PreviewModel model);
		void Clear();
	}
}



