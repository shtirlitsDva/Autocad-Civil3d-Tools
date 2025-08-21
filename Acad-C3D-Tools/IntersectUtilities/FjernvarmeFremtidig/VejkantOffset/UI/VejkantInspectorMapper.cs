using IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.App.Contracts;
using IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.Core.Models;
using IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.UI.Models;

namespace IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.UI
{
	internal sealed class VejkantInspectorMapper : IInspectorMapper<VejkantAnalysis, OffsetInspectorModel>
	{
		public OffsetInspectorModel Map(VejkantAnalysis analysis, Autodesk.AutoCAD.DatabaseServices.Line workingLine)
		{
			return new OffsetInspectorModel
			{
				Length = analysis.Length,
				ChosenSideLabel = analysis.ChosenSideLabel,
				IntersectionsCount = analysis.GkIntersections.Count
			};
		}
	}
}


