using IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.App.Contracts;
using IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.Core.Models;
using IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.UI.Models;

using Autodesk.AutoCAD.DatabaseServices;

namespace IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.UI
{
	internal sealed class VejkantInspectorMapper : IInspectorMapper<VejkantAnalysis, OffsetInspectorModel>
	{
		public OffsetInspectorModel Map(VejkantAnalysis analysis, Line workingLine)
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


