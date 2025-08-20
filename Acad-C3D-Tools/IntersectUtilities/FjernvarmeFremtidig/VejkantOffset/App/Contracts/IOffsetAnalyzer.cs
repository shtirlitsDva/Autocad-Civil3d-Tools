using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.App.Contracts
{
	public interface IOffsetAnalyzer
	{
		Polyline? Analyze(
			Line workingLine,
			VejkantOffsetSettings settings,
			IEnumerable<Polyline> dimPlines,
			IEnumerable<Polyline> gkPlines,
			out SamplerSnapshot snapshot);
	}
}



